using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System;
using System.IO.Pipes;
using System.Threading.Channels;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc;

/// <summary>
/// Hosted background service that opens a Windows named-pipe server, accepts connections
/// from the Electron broker, deserialises <see cref="CommandEnvelope"/> requests, dispatches
/// them through <see cref="CommandDispatcher"/>, and writes back <see cref="ResponseEnvelope"/>
/// responses using a 4-byte little-endian length-prefix framing protocol.
/// </summary>
/// <remarks>
/// <para>
/// Multiple accept loops run concurrently up to <see cref="MaxConcurrentConnections"/> to handle
/// back-to-back requests from the broker without head-of-line blocking.
/// Each connection handles exactly one request-response cycle, then closes.
/// </para>
/// <para>
/// <strong>Wizard multi-frame protocol</strong>: for <c>Generate.Wizard/v1</c> commands the host
/// writes zero or more progress frames before the final response envelope.  Each frame is a
/// length-prefixed JSON object that uses a <c>"t"</c> discriminator field:
/// <list type="bullet">
///   <item><term><c>{ "t": "p", "p": { …WizardProgressFrame } }</c></term><description>progress update</description></item>
///   <item><term><c>{ "t": "f", "e": { …ResponseEnvelope } }</c></term><description>final response (exactly one per connection)</description></item>
/// </list>
/// Non-wizard connections continue to use the legacy single-frame protocol so the broker can
/// detect the difference by the presence (or absence) of the <c>"t"</c> property.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Named-pipe I/O host: covered by integration tests, not unit tests.")]
internal sealed class NamedPipeCommandHost : BackgroundService
{
    /// <summary>The pipe name used by both the .NET host and the Electron broker.</summary>
    public const string PipeName = "godot-generator-ipc";

    /// <summary>Maximum payload size accepted; payloads larger than this are rejected.</summary>
    private const int MaxPayloadBytes = 1 * 1024 * 1024; // 1 MB

    /// <summary>Number of simultaneous accept loops; equals the max server instances.</summary>
    private const int MaxConcurrentConnections = 4;

    /// <summary>Default timeout for command execution when no command-specific timeout applies.</summary>
    private static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for non-wizard generate commands.</summary>
    private static readonly TimeSpan GenerateExecutionTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Longer timeout budget for wizard turns, which often chain multiple tool invocations.</summary>
    private static readonly TimeSpan WizardExecutionTimeout = TimeSpan.FromSeconds(240);

    /// <summary>Short grace timeout for writing responses after execution has completed/cancelled.</summary>
    private static readonly TimeSpan ResponseWriteGraceTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of progress frames written per wizard connection.
    /// Frames beyond this limit are silently dropped (the drain loop still consumes them
    /// so the channel does not stall dispatch).
    /// </summary>
    private const int MaxProgressFrames = 200;

    private readonly CommandDispatcher _dispatcher;
    private readonly ILogger<NamedPipeCommandHost> _logger;

    private static bool RawPayloadLoggingEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GODOT_MCP_RAW_LOG"));

    /// <summary>
    /// Initialises the host with the required dispatcher and logger.
    /// </summary>
    public NamedPipeCommandHost(CommandDispatcher dispatcher, ILogger<NamedPipeCommandHost> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DesktopIpc named-pipe host starting on pipe '{PipeName}' ({N} accept loops).",
            PipeName, MaxConcurrentConnections);

        var loops = Enumerable
            .Range(0, MaxConcurrentConnections)
            .Select(_ => AcceptLoopAsync(stoppingToken));

        await Task.WhenAll(loops).ConfigureAwait(false);

        _logger.LogInformation("DesktopIpc named-pipe host stopped.");
    }

    // ── Accept loop ──────────────────────────────────────────────────────────

    /// <summary>
    /// Continuously waits for a new pipe connection, fires off handling on a
    /// background task, and then immediately starts waiting for the next connection.
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                // Fire-and-forget: the accept loop continues; connection runs concurrently.
                _ = HandleConnectionAsync(server, ct);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                server?.Dispose();
                _logger.LogError(ex, "Unhandled error in pipe accept loop; retrying in 500 ms.");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    // ── Connection handler ───────────────────────────────────────────────────

    /// <summary>
    /// Reads one <see cref="CommandEnvelope"/> from the connected pipe stream, dispatches it,
    /// writes the response (single frame for normal commands; multi-frame for wizard), and
    /// disposes the connection.
    /// </summary>
    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken hostCt)
    {
        await using var _ = server.ConfigureAwait(false);
        CommandEnvelope? envelope = null;

        try
        {
            envelope = await ReadEnvelopeAsync(server, _logger, hostCt).ConfigureAwait(false);
            if (envelope is null)
            {
                return;
            }

            if (envelope.Command == GenerateCommandNames.Wizard)
            {
                await HandleWizardConnectionAsync(server, envelope, hostCt).ConfigureAwait(false);
            }
            else
            {
                await HandleStandardConnectionAsync(server, envelope, hostCt).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipe connection was cancelled before a response could be fully sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while handling pipe connection.");
        }
    }

    /// <summary>
    /// Handles a non-wizard command using the legacy single-frame response protocol.
    /// </summary>
    private async Task HandleStandardConnectionAsync(
        NamedPipeServerStream server,
        CommandEnvelope envelope,
        CancellationToken hostCt)
    {
        var executionTimeout = GetExecutionTimeout(envelope.Command);

        ResponseEnvelope response;
        using (var executionCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt))
        {
            executionCts.CancelAfter(executionTimeout);
            response = await _dispatcher.DispatchAsync(envelope, executionCts.Token).ConfigureAwait(false);
        }

        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        writeCts.CancelAfter(ResponseWriteGraceTimeout);
        await WriteResponseAsync(server, response, _logger, writeCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a wizard command using the multi-frame streaming protocol.
    /// </summary>
    /// <remarks>
    /// The connection proceeds in two concurrent phases:
    /// <list type="number">
    ///   <item>
    ///     <term>Dispatch</term>
    ///     <description>
    ///       <see cref="CommandDispatcher.DispatchAsync"/> runs with an
    ///       <see cref="WizardIpcProgressContext"/> installed.  Each progress callback
    ///       enqueues a <see cref="WizardProgressFrame"/> into a
    ///       <see cref="Channel{T}"/> without blocking dispatch.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Drain</term>
    ///     <description>
    ///       A concurrent reader dequeues frames and writes them to the pipe as
    ///       <c>{ "t": "p", "p": … }</c> JSON until the channel is completed.
    ///       The final <c>ResponseEnvelope</c> is then written as <c>{ "t": "f", "e": … }</c>.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    private async Task HandleWizardConnectionAsync(
        NamedPipeServerStream server,
        CommandEnvelope envelope,
        CancellationToken hostCt)
    {
        var frameChannel = Channel.CreateUnbounded<WizardProgressFrame>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        ResponseEnvelope response;

        try
        {
            using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
            executionCts.CancelAfter(WizardExecutionTimeout);

            // Install the progress sink BEFORE dispatching so the entire async call graph
            // (including SK continuations) can emit frames via WizardIpcProgressContext.
            using (WizardIpcProgressContext.Enter(frame => frameChannel.Writer.TryWrite(frame)))
            {
                var dispatchTask = _dispatcher.DispatchAsync(envelope, executionCts.Token);
                var drainTask = DrainProgressFramesAsync(server, frameChannel.Reader, _logger, executionCts.Token);

                try
                {
                    response = await dispatchTask.ConfigureAwait(false);
                }
                finally
                {
                    // Signal no more frames whether dispatch succeeded or failed.
                    frameChannel.Writer.TryComplete();
                }

                // Wait for all queued frames to be written before the final envelope.
                await drainTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Wizard connection timed out or was cancelled.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during wizard dispatch/drain.");
            // Attempt to write a failure envelope within the grace period.
            response = new ResponseEnvelope(
                envelope.CorrelationId, false, null,
                "HANDLER_FAULTED", "An unexpected error occurred during the wizard turn.");
        }

        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        writeCts.CancelAfter(ResponseWriteGraceTimeout);
        await WriteWizardFinalFrameAsync(server, response, _logger, writeCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads wizard progress frames from <paramref name="reader"/> and writes each one
    /// to <paramref name="stream"/> as a <c>{ "t": "p", "p": … }</c> length-prefixed frame.
    /// Frames beyond <see cref="MaxProgressFrames"/> are consumed but not written.
    /// </summary>
    private static async Task DrainProgressFramesAsync(
        PipeStream stream,
        ChannelReader<WizardProgressFrame> reader,
        ILogger logger,
        CancellationToken ct)
    {
        var written = 0;
        await foreach (var frame in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (written < MaxProgressFrames)
            {
                await WriteProgressFrameAsync(stream, frame, logger, ct).ConfigureAwait(false);
                written++;
            }
            // Continue consuming even when the cap is hit so the channel never stalls dispatch.
        }
    }

    // ── Wire protocol helpers ────────────────────────────────────────────────

    /// <summary>
    /// Reads a length-prefixed JSON payload from the stream and deserialises it as a
    /// <see cref="CommandEnvelope"/>. Returns <c>null</c> if the payload is invalid or
    /// exceeds <see cref="MaxPayloadBytes"/>.
    /// </summary>
    private static async Task<CommandEnvelope?> ReadEnvelopeAsync(PipeStream stream, ILogger logger, CancellationToken ct)
    {
        // 4-byte little-endian length prefix
        var lenBuf = new byte[4];
        var bytesRead = await ReadAtLeastAsync(stream, lenBuf, 4, ct).ConfigureAwait(false);
        if (bytesRead < 4)
        {
            return null;
        }

        var length = BitConverter.ToInt32(lenBuf);
        if (length <= 0 || length > MaxPayloadBytes)
        {
            return null;
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);

        if (RawPayloadLoggingEnabled)
        {
            try
            {
                var text = Encoding.UTF8.GetString(payload);
                logger.LogDebug("MCP IN: {Payload}", text.Length > 10000 ? text[..10000] + "...[truncated]" : text);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to decode MCP incoming payload for logging.");
            }
        }

        return JsonSerializer.Deserialize<CommandEnvelope>(payload, ContractJsonOptions.Default);
    }

    /// <summary>
    /// Serialises <paramref name="response"/> to a plain (legacy) length-prefixed JSON frame
    /// and writes it to the stream.  Used for all non-wizard commands.
    /// </summary>
    private static async Task WriteResponseAsync(PipeStream stream, ResponseEnvelope response, ILogger logger, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, ContractJsonOptions.Default);

        if (RawPayloadLoggingEnabled)
        {
            try
            {
                var text = Encoding.UTF8.GetString(payload);
                logger.LogDebug("MCP OUT (response): {Payload}", text.Length > 10000 ? text[..10000] + "...[truncated]" : text);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to decode outgoing response payload for logging.");
            }
        }

        var lenBuf = BitConverter.GetBytes(payload.Length);

        await stream.WriteAsync(lenBuf, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a wizard progress frame as <c>{ "t": "p", "p": … }</c>.
    /// </summary>
    private static async Task WriteProgressFrameAsync(
        PipeStream stream,
        WizardProgressFrame frame,
        ILogger logger,
        CancellationToken ct)
    {
        var wrapper = new WizardProgressWireFrame("p", Progress: frame, Envelope: null);
        var payload = JsonSerializer.SerializeToUtf8Bytes(wrapper, ContractJsonOptions.Default);

        if (RawPayloadLoggingEnabled)
        {
            try
            {
                var text = Encoding.UTF8.GetString(payload);
                logger.LogDebug("MCP OUT (progress): {Payload}", text.Length > 10000 ? text[..10000] + "...[truncated]" : text);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to decode outgoing progress payload for logging.");
            }
        }

        var lenBuf = BitConverter.GetBytes(payload.Length);

        await stream.WriteAsync(lenBuf, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the wizard final frame as <c>{ "t": "f", "e": … }</c>.
    /// </summary>
    private static async Task WriteWizardFinalFrameAsync(
        PipeStream stream,
        ResponseEnvelope envelope,
        ILogger logger,
        CancellationToken ct)
    {
        var wrapper = new WizardProgressWireFrame("f", Progress: null, Envelope: envelope);
        var payload = JsonSerializer.SerializeToUtf8Bytes(wrapper, ContractJsonOptions.Default);

        if (RawPayloadLoggingEnabled)
        {
            try
            {
                var text = Encoding.UTF8.GetString(payload);
                logger.LogDebug("MCP OUT (final): {Payload}", text.Length > 10000 ? text[..10000] + "...[truncated]" : text);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to decode outgoing final payload for logging.");
            }
        }

        var lenBuf = BitConverter.GetBytes(payload.Length);

        await stream.WriteAsync(lenBuf, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Polyfill for <c>Stream.ReadAtLeastAsync</c> targeting net10.0.</summary>
    private static async Task<int> ReadAtLeastAsync(Stream stream, byte[] buffer, int minimum, CancellationToken ct)
    {
        var total = 0;
        while (total < minimum)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    /// <summary>
    /// Gets command-specific execution timeout values, giving wizard generation a longer budget.
    /// </summary>
    private static TimeSpan GetExecutionTimeout(string command)
    {
        return command switch
        {
            GenerateCommandNames.Wizard => WizardExecutionTimeout,
            GenerateCommandNames.Text
                or GenerateCommandNames.Code
                or GenerateCommandNames.Image
                or GenerateCommandNames.Audio
                or GenerateCommandNames.Video
                or GenerateCommandNames.Sprites
                or GenerateCommandNames.GodotUi
                or GenerateCommandNames.GodotPhysics
                or GenerateCommandNames.GodotProject
                or GenerateCommandNames.Scenes
                or GenerateCommandNames.Animations
                or GenerateCommandNames.GodotLighting
                or GenerateCommandNames.GodotCamera
                or GenerateCommandNames.GodotShaders
                or GenerateCommandNames.GodotSignals
                or GenerateCommandNames.GodotNodes => GenerateExecutionTimeout,
            _ => DefaultExecutionTimeout,
        };
    }

    // ── Wire DTOs (internal to pipe protocol) ────────────────────────────────

    /// <summary>
    /// Discriminated wire wrapper for wizard streaming frames.
    /// <list type="bullet">
    ///   <item><c>t = "p"</c> — progress frame; <see cref="Progress"/> is set, <see cref="Envelope"/> is null.</item>
    ///   <item><c>t = "f"</c> — final frame; <see cref="Envelope"/> is set, <see cref="Progress"/> is null.</item>
    /// </list>
    /// </summary>
    private sealed record WizardProgressWireFrame(
        [property: System.Text.Json.Serialization.JsonPropertyName("t")]
        string Type,
        [property: System.Text.Json.Serialization.JsonPropertyName("p")]
        WizardProgressFrame? Progress,
        [property: System.Text.Json.Serialization.JsonPropertyName("e")]
        ResponseEnvelope? Envelope);
}

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.IO.Pipes;
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
/// Multiple accept loops run concurrently up to <see cref="MaxConcurrentConnections"/> to handle
/// back-to-back requests from the broker without head-of-line blocking.
/// Each connection handles exactly one request-response cycle, then closes.
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
    private static readonly TimeSpan GenerateExecutionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Longer timeout budget for wizard turns, which often chain multiple tool invocations.</summary>
    private static readonly TimeSpan WizardExecutionTimeout = TimeSpan.FromSeconds(240);

    /// <summary>Short grace timeout for writing responses after execution has completed/cancelled.</summary>
    private static readonly TimeSpan ResponseWriteGraceTimeout = TimeSpan.FromSeconds(5);

    private readonly CommandDispatcher _dispatcher;
    private readonly ILogger<NamedPipeCommandHost> _logger;

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
    /// writes the <see cref="ResponseEnvelope"/>, and disposes the connection.
    /// </summary>
    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken hostCt)
    {
        await using var _ = server.ConfigureAwait(false);
        CommandEnvelope? envelope = null;

        try
        {
            envelope = await ReadEnvelopeAsync(server, hostCt).ConfigureAwait(false);
            if (envelope is null)
            {
                return;
            }
            var executionTimeout = GetExecutionTimeout(envelope.Command);

            ResponseEnvelope response;
            using (var executionCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt))
            {
                executionCts.CancelAfter(executionTimeout);
                response = await _dispatcher.DispatchAsync(envelope, executionCts.Token).ConfigureAwait(false);
            }

            using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
            writeCts.CancelAfter(ResponseWriteGraceTimeout);
            await WriteResponseAsync(server, response, writeCts.Token).ConfigureAwait(false);
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

    // ── Wire protocol helpers ────────────────────────────────────────────────

    /// <summary>
    /// Reads a length-prefixed JSON payload from the stream and deserialises it as a
    /// <see cref="CommandEnvelope"/>. Returns <c>null</c> if the payload is invalid or
    /// exceeds <see cref="MaxPayloadBytes"/>.
    /// </summary>
    private static async Task<CommandEnvelope?> ReadEnvelopeAsync(PipeStream stream, CancellationToken ct)
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

        return JsonSerializer.Deserialize<CommandEnvelope>(payload, ContractJsonOptions.Default);
    }

    /// <summary>
    /// Serialises <paramref name="response"/> to UTF-8 JSON and writes it to the stream
    /// prefixed by a 4-byte little-endian length.
    /// </summary>
    private static async Task WriteResponseAsync(PipeStream stream, ResponseEnvelope response, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, ContractJsonOptions.Default);
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
}

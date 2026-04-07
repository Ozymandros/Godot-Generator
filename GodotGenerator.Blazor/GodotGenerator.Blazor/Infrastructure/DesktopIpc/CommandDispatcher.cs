using GodotGenerator.Desktop.Contracts.Envelope;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc;

/// <summary>
/// Routes incoming <see cref="CommandEnvelope"/> instances to the appropriate
/// <see cref="ICommandHandler"/> by command name. Provides uniform error mapping
/// for unknown commands, cancellation, and unhandled handler exceptions.
/// </summary>
internal sealed class CommandDispatcher
{
    private readonly IReadOnlyDictionary<string, ICommandHandler> _handlers;
    private readonly ILogger<CommandDispatcher> _logger;

    /// <summary>
    /// Initialises the dispatcher by indexing every handler by its declared command names.
    /// </summary>
    /// <param name="handlers">All registered <see cref="ICommandHandler"/> implementations.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public CommandDispatcher(IEnumerable<ICommandHandler> handlers, ILogger<CommandDispatcher> logger)
    {
        var dict = new Dictionary<string, ICommandHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            foreach (var name in handler.CommandNames)
            {
                dict[name] = handler;
            }
        }

        _handlers = dict;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches the given <paramref name="envelope"/> to its handler and returns a
    /// <see cref="ResponseEnvelope"/>. Never throws; all failures are mapped to typed error codes.
    /// </summary>
    /// <param name="envelope">The incoming command envelope.</param>
    /// <param name="ct">Cancellation token from the pipe host timeout policy.</param>
    public async Task<ResponseEnvelope> DispatchAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(envelope.Command, out var handler))
        {
            _logger.LogWarning("No handler registered for IPC command '{Command}'.", envelope.Command);
            return Failure(envelope.CorrelationId, DesktopErrorCode.UnknownCommand,
                $"No handler registered for command '{envelope.Command}'.");
        }

        try
        {
            return await handler.HandleAsync(envelope, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("IPC command '{Command}' was cancelled (correlation: {Id}).",
                envelope.Command, envelope.CorrelationId);
            return Failure(envelope.CorrelationId, DesktopErrorCode.Timeout,
                "Command execution was cancelled or timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPC handler faulted for command '{Command}' (correlation: {Id}).",
                envelope.Command, envelope.CorrelationId);
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted,
                "An unexpected error occurred while processing the command.");
        }
    }

    /// <summary>Gets the set of registered command names; used by tests and diagnostics.</summary>
    public IReadOnlyCollection<string> RegisteredCommandNames =>
        _handlers.Keys is IReadOnlyCollection<string> keys ? keys : _handlers.Keys.ToArray();

    private static ResponseEnvelope Failure(string correlationId, string errorCode, string message) =>
        new(correlationId, false, null, errorCode, message);
}

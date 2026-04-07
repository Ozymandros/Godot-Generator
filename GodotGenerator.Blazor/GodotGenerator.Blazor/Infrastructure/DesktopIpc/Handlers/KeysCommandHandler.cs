using System.Text.Json;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;

/// <summary>
/// Handles the <see cref="KeysCommandNames.Save"/> IPC command by delegating to
/// <see cref="IGodotGeneratorApiService.SaveApiKeysAsync"/> and packaging the result
/// into a <see cref="ResponseEnvelope"/>.
/// </summary>
internal sealed class KeysCommandHandler : ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KeysCommandHandler> _logger;

    /// <summary>
    /// Initialises the handler with the API service and logger.
    /// </summary>
    public KeysCommandHandler(IServiceScopeFactory scopeFactory, ILogger<KeysCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CommandNames { get; } = [KeysCommandNames.Save];

    /// <inheritdoc/>
    public async Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        _logger.LogDebug("Handling IPC command '{Command}' (correlation: {Id}).",
            envelope.Command, envelope.CorrelationId);

        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                "Keys.Save requires a JSON payload with 'keys'.");
        }

        var request = JsonSerializer.Deserialize<KeysSaveRequest>(
            envelope.PayloadJson, ContractJsonOptions.Default);

        if (request is null || request.Keys.Count == 0)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                "Keys.Save: at least one key entry is required.");
        }

        var apiRequest = new ApiKeysRequest(request.Keys);
        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();
        var result = await apiService.SaveApiKeysAsync(apiRequest, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var saved = result.Data?.GetValueOrDefault("saved") as IReadOnlyList<string>
                    ?? Array.Empty<string>();

        var response = new KeysSaveResponse(saved);
        return new ResponseEnvelope(
            envelope.CorrelationId, true,
            JsonSerializer.Serialize(response, ContractJsonOptions.Default),
            null, null);
    }

    private static ResponseEnvelope Failure(string correlationId, string errorCode, string? message) =>
        new(correlationId, false, null, errorCode, message);
}

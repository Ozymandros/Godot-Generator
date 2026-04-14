using System.Text.Json;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;

/// <summary>
/// Handles <see cref="PromptAssistCommandNames.Enhance"/> IPC commands by delegating
/// to <see cref="IGodotGeneratorApiService.EnhancePromptAsync"/>.
/// No Godot MCP plugin tools are invoked on this path.
/// </summary>
internal sealed class PromptAssistCommandHandler : ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PromptAssistCommandHandler> _logger;

    /// <summary>Initialises the handler.</summary>
    public PromptAssistCommandHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<PromptAssistCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CommandNames { get; } =
        [PromptAssistCommandNames.Enhance];

    /// <inheritdoc/>
    public async Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        _logger.LogDebug(
            "Handling IPC command '{Command}' (correlation: {Id}).",
            envelope.Command,
            envelope.CorrelationId);

        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: a JSON payload is required.");
        }

        PromptAssistEnhanceRequest? cmdRequest;
        try
        {
            cmdRequest = JsonSerializer.Deserialize<PromptAssistEnhanceRequest>(
                envelope.PayloadJson, ContractJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: invalid JSON payload — {ex.Message}");
        }

        if (cmdRequest is null || string.IsNullOrWhiteSpace(cmdRequest.Modality))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: 'modality' is required.");
        }

        var appRequest = new PromptAssistRequest(
            cmdRequest.Modality,
            cmdRequest.CurrentPrompt ?? string.Empty,
            cmdRequest.SystemPromptOverride,
            cmdRequest.Provider,
            cmdRequest.PreferredModelId);

        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();

        var result = await apiService
            .EnhancePromptAsync(appRequest, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var response = new PromptAssistEnhanceResponse(
            Result: result.Data?["result"]?.ToString() ?? string.Empty,
            Mode: result.Data?["mode"]?.ToString() ?? "improve");

        return new ResponseEnvelope(
            envelope.CorrelationId, true,
            JsonSerializer.Serialize(response, ContractJsonOptions.Default),
            null, null);
    }

    private static ResponseEnvelope Failure(string correlationId, string errorCode, string? message) =>
        new(correlationId, false, null, errorCode, message);
}

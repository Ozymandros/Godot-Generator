using System.Text.Json;
using System.IO;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;

/// <summary>
/// Handles <see cref="PreferenceCommandNames.Get"/> and <see cref="PreferenceCommandNames.Set"/>
/// IPC commands by delegating to <see cref="IGodotGeneratorApiService"/>.
/// </summary>
internal sealed class PreferenceCommandHandler : ICommandHandler
{
    private const string AgentDebugLogPath = @"C:\Projects\Godot-Generator-Avalonia\debug-cb9046.log";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PreferenceCommandHandler> _logger;

    /// <summary>
    /// Initialises the handler with the API service and logger.
    /// </summary>
    public PreferenceCommandHandler(IServiceScopeFactory scopeFactory, ILogger<PreferenceCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CommandNames { get; } =
        [PreferenceCommandNames.Get, PreferenceCommandNames.Set];

    /// <inheritdoc/>
    public async Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        _logger.LogDebug("Handling IPC command '{Command}' (correlation: {Id}).",
            envelope.Command, envelope.CorrelationId);

        return envelope.Command switch
        {
            PreferenceCommandNames.Get  => await HandleGetAsync(envelope, ct).ConfigureAwait(false),
            PreferenceCommandNames.Set  => await HandleSetAsync(envelope, ct).ConfigureAwait(false),
            _ => Failure(envelope.CorrelationId, DesktopErrorCode.UnknownCommand,
                    $"Unhandled preference command '{envelope.Command}'.")
        };
    }

    // ── Get ──────────────────────────────────────────────────────────────────

    private async Task<ResponseEnvelope> HandleGetAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                "Preference.Get requires a JSON payload with 'key'.");
        }

        var request = JsonSerializer.Deserialize<PreferenceGetRequest>(
            envelope.PayloadJson, ContractJsonOptions.Default);

        if (request is null || string.IsNullOrWhiteSpace(request.Key))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                "Preference.Get: 'key' is required.");
        }

        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();
        var result = await apiService.GetPreferenceAsync(request.Key, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var response = new PreferenceGetResponse(
            request.Key,
            result.Data?.GetValueOrDefault("value"));

        return Success(envelope.CorrelationId, response);
    }

    // ── Set ──────────────────────────────────────────────────────────────────

    private async Task<ResponseEnvelope> HandleSetAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                "Preference.Set requires a JSON payload with 'key' and 'value'.");
        }

        var request = JsonSerializer.Deserialize<PreferenceSetRequest>(
            envelope.PayloadJson, ContractJsonOptions.Default);

        if (request is null || string.IsNullOrWhiteSpace(request.Key))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                "Preference.Set: 'key' is required.");
        }

        #region agent log
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                sessionId = "cb9046",
                runId = "initial",
                hypothesisId = "H7",
                location = "PreferenceCommandHandler.cs:HandleSetAsync",
                message = "IPC Preference.Set received",
                data = new
                {
                    key = request.Key,
                    valueLength = request.Value?.Length ?? 0,
                    correlationId = envelope.CorrelationId
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
        }
        catch
        {
            // no-op
        }
        #endregion

        var apiRequest = new SetPreferenceRequest(request.Key, request.Value);
        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();
        var result = await apiService.SetPreferenceAsync(apiRequest, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var response = new PreferenceSetResponse(
            request.Key,
            result.Data?.GetValueOrDefault("value"));

        return Success(envelope.CorrelationId, response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ResponseEnvelope Success<T>(string correlationId, T payload) =>
        new(correlationId, true,
            JsonSerializer.Serialize(payload, ContractJsonOptions.Default),
            null, null);

    private static ResponseEnvelope Failure(string correlationId, string errorCode, string? message) =>
        new(correlationId, false, null, errorCode, message);
}

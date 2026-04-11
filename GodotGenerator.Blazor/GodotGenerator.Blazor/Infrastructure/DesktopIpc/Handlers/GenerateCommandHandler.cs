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
/// Handles all <c>Generate.*</c> IPC commands by mapping the versioned command name to the
/// appropriate <see cref="IGodotGeneratorApiService"/> method and returning the result.
/// </summary>
internal sealed class GenerateCommandHandler : ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerateCommandHandler> _logger;

    /// <summary>
    /// Initialises the handler with the API service and logger.
    /// </summary>
    public GenerateCommandHandler(IServiceScopeFactory scopeFactory, ILogger<GenerateCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CommandNames => GenerateCommandNames.All;

    /// <inheritdoc/>
    public async Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        _logger.LogDebug("Handling IPC command '{Command}' (correlation: {Id}).",
            envelope.Command, envelope.CorrelationId);

        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: a JSON payload with 'prompt' is required.");
        }

        var cmdRequest = JsonSerializer.Deserialize<GenerateCommandRequest>(
            envelope.PayloadJson, ContractJsonOptions.Default);

        if (cmdRequest is null || string.IsNullOrWhiteSpace(cmdRequest.Prompt))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: 'prompt' is required.");
        }

        // Map IReadOnlyDictionary → Dictionary for GenerateRequest compat
        var options = cmdRequest.Options is not null
            ? new Dictionary<string, object?>(cmdRequest.Options, StringComparer.OrdinalIgnoreCase)
            : null;

        var apiRequest = new GenerateRequest(
            cmdRequest.Prompt,
            cmdRequest.Provider,
            options,
            cmdRequest.ApiKey,
            cmdRequest.SystemPrompt,
            cmdRequest.ProjectName,
            cmdRequest.PreferredModelId);

        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();

        var result = await DispatchToModalityAsync(apiService, envelope.Command, apiRequest, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var response = new GenerateCommandResponse(
            result.Data ?? new Dictionary<string, object?>());

        return new ResponseEnvelope(
            envelope.CorrelationId, true,
            JsonSerializer.Serialize(response, ContractJsonOptions.Default),
            null, null);
    }

    // ── Modality dispatch ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps a versioned generate command name to the corresponding
    /// <see cref="IGodotGeneratorApiService"/> method.
    /// </summary>
    private static Task<Api.Dtos.ApiResponse<Dictionary<string, object?>>> DispatchToModalityAsync(
        IGodotGeneratorApiService apiService,
        string command,
        GenerateRequest request,
        CancellationToken ct) =>
        command switch
        {
            GenerateCommandNames.Text         => apiService.GenerateTextAsync(request, ct),
            GenerateCommandNames.Code         => apiService.GenerateCodeAsync(request, ct),
            GenerateCommandNames.Image        => apiService.GenerateImageAsync(request, ct),
            GenerateCommandNames.Audio        => apiService.GenerateAudioAsync(request, ct),
            GenerateCommandNames.Video        => apiService.GenerateVideoAsync(request, ct),
            GenerateCommandNames.Sprites      => apiService.GenerateSpritesAsync(request, ct),
            GenerateCommandNames.GodotUi       => apiService.GenerateGodotUiAsync(request, ct),
            GenerateCommandNames.GodotPhysics  => apiService.GenerateGodotPhysicsAsync(request, ct),
            GenerateCommandNames.GodotProject  => apiService.GenerateGodotProjectAsync(request, ct),
            GenerateCommandNames.Scenes        => apiService.CreateSceneAsync(request, ct),
            GenerateCommandNames.Animations    => apiService.GenerateAnimationsAsync(request, ct),
            GenerateCommandNames.GodotLighting => apiService.GenerateGodotLightingAsync(request, ct),
            GenerateCommandNames.GodotCamera   => apiService.GenerateGodotCameraAsync(request, ct),
            GenerateCommandNames.GodotShaders  => apiService.GenerateGodotShadersAsync(request, ct),
            GenerateCommandNames.GodotSignals  => apiService.GenerateGodotSignalsAsync(request, ct),
            GenerateCommandNames.GodotNodes    => apiService.GenerateGodotNodesAsync(request, ct),
            _ => throw new InvalidOperationException($"No modality mapping for command '{command}'.")
        };

    private static ResponseEnvelope Failure(string correlationId, string errorCode, string? message) =>
        new(correlationId, false, null, errorCode, message);
}

#nullable enable
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.Services;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging;
using static GodotGenerator.Application.PreferenceKeys;

namespace GodotGenerator.Api.Services;

/// <summary>
/// Transport-agnostic API service facade implementing FastAPI-like route semantics as DLL methods.
/// </summary>
public sealed class GodotGeneratorApiService(
    RunAgentTurnUseCase runAgentTurn,
    GetPreferenceUseCase getPreference,
    SetPreferenceUseCase setPreference,
    GetApiKeysUseCase getApiKeys,
    SaveApiKeysUseCase saveApiKeys,
    GetAllConfigUseCase getAllConfig,
    IModalityTurnComposer modalityTurnComposer,
    IGodotMcpToolCatalog godotToolCatalog,
    ILogger<GodotGeneratorApiService> logger) : IGodotGeneratorApiService
{
    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateTextAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("text", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateCodeAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("code", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateImageAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("image", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAudioAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("audio", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateVideoAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("video", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateSpritesAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("sprites", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotUiAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("godot-ui", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotPhysicsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("godot-physics", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotProjectAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("godot-project", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> CreateSceneAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("scenes", request, cancellationToken);

    /// <inheritdoc />
    public async Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Preference key is required.");
        }

        var value = await getPreference.ExecuteAsync(key.Trim(), cancellationToken).ConfigureAwait(false);
        return ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["key"] = key.Trim(),
            ["value"] = value,
        });
    }

    /// <inheritdoc />
    public async Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(SetPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Preference key is required.");
        }

        await setPreference.ExecuteAsync(request.Key.Trim(), request.Value, cancellationToken).ConfigureAwait(false);
        return ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["key"] = request.Key.Trim(),
            ["value"] = request.Value,
        });
    }

    /// <inheritdoc />
    public async Task<ApiResponse<Dictionary<string, string>>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = await getApiKeys.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<Dictionary<string, string>>.Ok(new Dictionary<string, string>(keys, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<ApiResponse<Dictionary<string, IReadOnlyList<string>>>> SaveApiKeysAsync(ApiKeysRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Keys.Count == 0)
        {
            return ApiResponse<Dictionary<string, IReadOnlyList<string>>>.Fail("At least one key entry is required.");
        }

        var saved = await saveApiKeys.ExecuteAsync(request.Keys, cancellationToken).ConfigureAwait(false);
        return ApiResponse<Dictionary<string, IReadOnlyList<string>>>.Ok(new Dictionary<string, IReadOnlyList<string>>
        {
            ["saved"] = saved,
        });
    }

    /// <inheritdoc />
    public async Task<ApiResponse<Dictionary<string, object?>>> GetAllConfigAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await getAllConfig.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var toolNames = await godotToolCatalog.ListRegisteredToolNamesAsync(cancellationToken).ConfigureAwait(false);

        var providers = snapshot.ProviderRegistry.Select(MapProviderEntry).Cast<object>().ToList();
        var models = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (providerId, entries) in snapshot.ModelsByProvider)
        {
            models[providerId] = entries.Select(MapModelEntry).ToList();
        }

        var prompts = snapshot.SystemPrompts.ToDictionary(
            x => x.Key,
            x => (object?)x.Value,
            StringComparer.OrdinalIgnoreCase);

        return ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
        {
            ["preferences"] = snapshot.Preferences,
            ["keys"] = snapshot.KeyNames,
            ["providers"] = providers,
            ["models"] = models,
            ["prompts"] = prompts,
            ["defaultLlmProvider"] = snapshot.DefaultLlmProvider,
            ["defaultChatModelId"] = snapshot.DefaultChatModelId,
            ["godotToolNames"] = toolNames,
        });
    }

    private static Dictionary<string, object?> MapProviderEntry(ProviderRegistryEntry p) =>
        new(StringComparer.Ordinal)
        {
            ["id"] = p.Id,
            ["keyStoreHandle"] = p.KeyStoreHandle,
            ["endpoint"] = p.Endpoint,
            ["openAiCompatibility"] = p.OpenAiCompatibility,
            ["authenticationRequired"] = p.AuthenticationRequired,
            ["vision"] = p.Vision,
            ["streaming"] = p.Streaming,
            ["functionCalling"] = p.FunctionCalling,
            ["genericToolUse"] = p.GenericToolUse,
            ["modalities"] = p.Modalities,
        };

    private static Dictionary<string, object?> MapModelEntry(ModelRegistryEntry m) =>
        new(StringComparer.Ordinal)
        {
            ["providerId"] = m.ProviderId,
            ["friendlyName"] = m.FriendlyName,
            ["engineValue"] = m.EngineValue,
            ["modality"] = m.Modality,
        };

    private async Task<string?> GetPersistedSystemPromptOverlayAsync(string modalityKey, CancellationToken cancellationToken)
    {
        var raw = await getPreference.ExecuteAsync(PromptsSystemV1, cancellationToken).ConfigureAwait(false);
        var doc = ConfigurationRegistryService.ParseSystemPrompts(raw);
        var prefs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [PromptsTextLegacy] = await getPreference.ExecuteAsync(PromptsTextLegacy, cancellationToken).ConfigureAwait(false),
            [PromptsCodeLegacy] = await getPreference.ExecuteAsync(PromptsCodeLegacy, cancellationToken).ConfigureAwait(false),
            [PromptsGodotUiLegacy] = await getPreference.ExecuteAsync(PromptsGodotUiLegacy, cancellationToken).ConfigureAwait(false),
            [PromptsGodotPhysicsLegacy] = await getPreference.ExecuteAsync(PromptsGodotPhysicsLegacy, cancellationToken).ConfigureAwait(false),
        };
        ConfigurationRegistryService.MergeLegacyPrompts(prefs, doc);
        var key = MapModalityToPromptKey(modalityKey);
        return doc.Prompts.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
    }

    private static string MapModalityToPromptKey(string modalityKey)
    {
        var m = modalityKey.Trim().ToLowerInvariant();
        return m switch
        {
            "text" => "text",
            "code" => "code",
            "image" or "sprites" => "image",
            "audio" => "audio",
            "music" => "music",
            "video" => "video",
            "godot-ui" => "godot-ui",
            "godot-physics" => "godot-physics",
            "godot-project" => "godot-project",
            "scenes" => "scenes",
            _ => "text",
        };
    }

    private static string? CombineSystemPrompts(string? persistedOverlay, string? requestPrompt)
    {
        if (string.IsNullOrWhiteSpace(persistedOverlay))
        {
            return requestPrompt;
        }

        if (string.IsNullOrWhiteSpace(requestPrompt))
        {
            return persistedOverlay;
        }

        return persistedOverlay.Trim() + Environment.NewLine + Environment.NewLine + requestPrompt.Trim();
    }

    private async Task<ApiResponse<Dictionary<string, object?>>> GenerateByModalityAsync(
        string modality,
        GenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ApiResponse<Dictionary<string, object?>>.Fail("Prompt cannot be empty.");
        }

        try
        {
            var (providerPreferenceKey, modelPreferenceKey) = EffectiveSelectionPolicy.GetPreferenceKeys(modality);
            var preferredProvider = await getPreference.ExecuteAsync(providerPreferenceKey, cancellationToken).ConfigureAwait(false);
            var preferredModelId = await getPreference.ExecuteAsync(modelPreferenceKey, cancellationToken).ConfigureAwait(false);
            var effective = EffectiveSelectionPolicy.Resolve(
                modality,
                request.Provider,
                request.PreferredModelId,
                panelLanguageOverride: null,
                globalPreferredLanguage: null,
                preferences: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [providerPreferenceKey] = preferredProvider,
                    [modelPreferenceKey] = preferredModelId,
                },
                hostDefaultProvider: null,
                hostDefaultModelId: null);

            var overlay = await GetPersistedSystemPromptOverlayAsync(modality, cancellationToken).ConfigureAwait(false);
            var mergedSystem = CombineSystemPrompts(overlay, request.SystemPrompt);

            var turnRequest = modalityTurnComposer.Compose(
                modality,
                request.Prompt,
                mergedSystem,
                request.ProjectName,
                effective.ModelId,
                request.Options) with { Provider = effective.Provider };

            var turn = await runAgentTurn.ExecuteAsync(turnRequest, cancellationToken).ConfigureAwait(false);
            if (!turn.Success)
            {
                return ApiResponse<Dictionary<string, object?>>.Fail(turn.Message);
            }

            return ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["modality"] = modality,
                ["provider"] = effective.Provider,
                ["modelId"] = effective.ModelId,
                ["projectName"] = request.ProjectName,
                ["message"] = turn.Message,
                ["detail"] = turn.Detail,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Generation failed for modality {Modality}", modality);
            return ApiResponse<Dictionary<string, object?>>.Fail(ex.Message);
        }
    }
}

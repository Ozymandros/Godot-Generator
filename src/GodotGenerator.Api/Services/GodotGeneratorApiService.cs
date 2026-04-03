#nullable enable
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging;

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
        GenerateByModalityAsync("text", "preferred_llm_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateCodeAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("code", "preferred_llm_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateImageAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("image", "preferred_image_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAudioAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("audio", "preferred_audio_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateVideoAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("video", "preferred_video_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateSpritesAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("sprites", "preferred_image_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotUiAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("godot-ui", "preferred_llm_provider", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotPhysicsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        GenerateByModalityAsync("godot-physics", "preferred_llm_provider", request, cancellationToken);

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
        var providers = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["name"] = snapshot.DefaultLlmProvider,
                ["defaultChatModelId"] = snapshot.DefaultChatModelId,
            },
        };

        var models = new Dictionary<string, object?>
        {
            [snapshot.DefaultLlmProvider] = new[] { snapshot.DefaultChatModelId },
        };

        return ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
        {
            ["preferences"] = snapshot.Preferences,
            ["keys"] = snapshot.KeyNames,
            ["providers"] = providers,
            ["models"] = models,
            ["prompts"] = new Dictionary<string, object?>(),
            ["godotToolNames"] = toolNames,
        });
    }

    private async Task<ApiResponse<Dictionary<string, object?>>> GenerateByModalityAsync(
        string modality,
        string providerPreferenceKey,
        GenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ApiResponse<Dictionary<string, object?>>.Fail("Prompt cannot be empty.");
        }

        try
        {
            var provider = request.Provider;
            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = await getPreference.ExecuteAsync(providerPreferenceKey, cancellationToken).ConfigureAwait(false);
            }

            var turnRequest = modalityTurnComposer.Compose(
                modality,
                request.Prompt,
                request.SystemPrompt,
                request.ProjectName,
                request.PreferredModelId,
                request.Options);

            var turn = await runAgentTurn.ExecuteAsync(turnRequest, cancellationToken).ConfigureAwait(false);
            if (!turn.Success)
            {
                return ApiResponse<Dictionary<string, object?>>.Fail(turn.Message);
            }

            return ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["modality"] = modality,
                ["provider"] = provider,
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

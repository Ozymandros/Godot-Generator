#nullable enable
using System.IO;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Services;
using static GodotGenerator.Application.PreferenceKeys;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Application use case that aggregates backend configuration state for discovery scenarios.
/// </summary>
public sealed class GetAllConfigUseCase(
    GetApiKeysUseCase getApiKeys,
    GetPreferenceUseCase getPreference,
    ILlmDiscoveryInfoProvider llmDiscovery)
{
    private const string AgentDebugLogPath = @"C:\Projects\Godot-Generator-Avalonia\debug-cb9046.log";
    /// <summary>
    /// Builds a configuration snapshot for API-style discovery calls.
    /// </summary>
    public async Task<AllConfigSnapshot> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var keyNames = await getApiKeys.GetKeyNamesAsync(cancellationToken).ConfigureAwait(false);

        var preferenceKeys = new[]
        {
            PreferredLlmProvider,
            PreferredImageProvider,
            PreferredAudioProvider,
            PreferredVideoProvider,
            PreferredLlmModel,
            PreferredImageModel,
            PreferredAudioModel,
            PreferredVideoModel,
            PreferredLanguage,
            ProvidersRegistryV1,
            ModelsRegistryV1,
            PromptsSystemV1,
            AppBackendUrl,
            AppOutputBasePath,
            AppWorkspaceNotes,
            PromptsTextLegacy,
            PromptsCodeLegacy,
            PromptsGodotUiLegacy,
            PromptsGodotPhysicsLegacy,
        };

        var preferences = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in preferenceKeys)
        {
            preferences[key] = await getPreference.ExecuteAsync(key, cancellationToken).ConfigureAwait(false);
        }
        #region agent log
        try
        {
            var providersRaw = preferences.GetValueOrDefault(ProvidersRegistryV1);
            var line = System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = "cb9046",
                runId = "initial",
                hypothesisId = "H2_H3",
                location = "GetAllConfigUseCase.cs:ExecuteAsync",
                message = "Loaded preference snapshot",
                data = new
                {
                    providersRegistryLength = providersRaw?.Length ?? 0,
                    hasProvidersRegistry = !string.IsNullOrWhiteSpace(providersRaw),
                    modelsRegistryLength = preferences.GetValueOrDefault(ModelsRegistryV1)?.Length ?? 0,
                    promptsRegistryLength = preferences.GetValueOrDefault(PromptsSystemV1)?.Length ?? 0
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

        if (string.IsNullOrWhiteSpace(preferences.GetValueOrDefault(PreferredLanguage)))
        {
            var legacyLocale = await getPreference.ExecuteAsync(PreferredLocaleLegacy, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(legacyLocale))
            {
                preferences[PreferredLanguage] = legacyLocale;
            }
        }

        var providerDoc = ConfigurationRegistryService.ParseProviderRegistry(
            preferences.GetValueOrDefault(ProvidersRegistryV1));
        var modelDoc = ConfigurationRegistryService.ParseModelRegistry(
            preferences.GetValueOrDefault(ModelsRegistryV1));
        var promptsDoc = ConfigurationRegistryService.ParseSystemPrompts(
            preferences.GetValueOrDefault(PromptsSystemV1));
        ConfigurationRegistryService.MergeLegacyPrompts(preferences, promptsDoc);

        var modelsByProvider = ConfigurationRegistryService.GroupModelsByProvider(modelDoc.Models);

        var systemPrompts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in promptsDoc.Prompts)
        {
            systemPrompts[k] = v;
        }

        var (provider, modelId) = llmDiscovery.GetDefaultChatModel();
        return new AllConfigSnapshot(
            preferences,
            keyNames,
            provider,
            modelId,
            providerDoc.Providers.ToList().AsReadOnly(),
            modelsByProvider,
            systemPrompts);
    }
}

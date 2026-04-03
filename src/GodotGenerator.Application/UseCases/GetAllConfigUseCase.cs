#nullable enable
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

#nullable enable
using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;

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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated configuration snapshot.</returns>
    public async Task<AllConfigSnapshot> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var keyNames = await getApiKeys.GetKeyNamesAsync(cancellationToken).ConfigureAwait(false);

        var preferenceKeys = new[]
        {
            PreferenceKeys.PreferredLlmProvider,
            PreferenceKeys.PreferredImageProvider,
            PreferenceKeys.PreferredAudioProvider,
            PreferenceKeys.PreferredVideoProvider,
            PreferenceKeys.PreferredLlmModel,
            PreferenceKeys.PreferredImageModel,
            PreferenceKeys.PreferredAudioModel,
            PreferenceKeys.PreferredVideoModel,
            PreferenceKeys.PreferredLanguage,
        };

        var preferences = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in preferenceKeys)
        {
            preferences[key] = await getPreference.ExecuteAsync(key, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(preferences.GetValueOrDefault(PreferenceKeys.PreferredLanguage)))
        {
            var legacyLocale = await getPreference.ExecuteAsync(PreferenceKeys.PreferredLocaleLegacy, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(legacyLocale))
            {
                preferences[PreferenceKeys.PreferredLanguage] = legacyLocale;
            }
        }

        var (provider, modelId) = llmDiscovery.GetDefaultChatModel();
        return new AllConfigSnapshot(preferences, keyNames, provider, modelId);
    }
}

/// <summary>
/// Aggregated backend configuration snapshot.
/// </summary>
/// <param name="Preferences">Configured preference values.</param>
/// <param name="KeyNames">Configured API key service names.</param>
/// <param name="DefaultLlmProvider">Default LLM provider label from host configuration.</param>
/// <param name="DefaultChatModelId">Default chat model id from host configuration.</param>
public sealed record AllConfigSnapshot(
    IReadOnlyDictionary<string, string?> Preferences,
    IReadOnlyList<string> KeyNames,
    string DefaultLlmProvider,
    string DefaultChatModelId);

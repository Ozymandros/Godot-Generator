#nullable enable
using GodotGenerator.Application;

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Single source of truth for modality preference key mapping and effective selection precedence.
/// </summary>
public static class EffectiveSelectionPolicy
{
    /// <summary>
    /// Resolves preference keys for provider/model by modality.
    /// </summary>
    public static (string ProviderPreferenceKey, string ModelPreferenceKey) GetPreferenceKeys(string modality)
    {
        var normalized = NormalizeModality(modality);
        return normalized switch
        {
            "image" or "sprites" => (PreferenceKeys.PreferredImageProvider, PreferenceKeys.PreferredImageModel),
            "audio" => (PreferenceKeys.PreferredAudioProvider, PreferenceKeys.PreferredAudioModel),
            "video" => (PreferenceKeys.PreferredVideoProvider, PreferenceKeys.PreferredVideoModel),
            _ => (PreferenceKeys.PreferredLlmProvider, PreferenceKeys.PreferredLlmModel),
        };
    }

    /// <summary>
    /// Resolves effective provider/model/language using deterministic precedence.
    /// </summary>
    public static EffectiveGenerationSettings Resolve(
        string modality,
        string? requestProvider,
        string? requestModelId,
        string? panelLanguageOverride,
        string? globalPreferredLanguage,
        IReadOnlyDictionary<string, string?>? preferences,
        string? hostDefaultProvider,
        string? hostDefaultModelId)
    {
        var (providerKey, modelKey) = GetPreferenceKeys(modality);
        var provider = FirstNonEmpty(
            requestProvider,
            GetPreferenceValue(preferences, providerKey),
            hostDefaultProvider);
        var modelId = FirstNonEmpty(
            requestModelId,
            GetPreferenceValue(preferences, modelKey),
            hostDefaultModelId);
        var language = FirstNonEmpty(panelLanguageOverride, globalPreferredLanguage);
        return new EffectiveGenerationSettings(
            Modality: NormalizeModality(modality),
            Provider: provider,
            ModelId: modelId,
            PreferredLanguage: language);
    }

    /// <summary>
    /// Normalizes modality keys used across UI/API boundaries.
    /// </summary>
    public static string NormalizeModality(string modality)
    {
        var trimmed = string.IsNullOrWhiteSpace(modality) ? string.Empty : modality.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "godotui" => "godot-ui",
            "godotphysics" => "godot-physics",
            _ => trimmed,
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? GetPreferenceValue(IReadOnlyDictionary<string, string?>? preferences, string key)
    {
        if (preferences is null)
        {
            return null;
        }

        return preferences.TryGetValue(key, out var value) ? value : null;
    }
}


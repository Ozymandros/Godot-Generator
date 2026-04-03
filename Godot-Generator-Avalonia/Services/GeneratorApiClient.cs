#nullable enable
using System.Collections;
using Godot_Generator_Avalonia.Models;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application;
using Microsoft.Extensions.Logging;

namespace Godot_Generator_Avalonia.Services;

/// <summary>
/// Adapts <see cref="IGodotGeneratorApiService"/> for Avalonia (same semantics as the Blazor client).
/// </summary>
public sealed class GeneratorApiClient(
    IGodotGeneratorApiService api,
    ILogger<GeneratorApiClient> logger) : IGeneratorApiClient
{
    /// <inheritdoc />
    public async Task<(bool Success, string Message, string? Error)> GenerateAsync(
        GenerationModality modality,
        string prompt,
        string? preferredLanguageOverride,
        string? globalPreferredLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, string.Empty, "Prompt cannot be empty.");
        }

        var (provider, preferredModelId) = await ResolveEffectiveProviderAndModelAsync(modality, cancellationToken).ConfigureAwait(false);
        var effectiveLanguage = ResolvePreferredLanguage(preferredLanguageOverride, globalPreferredLanguage);
        var request = new GenerateRequest(
            Prompt: prompt.Trim(),
            Provider: provider,
            PreferredModelId: preferredModelId,
            Options: BuildOptions(effectiveLanguage));

        ApiResponse<Dictionary<string, object?>> result = modality switch
        {
            GenerationModality.Text => await api.GenerateTextAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Code => await api.GenerateCodeAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Image => await api.GenerateImageAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Audio => await api.GenerateAudioAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Video => await api.GenerateVideoAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Sprites => await api.GenerateSpritesAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.GodotUi => await api.GenerateGodotUiAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.GodotPhysics => await api.GenerateGodotPhysicsAsync(request, cancellationToken).ConfigureAwait(false),
            _ => ApiResponse<Dictionary<string, object?>>.Fail("Unsupported modality."),
        };

        if (!result.Success)
        {
            return (false, string.Empty, result.Error ?? "Generation failed.");
        }

        var message = ExtractMessage(result.Data);
        logger.LogDebug("Generation succeeded for {Modality}", modality);
        return (true, message, null);
    }

    /// <inheritdoc />
    public async Task<string> GetGlobalPreferredLanguageAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.GetPreferenceAsync(LanguageCatalog.PreferredLanguagePreferenceKey, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null)
        {
            return string.Empty;
        }

        return result.Data.TryGetValue("value", out var value) ? value ?? string.Empty : string.Empty;
    }

    /// <inheritdoc />
    public async Task<bool> SaveGlobalPreferredLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var saveResult = await api
            .SetPreferenceAsync(new SetPreferenceRequest(LanguageCatalog.PreferredLanguagePreferenceKey, language), cancellationToken)
            .ConfigureAwait(false);
        return saveResult.Success;
    }

    /// <inheritdoc />
    public async Task<SettingsSnapshot?> GetAllConfigSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.GetAllConfigAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null)
        {
            return null;
        }

        return ParseSettingsSnapshot(result.Data);
    }

    /// <inheritdoc />
    public async Task<bool> SetPreferenceAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var saveResult = await api.SetPreferenceAsync(new SetPreferenceRequest(key.Trim(), value), cancellationToken).ConfigureAwait(false);
        return saveResult.Success;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>?> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.GetApiKeysAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null)
        {
            return null;
        }

        return result.Data;
    }

    /// <inheritdoc />
    public async Task<bool> SaveApiKeysAsync(IReadOnlyDictionary<string, string?> keys, CancellationToken cancellationToken = default)
    {
        var saveResult = await api.SaveApiKeysAsync(new ApiKeysRequest(keys), cancellationToken).ConfigureAwait(false);
        return saveResult.Success;
    }

    /// <summary>Panel override wins over global default.</summary>
    public static string ResolvePreferredLanguage(string? preferredLanguageOverride, string? globalPreferredLanguage)
    {
        if (!string.IsNullOrWhiteSpace(preferredLanguageOverride))
        {
            return preferredLanguageOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(globalPreferredLanguage) ? string.Empty : globalPreferredLanguage.Trim();
    }

    private static IReadOnlyDictionary<string, object?> BuildOptions(string effectiveLanguage)
    {
        if (string.IsNullOrWhiteSpace(effectiveLanguage))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["preferred_language"] = effectiveLanguage,
        };
    }

    private static string ExtractMessage(IReadOnlyDictionary<string, object?>? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        return data.TryGetValue("message", out var messageObj) && messageObj is string message
            ? message
            : string.Empty;
    }

    private async Task<(string? Provider, string? PreferredModelId)> ResolveEffectiveProviderAndModelAsync(
        GenerationModality modality,
        CancellationToken cancellationToken)
    {
        var (providerKey, modelKey) = modality switch
        {
            GenerationModality.Image or GenerationModality.Sprites =>
                (PreferenceKeys.PreferredImageProvider, PreferenceKeys.PreferredImageModel),
            GenerationModality.Audio =>
                (PreferenceKeys.PreferredAudioProvider, PreferenceKeys.PreferredAudioModel),
            GenerationModality.Video =>
                (PreferenceKeys.PreferredVideoProvider, PreferenceKeys.PreferredVideoModel),
            _ =>
                (PreferenceKeys.PreferredLlmProvider, PreferenceKeys.PreferredLlmModel),
        };

        var provider = await ReadPreferenceValueAsync(providerKey, cancellationToken).ConfigureAwait(false);
        var modelId = await ReadPreferenceValueAsync(modelKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(provider) || !string.IsNullOrWhiteSpace(modelId))
        {
            return (NullIfWhitespace(provider), NullIfWhitespace(modelId));
        }

        var snapshot = await GetAllConfigSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return (
            NullIfWhitespace(snapshot?.DefaultLlmProvider),
            NullIfWhitespace(snapshot?.DefaultChatModelId));
    }

    private async Task<string?> ReadPreferenceValueAsync(string key, CancellationToken cancellationToken)
    {
        var result = await api.GetPreferenceAsync(key, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null)
        {
            return null;
        }

        return result.Data.TryGetValue("value", out var value) ? value : null;
    }

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SettingsSnapshot ParseSettingsSnapshot(Dictionary<string, object?> data)
    {
        var prefs = ParsePreferences(data.GetValueOrDefault("preferences"));
        var keyNames = ParseStringList(data.GetValueOrDefault("keys"));
        var (defProvider, defModel) = ParseDefaultProviderModel(data.GetValueOrDefault("providers"));
        var tools = ParseStringList(data.GetValueOrDefault("godotToolNames"));
        return new SettingsSnapshot(prefs, keyNames, defProvider, defModel, tools);
    }

    private static IReadOnlyDictionary<string, string?> ParsePreferences(object? o)
    {
        if (o is IReadOnlyDictionary<string, string?> ro)
        {
            return ro;
        }

        if (o is Dictionary<string, string?> d)
        {
            return d;
        }

        if (o is IDictionary<string, object?> dod)
        {
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in dod)
            {
                map[k] = v switch
                {
                    null => null,
                    string s => s,
                    _ => v.ToString(),
                };
            }

            return map;
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseStringList(object? o)
    {
        if (o is IReadOnlyList<string> list)
        {
            return list;
        }

        if (o is List<string> list2)
        {
            return list2;
        }

        if (o is IEnumerable enumerable and not string)
        {
            var acc = new List<string>();
            foreach (var item in enumerable)
            {
                if (item is string s)
                {
                    acc.Add(s);
                }
                else if (item is not null)
                {
                    acc.Add(item.ToString() ?? string.Empty);
                }
            }

            return acc;
        }

        return Array.Empty<string>();
    }

    private static (string Provider, string ModelId) ParseDefaultProviderModel(object? o)
    {
        if (o is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                if (item is IReadOnlyDictionary<string, object?> dict)
                {
                    return (
                        dict.GetValueOrDefault("name")?.ToString() ?? string.Empty,
                        dict.GetValueOrDefault("defaultChatModelId")?.ToString() ?? string.Empty);
                }

                if (item is Dictionary<string, object?> d2)
                {
                    return (
                        d2.GetValueOrDefault("name")?.ToString() ?? string.Empty,
                        d2.GetValueOrDefault("defaultChatModelId")?.ToString() ?? string.Empty);
                }
            }
        }

        return (string.Empty, string.Empty);
    }
}

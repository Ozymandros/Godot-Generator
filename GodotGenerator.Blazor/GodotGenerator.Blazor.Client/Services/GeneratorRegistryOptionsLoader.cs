#nullable enable

using System.Text.Json;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Desktop.Contracts.Serialization;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Extracts provider ids and per-provider model ids from the same config envelope
/// used by <c>Settings</c> (providers + models registries).
/// </summary>
internal static class GeneratorRegistryOptionsLoader
{
    private static readonly JsonSerializerOptions ProviderJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static void Fill(
        Dictionary<string, object?> data,
        List<string> providerIds,
        Dictionary<string, List<string>> modelsByProvider,
        GenerationModality panelModality)
    {
        providerIds.Clear();
        modelsByProvider.Clear();

        var allowedTags = GenerationModalityRegistryTags.GetAllowedRegistryTags(panelModality);

        if (!TryGetJsonElement(data.GetValueOrDefault("providers"), out var providersEl) ||
            providersEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var allowedProviderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in providersEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<ProviderRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            if (!ProviderModalitiesIntersectAllowed(entry.Modalities, allowedTags))
            {
                continue;
            }

            var id = entry.Id.Trim();
            providerIds.Add(id);
            allowedProviderIds.Add(id);
        }

        if (providerIds.Count == 0)
        {
            // Keep dropdowns usable when provider rows have missing/unknown modality tags.
            foreach (var item in providersEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<ProviderRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                var id = entry.Id.Trim();
                if (id.Length == 0 || allowedProviderIds.Contains(id))
                {
                    continue;
                }

                providerIds.Add(id);
                allowedProviderIds.Add(id);
            }
        }

        providerIds.Sort(StringComparer.OrdinalIgnoreCase);

        if (!TryGetJsonElement(data.GetValueOrDefault("models"), out var modelsRoot) ||
            modelsRoot.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var scratch = new List<ModelRegistryEntry>();
        foreach (var prop in modelsRoot.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<ModelRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.EngineValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.ProviderId))
                {
                    entry.ProviderId = prop.Name;
                }

                var pid = entry.ProviderId.Trim();
                if (string.IsNullOrEmpty(pid) || !allowedProviderIds.Contains(pid))
                {
                    continue;
                }

                if (!ModelModalityAllowed(entry.Modality, allowedTags))
                {
                    continue;
                }

                scratch.Add(entry);
            }
        }

        foreach (var m in scratch)
        {
            var pid = m.ProviderId.Trim();
            if (string.IsNullOrEmpty(pid))
            {
                continue;
            }

            if (!modelsByProvider.TryGetValue(pid, out var list))
            {
                list = [];
                modelsByProvider[pid] = list;
            }

            list.Add(m.EngineValue.Trim());
        }

        foreach (var key in modelsByProvider.Keys.ToList())
        {
            modelsByProvider[key] = modelsByProvider[key]
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// All provider ids present in the registry (Settings parity — not modality-filtered).
    /// Used so saved default provider preferences can match even when modality tags exclude a row from <see cref="Fill"/>.
    /// </summary>
    internal static List<string> GetAllRegisteredProviderIds(Dictionary<string, object?> data)
    {
        var list = new List<string>();
        if (!TryGetJsonElement(data.GetValueOrDefault("providers"), out var providersEl) ||
            providersEl.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in providersEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<ProviderRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            list.Add(entry.Id.Trim());
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>
    /// Resolves <paramref name="candidate"/> to the registry id casing when it appears in <c>providers</c>.
    /// </summary>
    internal static bool TryGetCanonicalProviderId(
        Dictionary<string, object?> data,
        string candidate,
        out string? canonicalId)
    {
        canonicalId = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (!TryGetJsonElement(data.GetValueOrDefault("providers"), out var providersEl) ||
            providersEl.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in providersEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<ProviderRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            var id = entry.Id.Trim();
            if (string.Equals(id, candidate.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                canonicalId = id;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends modality-filtered model engine values for a single provider into <paramref name="modelsByProvider"/>.
    /// </summary>
    internal static void AppendFilteredModelsForProvider(
        Dictionary<string, object?> data,
        string canonicalProviderId,
        HashSet<string> allowedTags,
        Dictionary<string, List<string>> modelsByProvider)
    {
        if (!TryGetJsonElement(data.GetValueOrDefault("models"), out var modelsRoot) ||
            modelsRoot.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in modelsRoot.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<ModelRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.EngineValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.ProviderId))
                {
                    entry.ProviderId = prop.Name;
                }

                var pid = entry.ProviderId.Trim();
                if (!string.Equals(pid, canonicalProviderId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ModelModalityAllowed(entry.Modality, allowedTags))
                {
                    continue;
                }

                if (!modelsByProvider.TryGetValue(canonicalProviderId, out var list))
                {
                    list = [];
                    modelsByProvider[canonicalProviderId] = list;
                }

                list.Add(entry.EngineValue.Trim());
            }
        }

        if (modelsByProvider.TryGetValue(canonicalProviderId, out var rows))
        {
            modelsByProvider[canonicalProviderId] = rows
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// If the saved default model exists in the full registry for this provider but was omitted by
    /// modality filtering in <see cref="Fill"/>, add its <c>engineValue</c> so the dropdown matches
    /// Configuration → General (which lists all models per provider).
    /// </summary>
    internal static void EnsureSavedModelEngineListedForProvider(
        Dictionary<string, object?> data,
        string canonicalProviderId,
        string? preferredEngine,
        Dictionary<string, List<string>> modelsByProvider)
    {
        if (string.IsNullOrWhiteSpace(preferredEngine) || string.IsNullOrWhiteSpace(canonicalProviderId))
        {
            return;
        }

        var want = preferredEngine.Trim();
        if (modelsByProvider.TryGetValue(canonicalProviderId, out var existing))
        {
            foreach (var e in existing)
            {
                if (string.Equals(e, want, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        if (!TryGetJsonElement(data.GetValueOrDefault("models"), out var modelsRoot) ||
            modelsRoot.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in modelsRoot.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<ModelRegistryEntry>(item.GetRawText(), ProviderJsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.EngineValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.ProviderId))
                {
                    entry.ProviderId = prop.Name;
                }

                var pid = entry.ProviderId.Trim();
                if (!string.Equals(pid, canonicalProviderId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(entry.EngineValue.Trim(), want, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!modelsByProvider.TryGetValue(canonicalProviderId, out var list))
                {
                    list = [];
                    modelsByProvider[canonicalProviderId] = list;
                }

                list.Add(entry.EngineValue.Trim());
                modelsByProvider[canonicalProviderId] = list
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return;
            }
        }
    }

    private static bool ProviderModalitiesIntersectAllowed(IReadOnlyList<string>? modalities, HashSet<string> allowedTags)
    {
        if (modalities is null || modalities.Count == 0)
        {
            return false;
        }

        foreach (var m in modalities)
        {
            var t = m?.Trim();
            if (string.IsNullOrEmpty(t))
            {
                continue;
            }

            if (allowedTags.Contains(t))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ModelModalityAllowed(string modality, HashSet<string> allowedTags)
    {
        var t = modality.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return false;
        }

        return allowedTags.Contains(t);
    }

    /// <summary>Parses the <c>preferences</c> object from a config envelope (same shape as Settings).</summary>
    internal static Dictionary<string, string?> ParsePreferences(Dictionary<string, object?> data)
    {
        var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetJsonElement(data.GetValueOrDefault("preferences"), out var je) || je.ValueKind != JsonValueKind.Object)
        {
            return d;
        }

        foreach (var p in je.EnumerateObject())
        {
            d[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => p.Value.GetString(),
                _ => p.Value.ToString(),
            };
        }

        return d;
    }

    private static bool TryGetJsonElement(object? raw, out JsonElement element)
    {
        if (raw is JsonElement je)
        {
            element = je;
            return true;
        }

        if (raw is null)
        {
            element = default;
            return false;
        }

        try
        {
            element = JsonSerializer.SerializeToElement(raw, ContractJsonOptions.Default);
            return true;
        }
        catch
        {
            element = default;
            return false;
        }
    }
}

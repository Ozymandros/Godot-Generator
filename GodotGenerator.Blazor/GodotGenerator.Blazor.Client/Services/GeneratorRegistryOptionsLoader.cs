#nullable enable

using System.Text.Json;
using GodotGenerator.Application.Configuration;
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
        Dictionary<string, List<string>> modelsByProvider)
    {
        providerIds.Clear();
        modelsByProvider.Clear();

        if (!TryGetJsonElement(data.GetValueOrDefault("providers"), out var providersEl) ||
            providersEl.ValueKind != JsonValueKind.Array)
        {
            return;
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

            providerIds.Add(entry.Id.Trim());
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

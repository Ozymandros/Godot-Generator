#nullable enable
using System.Text.Json;
using GodotGenerator.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Application use case for saving API keys into preference-backed storage.
/// </summary>
public sealed class SaveApiKeysUseCase(
    IPreferenceRepository preferences,
    ILogger<SaveApiKeysUseCase> logger)
{
    private const string KeyIndexName = "__api_keys_index";
    private const string KeyPrefix = "api.keys.";

    /// <summary>
    /// Saves a batch of API keys by service name.
    /// </summary>
    /// <param name="keys">Service-to-key mappings. Null or empty value removes a key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Saved service names.</returns>
    public async Task<IReadOnlyList<string>> ExecuteAsync(
        IReadOnlyDictionary<string, string?> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var index = await ReadIndexSetAsync(cancellationToken).ConfigureAwait(false);
        var saved = new List<string>();

        foreach (var (serviceRaw, keyValue) in keys)
        {
            var service = serviceRaw?.Trim();
            if (string.IsNullOrWhiteSpace(service))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(keyValue))
            {
                await preferences.SetAsync(KeyPrefix + service, null, cancellationToken).ConfigureAwait(false);
                index.Remove(service);
                continue;
            }

            await preferences.SetAsync(KeyPrefix + service, keyValue.Trim(), cancellationToken).ConfigureAwait(false);
            index.Add(service);
            saved.Add(service);
        }

        var indexPayload = JsonSerializer.Serialize(index.Order(StringComparer.OrdinalIgnoreCase));
        await preferences.SetAsync(KeyIndexName, indexPayload, cancellationToken).ConfigureAwait(false);

        logger.LogDebug("SaveApiKeys: saved {Count} key(s)", saved.Count);
        return saved;
    }

    /// <summary>
    /// Reads the API key index set from preferences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>API key index set.</returns>
    private async Task<HashSet<string>> ReadIndexSetAsync(CancellationToken cancellationToken)
    {
        var indexRaw = await preferences.GetAsync(KeyIndexName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(indexRaw))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(indexRaw) ?? [];
            return list
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "API key index is invalid JSON. Resetting index.");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

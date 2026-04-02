#nullable enable
using System.Text.Json;
using GodotGenerator.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Application use case for reading configured API keys from preference-backed storage.
/// </summary>
public sealed class GetApiKeysUseCase(
    IPreferenceRepository preferences,
    ILogger<GetApiKeysUseCase> logger)
{
    private const string KeyIndexName = "__api_keys_index";
    private const string KeyPrefix = "api.keys.";

    /// <summary>
    /// Gets all configured API keys keyed by service name.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of service name to key value.</returns>
    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var names = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var value = await preferences.GetAsync(KeyPrefix + name, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[name] = value;
            }
        }

        logger.LogDebug("GetApiKeys: returned {Count} key(s)", result.Count);
        return result;
    }

    /// <summary>
    /// Gets only API key service names without exposing secret values.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configured API key service names.</returns>
    public async Task<IReadOnlyList<string>> GetKeyNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
        return names.AsReadOnly();
    }

    private async Task<List<string>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        var indexRaw = await preferences.GetAsync(KeyIndexName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(indexRaw))
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(indexRaw);
            return list?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "API key index is invalid JSON. Falling back to empty index.");
            return [];
        }
    }
}

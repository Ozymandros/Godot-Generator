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
    /// Decrypted values are also injected into the current process environment so that
    /// third-party SDKs that read <c>Environment.GetEnvironmentVariable</c> directly
    /// (e.g. the OpenAI .NET SDK) pick them up without additional configuration.
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
                InjectIntoEnvironment(name, value);
            }
        }

        logger.LogDebug("GetApiKeys: returned {Count} key(s)", result.Count);
        return result;
    }

    /// <summary>
    /// Sets the process environment variable for a provider API key using the
    /// conventional <c>{PROVIDER_UPPER}_API_KEY</c> naming pattern.
    /// </summary>
    /// <param name="serviceName">Provider/service name (e.g. <c>openai</c>).</param>
    /// <param name="keyValue">Decrypted API key value.</param>
    private void InjectIntoEnvironment(string serviceName, string keyValue)
    {
        try
        {
            var envVarName = serviceName.ToUpperInvariant().Replace('-', '_').Replace(' ', '_') + "_API_KEY";
            Environment.SetEnvironmentVariable(envVarName, keyValue, EnvironmentVariableTarget.Process);
            logger.LogDebug("Injected API key for '{Service}' into environment as {EnvVar}.", serviceName, envVarName);
        }
        catch (Exception ex)
        {
            // Non-fatal: environment injection is best-effort; the primary resolution path
            // uses IPreferenceRepository and does not depend on environment variables.
            logger.LogWarning(ex, "Failed to inject API key for '{Service}' into process environment.", serviceName);
        }
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

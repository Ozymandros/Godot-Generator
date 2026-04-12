#nullable enable
using GodotGenerator.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Reads provider-scoped API keys from preferences (api.keys.&lt;provider&gt;).
/// </summary>
public sealed class PreferenceProviderSecretResolver(
    IPreferenceRepository preferences,
    ILogger<PreferenceProviderSecretResolver> logger) : IProviderSecretResolver
{
    private const string KeyPrefix = "api.keys.";

    /// <inheritdoc />
    public async Task<string?> ResolveApiKeyAsync(string provider, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        var normalized = provider.Trim();
        var key = await preferences.GetAsync(KeyPrefix + normalized, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogDebug("No API key configured for provider {Provider}.", normalized);
            return null;
        }

        return key.Trim();
    }
}


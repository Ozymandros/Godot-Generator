#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Resolves provider-scoped API keys from local secret storage.
/// </summary>
public interface IProviderSecretResolver
{
    /// <summary>
    /// Resolves the API key for a provider/service name.
    /// </summary>
    Task<string?> ResolveApiKeyAsync(string provider, CancellationToken cancellationToken = default);
}


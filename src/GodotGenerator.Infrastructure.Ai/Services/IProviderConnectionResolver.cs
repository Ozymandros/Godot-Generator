#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Resolves effective provider connectivity settings (provider, model, key, endpoint)
/// for OpenAI-compatible Semantic Kernel chat completion.
/// </summary>
public interface IProviderConnectionResolver
{
    /// <summary>
    /// Resolves effective provider connection settings from explicit overrides and user-stored preferences.
    /// </summary>
    /// <param name="provider">Optional provider override.</param>
    /// <param name="preferredModelId">Optional model id override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved connection settings ready for kernel creation.</returns>
    Task<ProviderConnectionSettings> ResolveAsync(
        string? provider,
        string? preferredModelId,
        CancellationToken cancellationToken = default);
}

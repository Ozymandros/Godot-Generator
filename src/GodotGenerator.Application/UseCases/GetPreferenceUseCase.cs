#nullable enable
using GodotGenerator.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Application use case for reading a preference value by key.
/// </summary>
public sealed class GetPreferenceUseCase(
    IPreferenceRepository preferences,
    ILogger<GetPreferenceUseCase> logger)
{
    /// <summary>
    /// Gets a preference value by key.
    /// </summary>
    /// <param name="key">Preference key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preference value, or <see langword="null"/> when missing.</returns>
    public async Task<string?> ExecuteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        logger.LogDebug("GetPreference: {Key}", key);
        return await preferences.GetAsync(key, cancellationToken).ConfigureAwait(false);
    }
}

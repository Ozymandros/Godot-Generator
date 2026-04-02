#nullable enable
using GodotGenerator.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Application use case for setting a preference value by key.
/// </summary>
public sealed class SetPreferenceUseCase(
    IPreferenceRepository preferences,
    ILogger<SetPreferenceUseCase> logger)
{
    /// <summary>
    /// Sets a preference value by key.
    /// </summary>
    /// <param name="key">Preference key.</param>
    /// <param name="value">Preference value. Null removes the key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExecuteAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        logger.LogDebug("SetPreference: {Key}", key);
        await preferences.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
    }
}

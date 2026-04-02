#nullable enable

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Port for reading and writing user preferences backed by JSON file storage.
/// </summary>
public interface IPreferenceRepository
{
    /// <summary>
    /// Gets a preference value by key, or null if missing.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a preference value (null removes the key if supported).
    /// </summary>
    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}

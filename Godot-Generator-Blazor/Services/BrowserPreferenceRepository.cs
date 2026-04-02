using GodotGenerator.Application.Abstractions;

namespace Godot_Generator_Blazor.Services;

/// <summary>
/// In-browser preference repository for direct-WASM integration mode.
/// </summary>
public sealed class BrowserPreferenceRepository : IPreferenceRepository
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }

        return Task.CompletedTask;
    }
}

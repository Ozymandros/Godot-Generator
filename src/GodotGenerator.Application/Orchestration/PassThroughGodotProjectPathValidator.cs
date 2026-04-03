#nullable enable
using GodotGenerator.Application.Abstractions;

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Always reports valid when the Godot plugin stack is not available (e.g. WASM stub host).
/// </summary>
public sealed class PassThroughGodotProjectPathValidator : IGodotProjectPathValidator
{
    /// <inheritdoc />
    public Task<bool> IsValidGodotProjectRootAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

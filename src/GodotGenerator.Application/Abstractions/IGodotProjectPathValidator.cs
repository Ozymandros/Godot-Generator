#nullable enable

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Validates whether a directory path looks like a Godot project root (e.g. project.godot present).
/// </summary>
public interface IGodotProjectPathValidator
{
    /// <summary>
    /// Returns true if the path is a valid Godot project root.
    /// </summary>
    Task<bool> IsValidGodotProjectRootAsync(string path, CancellationToken cancellationToken = default);
}

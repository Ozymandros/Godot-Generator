#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotMcp.Plugin;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Delegates Godot project validation to <see cref="GodotPlugin"/>.
/// </summary>
public sealed class GodotProjectPathValidator(GodotPlugin godotPlugin) : IGodotProjectPathValidator
{
    /// <inheritdoc />
    public Task<bool> IsValidGodotProjectRootAsync(string path, CancellationToken cancellationToken = default) =>
        godotPlugin.ValidateGodotProjectAsync(path, cancellationToken);
}

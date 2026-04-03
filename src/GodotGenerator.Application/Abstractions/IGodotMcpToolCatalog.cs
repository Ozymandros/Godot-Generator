#nullable enable

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Lists Semantic Kernel functions registered from the Godot MCP plugin (for diagnostics and discovery UIs).
/// </summary>
public interface IGodotMcpToolCatalog
{
    /// <summary>
    /// Returns registered tool/function names (best-effort; empty if the kernel cannot be created).
    /// </summary>
    Task<IReadOnlyList<string>> ListRegisteredToolNamesAsync(CancellationToken cancellationToken = default);
}

#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Per-async-flow defaults for Godot MCP tool calls. Semantic Kernel kernels are shared/cached, so request-scoped
/// values cannot live on the kernel; <see cref="System.Threading.AsyncLocal{T}"/> ties them to the current turn.
/// </summary>
internal static class GodotSkTurnContext
{
    private static readonly AsyncLocal<TurnState?> Current = new();

    /// <summary>
    /// Captures optional project root, project display name (create-project only), and default <c>fileName</c> for MCP 1.5.
    /// </summary>
    internal sealed record TurnState(string? GodotProjectRoot, string? ProjectName, string? DefaultFileName);

    internal static IDisposable Enter(string? godotProjectRoot, string? projectName, string? defaultFileName = null)
    {
        Current.Value = new TurnState(
            string.IsNullOrWhiteSpace(godotProjectRoot) ? null : godotProjectRoot.Trim(),
            string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim(),
            string.IsNullOrWhiteSpace(defaultFileName) ? null : defaultFileName.Trim());
        return new ClearScope();
    }

    internal static TurnState? Snapshot => Current.Value;

    private sealed class ClearScope : IDisposable
    {
        public void Dispose() => Current.Value = null;
    }
}

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
    /// Captures optional project root and name for the current agent turn.
    /// </summary>
    internal sealed record TurnState(string? GodotProjectRoot, string? ProjectName);

    internal static IDisposable Enter(string? godotProjectRoot, string? projectName)
    {
        Current.Value = new TurnState(
            string.IsNullOrWhiteSpace(godotProjectRoot) ? null : godotProjectRoot.Trim(),
            string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim());
        return new ClearScope();
    }

    internal static TurnState? Snapshot => Current.Value;

    private sealed class ClearScope : IDisposable
    {
        public void Dispose() => Current.Value = null;
    }
}

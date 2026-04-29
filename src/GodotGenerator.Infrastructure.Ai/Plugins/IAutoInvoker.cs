#nullable enable
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GodotGenerator.Infrastructure.Ai.Plugins;

/// <summary>
/// Inspects a completed LLM response for generated GDScript and, when code is detected,
/// automatically invokes the appropriate Godot MCP kernel function to persist or attach it.
/// </summary>
/// <remarks>
/// All implementations must be best-effort: failures should be logged at
/// <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/> and must never propagate
/// exceptions that would block the primary user response.
/// </remarks>
public interface IAutoInvoker
{
    /// <summary>
    /// Examines <paramref name="contents"/> for GDScript code blocks and, if found, invokes
    /// the best-matching Godot MCP function via <paramref name="kernel"/>.
    /// </summary>
    /// <param name="kernel">The active Semantic Kernel instance with registered Godot plugins.</param>
    /// <param name="contents">The chat message contents returned by the LLM for the current turn.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if a Godot MCP function was successfully invoked;
    /// <see langword="false"/> if no code was detected or all invocation attempts failed.
    /// </returns>
    Task<bool> TryAutoInvokeGeneratedCodeAsync(
        Kernel kernel,
        IReadOnlyList<ChatMessageContent> contents,
        CancellationToken ct);
}

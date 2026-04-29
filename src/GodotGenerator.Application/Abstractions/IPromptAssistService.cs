#nullable enable

using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Performs AI-driven prompt improvement or sample-prompt generation via a bare LLM call
/// (no Godot MCP plugin tools involved).
/// </summary>
public interface IPromptAssistService
{
    /// <summary>
    /// When <paramref name="request"/>'s <c>CurrentPrompt</c> is non-empty, improves it;
    /// otherwise generates a sample prompt for the given modality.
    /// Returns a <see cref="PromptAssistResult"/> with the result text and the mode used.
    /// </summary>
    Task<PromptAssistResult> EnhanceAsync(
        PromptAssistRequest request,
        CancellationToken cancellationToken = default);
}

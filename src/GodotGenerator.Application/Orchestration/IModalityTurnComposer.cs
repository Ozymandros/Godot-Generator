#nullable enable
using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Builds an <see cref="AgentTurnRequest"/> from modality metadata and public generation fields.
/// </summary>
public interface IModalityTurnComposer
{
    /// <summary>
    /// Composes a turn request with modality-specific system guidance and merged options.
    /// </summary>
    /// <param name="modalityKey">Modality identifier (e.g. text, code, godot-ui).</param>
    /// <param name="prompt">User prompt.</param>
    /// <param name="userSystemPrompt">Optional caller system prompt.</param>
    /// <param name="projectName">Optional project label for context.</param>
    /// <param name="preferredModelId">Optional model override.</param>
    /// <param name="options">Optional key/value options (e.g. preferred_language).</param>
    /// <returns>Request for <see cref="Abstractions.IAiOrchestrationService"/>.</returns>
    AgentTurnRequest Compose(
        string modalityKey,
        string prompt,
        string? userSystemPrompt,
        string? projectName,
        string? preferredModelId,
        IReadOnlyDictionary<string, object?>? options);
}

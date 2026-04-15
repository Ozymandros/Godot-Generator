#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Input for an "enhance or sample" prompt-assist operation.
/// </summary>
/// <param name="Modality">
/// Modality key used to choose the system instruction (e.g. <c>godot-physics</c>).
/// </param>
/// <param name="CurrentPrompt">
/// Existing prompt text to improve.  Empty / whitespace triggers sample generation instead.
/// </param>
/// <param name="SystemPromptOverride">
/// Optional caller-supplied system-prompt supplement (e.g. from Advanced Options).
/// </param>
/// <param name="FunctionalScope">Optional functional scope of the active UI view.</param>
/// <param name="ViewTitle">Optional active UI view title.</param>
/// <param name="ViewDescription">Optional active UI view description.</param>
/// <param name="Provider">Optional LLM provider override.</param>
/// <param name="PreferredModelId">Optional model-id override.</param>
public sealed record PromptAssistRequest(
    string Modality,
    string CurrentPrompt,
    string? SystemPromptOverride = null,
    string? FunctionalScope = null,
    string? ViewTitle = null,
    string? ViewDescription = null,
    string? Provider = null,
    string? PreferredModelId = null);

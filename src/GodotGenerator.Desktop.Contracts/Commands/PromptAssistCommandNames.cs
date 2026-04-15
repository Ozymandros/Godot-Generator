#nullable enable

namespace GodotGenerator.Desktop.Contracts.Commands;

/// <summary>Versioned command name constants for the PromptAssist domain.</summary>
public static class PromptAssistCommandNames
{
    /// <summary>
    /// Enhances an existing prompt or generates a sample prompt via a bare SK call
    /// (no plugin tool invocations).  Mode is determined by whether
    /// <see cref="PromptAssistEnhanceRequest.CurrentPrompt"/> is non-empty.
    /// </summary>
    public const string Enhance = "PromptAssist.Enhance/v1";
}

/// <summary>
/// Request payload for <see cref="PromptAssistCommandNames.Enhance"/>.
/// </summary>
/// <param name="Modality">Modality key (e.g. <c>godot-physics</c>).</param>
/// <param name="CurrentPrompt">
/// Existing prompt text to improve.  When empty or whitespace the backend
/// generates a sample prompt instead of improving an existing one.
/// </param>
/// <param name="SystemPromptOverride">
/// Optional additional system-prompt text supplied by the caller (e.g. from the
/// Advanced Options section).  Appended after the modality system instruction.
/// </param>
/// <param name="FunctionalScope">
/// Optional functional area label for the active view (e.g. <c>image generation</c>,
/// <c>audio generation</c>, <c>godot physics</c>).
/// </param>
/// <param name="ViewTitle">
/// Optional UI screen title where the prompt assistant is being used.
/// </param>
/// <param name="ViewDescription">
/// Optional UI screen description used to better scope sample/improved prompts.
/// </param>
/// <param name="Provider">Optional provider override for this call.</param>
/// <param name="PreferredModelId">Optional model-id override for this call.</param>
public sealed record PromptAssistEnhanceRequest(
    string Modality,
    string CurrentPrompt,
    string? SystemPromptOverride = null,
    string? FunctionalScope = null,
    string? ViewTitle = null,
    string? ViewDescription = null,
    string? Provider = null,
    string? PreferredModelId = null);

/// <summary>Response payload for <see cref="PromptAssistCommandNames.Enhance"/>.</summary>
/// <param name="Result">The enhanced or generated sample prompt text.</param>
/// <param name="Mode">
/// <c>"improve"</c> when an existing prompt was enhanced;
/// <c>"sample"</c> when a new sample was generated.
/// </param>
public sealed record PromptAssistEnhanceResponse(string Result, string Mode);

#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Input for a Wizard orchestration turn.
/// The wizard LLM may invoke multiple API generation tools before producing its final response.
/// </summary>
/// <param name="Prompt">User's goal description (what they want to accomplish).</param>
/// <param name="ProjectName">Optional Godot project label forwarded to each tool call.</param>
/// <param name="GodotProjectPath">Optional Godot project root path; forwarded to tools that validate it.</param>
/// <param name="Provider">Effective LLM provider (already resolved from preferences by the API layer).</param>
/// <param name="PreferredModelId">Effective model id (already resolved from preferences by the API layer).</param>
/// <param name="SystemPromptOverride">Optional system-prompt supplement from Advanced Options.</param>
public sealed record WizardRequest(
    string Prompt,
    string? ProjectName = null,
    string? GodotProjectPath = null,
    string? Provider = null,
    string? PreferredModelId = null,
    string? SystemPromptOverride = null);

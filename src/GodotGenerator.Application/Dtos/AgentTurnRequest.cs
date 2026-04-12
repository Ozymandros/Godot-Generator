#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Input for a single LLM + tool orchestration turn.
/// </summary>
/// <param name="Prompt">User prompt text.</param>
/// <param name="SystemPrompt">Optional system prompt (may include modality and language instructions).</param>
/// <param name="PreferredModelId">Optional model override.</param>
/// <param name="Modality">Logical modality key (e.g. text, code, godot-ui).</param>
/// <param name="Options">Optional request options (e.g. preferred_language, godot_project_path for validation).</param>
/// <param name="Provider">Optional provider/service key hint used to resolve runtime credentials.</param>
public sealed record AgentTurnRequest(
    string Prompt,
    string? SystemPrompt = null,
    string? PreferredModelId = null,
    string? Modality = null,
    IReadOnlyDictionary<string, object?>? Options = null,
    string? Provider = null);

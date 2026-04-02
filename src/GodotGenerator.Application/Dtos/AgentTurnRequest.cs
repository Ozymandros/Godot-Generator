#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Input for a single LLM + tool orchestration turn.
/// </summary>
public sealed record AgentTurnRequest(
    string Prompt,
    string? SystemPrompt = null,
    string? PreferredModelId = null);

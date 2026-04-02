#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Outcome of an agent turn (LLM text and optional tool traces are summarized as text for the port).
/// </summary>
public sealed record AgentTurnResult(
    bool Success,
    string Message,
    string? Detail = null);

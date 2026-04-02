using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Port for running an orchestrated LLM turn with Godot MCP tools (implemented in Infrastructure.Ai using Semantic Kernel).
/// </summary>
public interface IAiOrchestrationService
{
    /// <summary>
    /// Runs one agent turn: chat completion with automatic kernel function invocation for Godot tools.
    /// </summary>
    /// <param name="request">User prompt and optional system prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured result for the UI or callers.</returns>
    Task<AgentTurnResult> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default);
}

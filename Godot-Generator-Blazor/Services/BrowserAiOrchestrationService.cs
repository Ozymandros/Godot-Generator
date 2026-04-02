using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;

namespace Godot_Generator_Blazor.Services;

/// <summary>
/// Browser-safe orchestration adapter used for direct-WASM mode.
/// </summary>
/// <remarks>
/// This adapter intentionally avoids server/process-bound MCP execution and returns
/// a deterministic result so UI integration can function without backend hosting.
/// </remarks>
public sealed class BrowserAiOrchestrationService : IAiOrchestrationService
{
    /// <inheritdoc />
    public Task<AgentTurnResult> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Task.FromResult(new AgentTurnResult(false, "Prompt is required."));
        }

        var message = $"[Direct WASM mode] {request.Prompt.Trim()}";
        return Task.FromResult(new AgentTurnResult(true, message));
    }
}

using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Application use case: orchestrates a single agent turn via <see cref="IAiOrchestrationService"/>.
/// </summary>
public sealed class RunAgentTurnUseCase(
    IAiOrchestrationService aiOrchestration,
    ILogger<RunAgentTurnUseCase> logger)
{
    /// <summary>
    /// Executes the agent turn.
    /// </summary>
    public async Task<AgentTurnResult> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new AgentTurnResult(false, "Prompt is required.");
        }

        logger.LogDebug("RunAgentTurn: starting turn");
        return await aiOrchestration.RunTurnAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

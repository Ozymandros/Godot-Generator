#nullable enable

using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Orchestrates a single Wizard turn: runs the LLM with the
/// <c>GodotGeneratorApi</c> SK plugin registered so the model can invoke
/// any generation or configuration tool to fulfil the user's goal.
/// </summary>
public interface IWizardOrchestrationService
{
    /// <summary>
    /// Executes one wizard turn.  The LLM may call multiple generation tools
    /// (code, scenes, physics, lighting, etc.) before returning a consolidated response.
    /// </summary>
    /// <param name="request">Wizard turn parameters including the user prompt and project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WizardResult> RunAsync(
        WizardRequest request,
        CancellationToken cancellationToken = default);
}

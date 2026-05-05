#nullable enable

using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Validates wizard input and delegates to <see cref="IWizardOrchestrationService"/>.
/// Provides a thin, testable boundary between the API layer and the orchestration infrastructure.
/// </summary>
public sealed class RunWizardUseCase(
    IWizardOrchestrationService wizardOrchestration,
    ILogger<RunWizardUseCase> logger)
{
    /// <summary>
    /// Validates that the prompt is non-empty, then runs one wizard turn.
    /// </summary>
    /// <param name="request">Wizard request; <see cref="WizardRequest.Prompt"/> must be non-empty.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<WizardResult> ExecuteAsync(
        WizardRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return WizardResult.Fail("Wizard prompt cannot be empty.");
        }

        logger.LogDebug(
            "RunWizardUseCase: running wizard turn, provider={Provider}, model={ModelId}",
            request.Provider,
            request.PreferredModelId);

        return await wizardOrchestration
            .RunAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }
}

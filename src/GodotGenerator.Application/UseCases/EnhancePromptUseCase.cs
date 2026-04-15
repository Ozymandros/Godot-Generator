#nullable enable

using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Application.UseCases;

/// <summary>
/// Delegates prompt-assist operations (improve or generate a sample) to
/// <see cref="IPromptAssistService"/>, providing a thin validation and logging layer.
/// </summary>
public sealed class EnhancePromptUseCase(
    IPromptAssistService promptAssistService,
    ILogger<EnhancePromptUseCase> logger)
{
    /// <summary>
    /// Validates <paramref name="request"/> then calls the underlying service.
    /// Returns a failure <see cref="PromptAssistResult"/> if the modality key is absent;
    /// propagates service results otherwise.
    /// </summary>
    /// <param name="request">Prompt-assist input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PromptAssistResult> ExecuteAsync(
        PromptAssistRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Modality))
        {
            return PromptAssistResult.Fail("Modality key is required.");
        }

        var mode = string.IsNullOrWhiteSpace(request.CurrentPrompt) ? "sample" : "improve";
        logger.LogDebug(
            "EnhancePromptUseCase: mode={Mode}, modality={Modality}",
            mode,
            request.Modality);

        return await promptAssistService
            .EnhanceAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }
}

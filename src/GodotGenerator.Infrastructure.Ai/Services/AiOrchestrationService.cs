#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Orchestrates LLM turns with automatic Godot MCP tool invocation via Semantic Kernel.
/// </summary>
public sealed class AiOrchestrationService(
    IKernelFactory kernelFactory,
    IOptions<OrchestrationOptions> orchestrationOptions,
    IGodotProjectPathValidator godotProjectPathValidator,
    ILogger<AiOrchestrationService> logger) : IAiOrchestrationService
{
    /// <inheritdoc />
    public async Task<AgentTurnResult> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await TryValidateGodotProjectPathAsync(request, cancellationToken).ConfigureAwait(false);
        if (validation is not null)
        {
            return validation;
        }

        CancellationToken effectiveCancellationToken = cancellationToken;
        CancellationTokenSource? timeoutCts = null;
        try
        {
            if (orchestrationOptions.Value.TurnTimeoutSeconds > 0)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(orchestrationOptions.Value.TurnTimeoutSeconds));
                effectiveCancellationToken = timeoutCts.Token;
            }

            var kernel = await kernelFactory
                .GetOrCreateKernelAsync(request.PreferredModelId, effectiveCancellationToken)
                .ConfigureAwait(false);
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                history.AddSystemMessage(request.SystemPrompt);
            }

            history.AddUserMessage(request.Prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = orchestrationOptions.Value.EnableAutoToolInvocation
                    ? ToolCallBehavior.AutoInvokeKernelFunctions
                    : ToolCallBehavior.EnableKernelFunctions,
            };

            var contents = await chat
                .GetChatMessageContentsAsync(history, settings, kernel, cancellationToken: effectiveCancellationToken)
                .ConfigureAwait(false);

            var text = NormalizeResponseText(contents);
            return new AgentTurnResult(true, text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new AgentTurnResult(false, "Agent turn timed out.", "The operation exceeded the configured timeout.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent turn failed");
            var safeMessage = string.IsNullOrWhiteSpace(orchestrationOptions.Value.GenericFailureMessage)
                ? "Agent turn failed. Check logs for details."
                : orchestrationOptions.Value.GenericFailureMessage;
            return new AgentTurnResult(false, safeMessage, NormalizeUserDetail(ex));
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    /// <summary>
    /// When <see cref="ModalityTurnComposer.GodotProjectPathOptionKey"/> is set, validates the path via the Godot plugin before the LLM runs.
    /// </summary>
    private async Task<AgentTurnResult?> TryValidateGodotProjectPathAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Options is null ||
            !request.Options.TryGetValue(ModalityTurnComposer.GodotProjectPathOptionKey, out var raw) ||
            raw is null)
        {
            return null;
        }

        var path = raw switch
        {
            string s => s.Trim(),
            _ => raw.ToString()?.Trim(),
        };

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var valid = await godotProjectPathValidator
            .IsValidGodotProjectRootAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (valid)
        {
            return null;
        }

        return new AgentTurnResult(
            false,
            "The provided Godot project path is not valid (expected project.godot at the root).",
            path);
    }

    /// <summary>
    /// Converts multi-message chat output into a deterministic response payload.
    /// </summary>
    /// <param name="contents">Chat response message collection.</param>
    /// <returns>Normalized text for callers.</returns>
    private static string NormalizeResponseText(IReadOnlyList<ChatMessageContent> contents)
    {
        if (contents.Count == 0)
        {
            return "(no response)";
        }

        var parts = contents
            .Select(c => c.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .ToArray();

        if (parts.Length == 0)
        {
            return "(no response)";
        }

        return string.Join(Environment.NewLine, parts);
    }

    /// <summary>
    /// Produces a bounded detail string for diagnostics without exposing unbounded internal data.
    /// </summary>
    /// <param name="ex">Captured exception.</param>
    /// <returns>Sanitized detail text for result payloads.</returns>
    private static string NormalizeUserDetail(Exception ex)
    {
        var message = ex.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return "No additional detail available.";
        }

        return message.Length > 500 ? message[..500] + "..." : message;
    }
}

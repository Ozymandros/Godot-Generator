#nullable enable

using System.Text;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Improves an existing prompt or generates a sample prompt for the given modality.
/// Uses a direct Semantic Kernel chat-completion call with <em>no</em> tool invocation,
/// so Godot MCP plugin tools are never involved in this path.
/// </summary>
public sealed class PromptAssistService(
    IKernelFactory kernelFactory,
    IModalityTurnComposer modalityTurnComposer,
    ILogger<PromptAssistService> logger) : IPromptAssistService
{
    /// <inheritdoc />
    public async Task<PromptAssistResult> EnhanceAsync(
        PromptAssistRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isImprove = !string.IsNullOrWhiteSpace(request.CurrentPrompt);
        var mode = isImprove ? "improve" : "sample";

        try
        {
            var kernel = await kernelFactory
                .GetOrCreateKernelAsync(request.Provider, request.PreferredModelId, null, cancellationToken)
                .ConfigureAwait(false);

            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = BuildHistory(request, isImprove);

            // Explicitly disable all tool invocation — this is a raw text generation call.
            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = null,
                Temperature = 0.4,
            };

            var contents = await chat
                .GetChatMessageContentsAsync(history, settings, kernel, cancellationToken)
                .ConfigureAwait(false);

            var result = NormalizeResponse(contents);
            if (string.IsNullOrWhiteSpace(result))
            {
                return PromptAssistResult.Fail("The model returned an empty response.", mode);
            }

            return PromptAssistResult.Ok(result, mode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PromptAssistService failed for modality={Modality}, mode={Mode}",
                request.Modality, mode);
            return PromptAssistResult.Fail(
                "Prompt assist failed. Check the configured provider and API key.",
                mode);
        }
    }

    /// <summary>
    /// Builds the chat history for improve or sample mode.
    /// </summary>
    private ChatHistory BuildHistory(PromptAssistRequest request, bool isImprove)
    {
        var modalityInstruction = modalityTurnComposer
            .GetSystemInstruction(request.Modality);

        var sb = new StringBuilder();
        sb.AppendLine("You are a concise prompt-engineering assistant for a Godot 4 AI game generator.");
        sb.AppendLine($"Generation context: {modalityInstruction}");

        if (!string.IsNullOrWhiteSpace(request.SystemPromptOverride))
        {
            sb.AppendLine(request.SystemPromptOverride.Trim());
        }

        if (isImprove)
        {
            sb.Append(
                "Task: rewrite the user's generation prompt to be more specific, clear, and " +
                "effective for this context. Return ONLY the improved prompt text — " +
                "no explanations, no commentary, no surrounding quotes.");
        }
        else
        {
            sb.Append(
                "Task: write a single concise example prompt that demonstrates what a user " +
                "might ask in this generation context. Return ONLY the sample prompt text — " +
                "no explanations, no commentary, no surrounding quotes.");
        }

        var history = new ChatHistory();
        history.AddSystemMessage(sb.ToString().Trim());

        var userMessage = isImprove
            ? request.CurrentPrompt.Trim()
            : "Generate a sample prompt for me.";
        history.AddUserMessage(userMessage);

        return history;
    }

    /// <summary>
    /// Joins multi-part responses into a single clean string.
    /// </summary>
    private static string NormalizeResponse(IReadOnlyList<ChatMessageContent> contents)
    {
        if (contents.Count == 0)
        {
            return string.Empty;
        }

        var parts = contents
            .Select(c => c.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim());

        return string.Join(Environment.NewLine, parts).Trim();
    }
}

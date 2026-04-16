#nullable enable

using System.Text;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using GodotGenerator.Plugins.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GodotGenerator.Plugins.Services;

/// <summary>
/// Runs one wizard orchestration turn with the dedicated MCP plugin toolset.
/// </summary>
public sealed class WizardOrchestrationService(
    WizardMcpPlugin wizardPlugin,
    IProviderConnectionResolver providerConnectionResolver,
    IOptions<OrchestrationOptions> orchestrationOptions,
    ILoggerFactory loggerFactory,
    ILogger<WizardOrchestrationService> logger) : IWizardOrchestrationService
{
    /// <summary>
    /// Executes a single wizard turn using the dedicated SK plugin pipeline.
    /// </summary>
    /// <param name="request">Wizard request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A successful wizard result or a user-safe failure result.</returns>
    public async Task<WizardResult> RunAsync(
        WizardRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CancellationToken effectiveCt = cancellationToken;
        CancellationTokenSource? timeoutCts = null;

        try
        {
            var timeoutSec = orchestrationOptions.Value.TurnTimeoutSeconds;
            if (timeoutSec > 0)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
                effectiveCt = timeoutCts.Token;
            }

            var kernel = await BuildKernelAsync(request.Provider, request.PreferredModelId, effectiveCt).ConfigureAwait(false);
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = BuildHistory(request);
            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.5,
            };

            var toolsInvoked = new List<string>();
            kernel.FunctionInvocationFilters.Add(new ToolTrackingFilter(toolsInvoked));

            var contents = await chat
                .GetChatMessageContentsAsync(history, settings, kernel, cancellationToken: effectiveCt)
                .ConfigureAwait(false);

            var message = NormalizeResponse(contents);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "(The Wizard completed the requested tasks but returned no summary.)";
            }

            logger.LogInformation("Wizard turn completed; tools invoked: [{Tools}]", string.Join(", ", toolsInvoked));
            return WizardResult.Ok(message, toolsInvoked);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return WizardResult.Fail("The Wizard turn timed out.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Wizard turn failed.");
            var safeMessage = ex is InvalidOperationException ioe && ioe.Message.Contains("API key", StringComparison.OrdinalIgnoreCase)
                ? ioe.Message
                : "The Wizard encountered an error. Check the configured provider and API key.";
            return WizardResult.Fail(safeMessage);
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    /// <summary>
    /// Builds a dedicated Semantic Kernel instance for the wizard turn.
    /// </summary>
    /// <param name="provider">Preferred provider id.</param>
    /// <param name="preferredModelId">Optional preferred model id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configured kernel with wizard plugin tools registered.</returns>
    private async Task<Kernel> BuildKernelAsync(string? provider, string? preferredModelId, CancellationToken cancellationToken)
    {
        var connection = await providerConnectionResolver
            .ResolveAsync(provider, preferredModelId, cancellationToken)
            .ConfigureAwait(false);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(loggerFactory);
        if (connection.Endpoint is null)
        {
            builder.AddOpenAIChatCompletion(connection.ModelId, connection.ApiKey);
        }
        else
        {
            builder.AddOpenAIChatCompletion(connection.ModelId, connection.Endpoint, connection.ApiKey);
        }

        var kernel = builder.Build();
        kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(wizardPlugin, pluginName: "GodotGeneratorWizard"));

        logger.LogInformation(
            "Wizard kernel built for provider={Provider}, model={ModelId}; {ToolCount} tools registered.",
            connection.Provider,
            connection.ModelId,
            kernel.Plugins.SelectMany(static plugin => plugin).Count());

        return kernel;
    }

    /// <summary>
    /// Builds chat history for the current wizard turn.
    /// </summary>
    /// <param name="request">Wizard request payload.</param>
    /// <returns>System and user messages for the turn.</returns>
    private static ChatHistory BuildHistory(WizardRequest request)
    {
        var basePrompt = SystemPromptDefaults.ByModality.TryGetValue("wizard", out var prompt)
            ? prompt
            : "You are the Godot Generator Wizard.";
        var sb = new StringBuilder(basePrompt);

        if (!string.IsNullOrWhiteSpace(request.ProjectName))
        {
            sb.AppendLine();
            sb.Append($"Active project: {request.ProjectName.Trim()}.");
        }

        if (!string.IsNullOrWhiteSpace(request.GodotProjectPath))
        {
            sb.AppendLine();
            sb.Append($"Project path: {request.GodotProjectPath.Trim()}.");
        }

        if (!string.IsNullOrWhiteSpace(request.SystemPromptOverride))
        {
            sb.AppendLine();
            sb.Append(request.SystemPromptOverride.Trim());
        }

        var history = new ChatHistory();
        history.AddSystemMessage(sb.ToString().Trim());
        history.AddUserMessage(request.Prompt.Trim());
        return history;
    }

    /// <summary>
    /// Normalizes multi-message SK responses into a single response string.
    /// </summary>
    /// <param name="contents">Chat message content collection.</param>
    /// <returns>Normalized text response.</returns>
    private static string NormalizeResponse(IReadOnlyList<ChatMessageContent> contents)
    {
        var parts = contents
            .Select(static x => x.Content)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x!.Trim());
        return string.Join(Environment.NewLine, parts).Trim();
    }

    /// <summary>
    /// Tracks plugin function invocations performed during a wizard turn.
    /// </summary>
    private sealed class ToolTrackingFilter(List<string> invoked) : IFunctionInvocationFilter
    {
        /// <summary>
        /// Records the invoked function and continues filter execution.
        /// </summary>
        /// <param name="context">Invocation context for the current function call.</param>
        /// <param name="next">Next filter delegate.</param>
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            invoked.Add($"{context.Function.PluginName}.{context.Function.Name}");
            await next(context).ConfigureAwait(false);
        }
    }

}

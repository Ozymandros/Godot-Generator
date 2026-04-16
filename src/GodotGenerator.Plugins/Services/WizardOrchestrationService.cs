#nullable enable

using System.Text;
using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Services;
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
    IProviderSecretResolver providerSecretResolver,
    IPreferenceRepository preferences,
    IOptions<LlmOptions> llmOptions,
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
        var effectiveProvider = string.IsNullOrWhiteSpace(provider) ? "openai" : provider.Trim().ToLowerInvariant();
        var modelId = string.IsNullOrWhiteSpace(preferredModelId) ? llmOptions.Value.ChatModelId : preferredModelId.Trim();

        var apiKey = await providerSecretResolver.ResolveApiKeyAsync(effectiveProvider, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey) &&
            string.Equals(effectiveProvider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = llmOptions.Value.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"API key for provider '{effectiveProvider}' is not configured. Set it in Settings -> Secrets.");
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(loggerFactory);
        var endpoint = ResolveProviderEndpoint(effectiveProvider);
        if (endpoint is null)
        {
            builder.AddOpenAIChatCompletion(modelId, apiKey);
        }
        else
        {
            builder.AddOpenAIChatCompletion(modelId, endpoint, apiKey);
        }

        var kernel = builder.Build();
        kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(wizardPlugin, pluginName: "GodotGeneratorWizard"));

        logger.LogInformation(
            "Wizard kernel built for provider={Provider}, model={ModelId}; {ToolCount} tools registered.",
            effectiveProvider,
            modelId,
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

    /// <summary>
    /// Resolves the provider endpoint from registry settings or built-in defaults.
    /// </summary>
    /// <param name="provider">Provider id.</param>
    /// <returns>Provider endpoint URI when available; otherwise null.</returns>
    private Uri? ResolveProviderEndpoint(string provider)
    {
        var json = preferences
            .GetAsync(PreferenceKeys.ProvidersRegistryV1, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        var doc = ConfigurationRegistryService.ParseProviderRegistry(json);
        var entry = doc.Providers.FirstOrDefault(x => string.Equals(x.Id, provider, StringComparison.OrdinalIgnoreCase));
        if (entry is not null)
        {
            if (!entry.OpenAiCompatibility)
            {
                throw new InvalidOperationException($"Provider '{provider}' is not marked OpenAI-compatible.");
            }

            var configured = string.IsNullOrWhiteSpace(entry.Endpoint) ? GetDefaultEndpoint(provider) : entry.Endpoint.Trim();
            return TryParseEndpoint(provider, configured);
        }

        return TryParseEndpoint(provider, GetDefaultEndpoint(provider));
    }

    /// <summary>
    /// Validates and parses endpoint strings into absolute URIs.
    /// </summary>
    /// <param name="provider">Provider id used for diagnostics.</param>
    /// <param name="endpoint">Endpoint string candidate.</param>
    /// <returns>Absolute endpoint URI or null when endpoint is empty.</returns>
    private static Uri? TryParseEndpoint(string provider, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        throw new InvalidOperationException(
            $"Provider '{provider}' endpoint '{endpoint}' is not a valid absolute URI.");
    }

    /// <summary>
    /// Returns built-in OpenAI-compatible endpoint defaults for known providers.
    /// </summary>
    /// <param name="provider">Provider id.</param>
    /// <returns>Default endpoint URI as string when known; otherwise null.</returns>
    private static string? GetDefaultEndpoint(string provider) =>
        provider.ToLowerInvariant() switch
        {
            "anthropic" => "https://api.anthropic.com/v1",
            "google" => "https://generativelanguage.googleapis.com",
            "vertex_ai" => "https://aiplatform.googleapis.com",
            "deepseek" => "https://api.deepseek.com/v1",
            "openrouter" => "https://openrouter.ai/api/v1",
            "huggingface" => "https://api-inference.huggingface.co",
            "ollama" => "http://localhost:11434/v1",
            "groq" => "https://api.groq.com/openai/v1",
            "qwen" => "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "stability" => "https://api.stability.ai",
            "flux" => "https://api.bfl.ai/v1",
            "elevenlabs" => "https://api.elevenlabs.io",
            "playht" => "https://api.play.ht/api/v2",
            _ => null,
        };
}

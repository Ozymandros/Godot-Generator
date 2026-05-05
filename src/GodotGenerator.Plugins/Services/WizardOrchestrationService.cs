#nullable enable

using System.Text;
using System.Text.Json;
using System.IO;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using GodotGenerator.Plugins.Plugins;
using GodotMcp.Plugin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GodotGenerator.Plugins.Services;

/// <summary>
/// Runs one wizard orchestration turn with a four-plugin kernel:
/// GodotMcp (Godot editor tools), WizardMcpPlugin (utility), ElevenLabs (audio/TTS),
/// and ImageGen (image generation).
/// </summary>
/// <remarks>
/// <para>
/// The base kernel is obtained from <see cref="IKernelFactory"/>, which caches a fully
/// initialised kernel with all Godot MCP tools. That kernel is <em>cloned</em> for each
/// wizard turn so the shared cache is never mutated; the wizard-specific plugins are then
/// added to the clone only.
/// </para>
/// <para>
/// External plugins (ElevenLabs, ImageGen) are loaded from environment variables by
/// <see cref="WizardExternalPluginFactory"/>. A missing API key causes the plugin to be
/// silently skipped — the turn proceeds with the remaining plugins.
/// </para>
/// </remarks>
public sealed class WizardOrchestrationService(
    WizardMcpPlugin wizardPlugin,
    IKernelFactory kernelFactory,
    IOptions<OrchestrationOptions> orchestrationOptions,
    ILogger<WizardOrchestrationService> logger,
    GodotPlugin? godotPlugin = null) : IWizardOrchestrationService
{
    /// <summary>
    /// Executes a single wizard turn using the four-plugin SK kernel.
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

            GenerationIpcProgressContext.EmitIfActive(GenerationProgressFrame.Status("Wizard turn starting…"));

            var kernel = await BuildKernelAsync(request.Provider, request.PreferredModelId, request.GodotProjectPath, effectiveCt).ConfigureAwait(false);
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = BuildHistory(request);
            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.5,
            };

            var toolsInvoked = new List<string>();
            kernel.FunctionInvocationFilters.Add(new ToolTrackingFilter(toolsInvoked, request, logger, godotPlugin));

            GenerationIpcProgressContext.EmitIfActive(GenerationProgressFrame.Status("LLM processing request…"));

            var contents = await chat
                .GetChatMessageContentsAsync(history, settings, kernel, cancellationToken: effectiveCt)
                .ConfigureAwait(false);

            var message = NormalizeResponse(contents);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "(The Wizard completed the requested tasks but returned no summary.)";
            }

            GenerationIpcProgressContext.EmitIfActive(GenerationProgressFrame.Status("Wizard turn complete."));
            logger.LogInformation("Wizard turn completed; tools invoked: [{Tools}]", string.Join(", ", toolsInvoked));
            return WizardResult.Ok(message, toolsInvoked);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Wizard turn cancelled or timed out.");
            return WizardResult.Fail("The Wizard turn timed out.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Wizard turn failed: {Message}", ex.Message);
            var safeMessage = ex is InvalidOperationException ioe && ioe.Message.Contains("API key", StringComparison.OrdinalIgnoreCase)
                ? ioe.Message
                : "The Wizard encountered an error. Check the configured provider and API key.";
            try
            {
                return WizardResult.Fail(safeMessage);
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Failed to create WizardResult failure response");
                return WizardResult.Fail("The Wizard encountered an unexpected error.");
            }
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    /// <summary>
    /// Builds the per-turn Wizard kernel by cloning the shared base kernel and layering
    /// wizard-specific plugins on top.
    /// </summary>
    /// <remarks>
    /// Plugin registration order:
    /// <list type="number">
    ///   <item>GodotMcp — inherited from the cloned base kernel (Godot editor tools).</item>
    ///   <item>GodotGeneratorWizard — utility plugin (get_configuration, set_preference).</item>
    ///   <item>ElevenLabsAudio — optional TTS plugin; skipped when <c>ELEVENLABS_API_KEY</c> is absent.</item>
    ///   <item>ImageGen — optional image plugin; skipped when <c>IMAGE_GEN_API_KEY</c> is absent.</item>
    /// </list>
    /// </remarks>
    /// <param name="provider">Preferred provider id.</param>
    /// <param name="preferredModelId">Optional preferred model id.</param>
    /// <param name="projectRoot">Optional project root path used by Godot MCP tools.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configured kernel with all available wizard plugin tools registered.</returns>
    private async Task<Kernel> BuildKernelAsync(
        string? provider,
        string? preferredModelId,
        string? projectRoot,
        CancellationToken cancellationToken)
    {
        // Obtain the shared base kernel (with GodotMcp tools) and clone it so that adding
        // wizard-specific plugins does not mutate the shared cached instance.
        var baseKernel = await kernelFactory
        .GetOrCreateKernelAsync(
            provider,
            preferredModelId,
            modalityKeyForToolFiltering: null,
            projectRoot: projectRoot ?? string.Empty,
            cancellationToken)
        .ConfigureAwait(false);

        var kernel = baseKernel.Clone();

        // Plugin 2 — utility: configuration inspection + preference management.
        kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(wizardPlugin, pluginName: "GodotGeneratorWizard"));

        // Plugin 3 — ElevenLabs audio/TTS (gracefully skipped when key is absent).
        var elevenLabsPlugin = await WizardExternalPluginFactory
            .TryCreateElevenLabsPluginAsync(logger, cancellationToken)
            .ConfigureAwait(false);
        if (elevenLabsPlugin is not null)
        {
            kernel.Plugins.Add(elevenLabsPlugin);
        }

        // Plugin 4 — Image generation (gracefully skipped when key is absent).
        var imageGenPlugin = await WizardExternalPluginFactory
            .TryCreateImageGenPluginAsync(logger, cancellationToken)
            .ConfigureAwait(false);
        if (imageGenPlugin is not null)
        {
            kernel.Plugins.Add(imageGenPlugin);
        }

        var totalTools = kernel.Plugins.SelectMany(static p => p).Count();
        logger.LogInformation(
            "Wizard kernel ready — provider={Provider}, model={ModelId}, " +
            "plugins=[GodotMcp, GodotGeneratorWizard{ElevenLabs}{ImageGen}], tools={ToolCount}.",
            provider ?? "(default)",
            preferredModelId ?? "(default)",
            elevenLabsPlugin is not null ? ", ElevenLabsAudio" : string.Empty,
            imageGenPlugin is not null ? ", ImageGen" : string.Empty,
            totalTools);

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
            sb.AppendLine();
            sb.Append(
                "For every file-creating or file-modifying Godot MCP call, you must pass this exact value as projectPath. " +
                "Do not substitute cwd or any other host path.");
        }

        if (!string.IsNullOrWhiteSpace(request.GodotTargetFileName))
        {
            sb.AppendLine();
            sb.Append($"Default MCP fileName (project-relative): {request.GodotTargetFileName.Trim()}.");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectName))
        {
            sb.AppendLine();
            sb.Append(
                "Use this project name only for create_godot_project calls (projectName/name aliases). " +
                "Do not overwrite non-create tools with projectName parameters.");
        }

        sb.AppendLine();
        sb.Append(
            "You must orchestrate tool calls that result in at least one concrete file creation or modification " +
            "for the user's request before returning your summary.");

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
    /// Tracks plugin function invocations performed during a wizard turn and coerces
    /// boolean-like string arguments for Godot MCP tools that expect a real bool.
    /// </summary>
    private sealed class ToolTrackingFilter(
        List<string> invoked,
        WizardRequest request,
        ILogger logger,
        GodotPlugin? godotPlugin) : IFunctionInvocationFilter
    {
        /// <summary>
        /// Records the invoked function, applies Godot project-context injection with debug
        /// logging, and continues filter execution.
        /// </summary>
        /// <param name="context">Invocation context for the current function call.</param>
        /// <param name="next">Next filter delegate.</param>
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            var invocationId = $"{context.Function.PluginName}.{context.Function.Name}";
            invoked.Add(invocationId);

            // Fill project path / name on known MCP parameter names when the model omitted them.
            // Parity with AiOrchestrationService.GodotToolDebugFilter; logger enables debug tracing.
            GodotKernelToolArgumentInjection.ApplyProjectDefaults(
                context,
                request.GodotProjectPath,
                request.ProjectName,
                request.GodotTargetFileName,
                logger);

            // If this is a create-project invocation, proactively set the Godot MCP
            // server working directory to the composite project path (projectRoot + projectName)
            // so the server creates files in the intended subfolder rather than the plain root.
            try
            {
                var funcName = context.Function.Name ?? string.Empty;
                if (godotPlugin is not null
                    && !string.IsNullOrWhiteSpace(request.GodotProjectPath)
                    && !string.IsNullOrWhiteSpace(request.ProjectName)
                    && funcName.IndexOf("create", StringComparison.OrdinalIgnoreCase) >= 0
                    && funcName.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var godotPathTrim = request.GodotProjectPath.Trim();
                    var projectNameTrim = request.ProjectName.Trim();
                    string combinedRoot;
                    try
                    {
                        var rootLeaf = Path.GetFileName(godotPathTrim.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (!string.IsNullOrWhiteSpace(rootLeaf) && string.Equals(rootLeaf, projectNameTrim, StringComparison.OrdinalIgnoreCase))
                        {
                            combinedRoot = godotPathTrim;
                        }
                        else
                        {
                            combinedRoot = Path.Combine(godotPathTrim, projectNameTrim);
                        }
                    }
                    catch
                    {
                        combinedRoot = Path.Combine(godotPathTrim, projectNameTrim);
                    }

                    // Ensure the target composite directory exists before starting the MCP server
                    // with that working directory. Starting a process with a non-existent
                    // working directory causes a Win32Exception (invalid directory name).
                    var applyPath = combinedRoot;
                    try
                    {
                        if (!Directory.Exists(combinedRoot))
                        {
                            Directory.CreateDirectory(combinedRoot);
                            logger.LogDebug("Created composite project directory for create-project: {Path}", combinedRoot);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we cannot create the composite folder, fall back to the plain
                        // project root so the MCP process can still start. The create-project
                        // tool should then create the subfolder as part of its operation.
                        logger.LogDebug(ex, "Could not create composite project directory; falling back to project root.");
                        applyPath = godotPathTrim;
                    }

                    //await godotPlugin.ApplyProjectRootAsync(applyPath).ConfigureAwait(false);
                    logger.LogDebug("Applied composite project root for create-project: {Path}", applyPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to apply composite project root before tool invocation; continuing.");
            }

            // Debug: log invocation arguments for create-project and create-like tool calls
            try
            {
                var funcName = context.Function.Name ?? string.Empty;
                if (funcName.IndexOf("create_godot_project", StringComparison.OrdinalIgnoreCase) >= 0
                    || funcName.IndexOf("create", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in context.Arguments)
                    {
                        args[kvp.Key] = kvp.Value;
                    }
                    logger.LogInformation("Invoking {Plugin}.{Function} with args: {@Args}", context.Function.PluginName, funcName, args);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to log function invocation args for debugging.");
            }

            // Emit a structured progress frame before the tool executes so the user sees
            // live activity in the Wizard panel. Plugin and function names are safe to forward.
            GenerationIpcProgressContext.EmitIfActive(
                GenerationProgressFrame.Tool(
                    context.Function.PluginName ?? "unknown",
                    context.Function.Name ?? "unknown"));

            if (invocationId.Contains("configure", StringComparison.OrdinalIgnoreCase)
                || invocationId.Contains("autoload", StringComparison.OrdinalIgnoreCase))
            {
                var enabledArg = context.Arguments.FirstOrDefault(
                    static kvp => string.Equals(kvp.Key, "enabled", StringComparison.OrdinalIgnoreCase));

                var enabledValueObj = enabledArg.Value;

                var coerced = TryCoerceToBool(enabledValueObj, out var coercedValue);

                if (enabledValueObj is not null
                    && (!enabledValueObj.Equals(coercedValue) || enabledValueObj.GetType() != typeof(bool))
                    && coerced)
                {
                    // Update the invocation argument so Godot MCP parameter validation sees a boolean.
                    context.Arguments[enabledArg.Key] = coercedValue;
                }
            }

            await next(context).ConfigureAwait(false);
        }

        private static bool TryCoerceToBool(object? value, out bool result)
        {
            result = false;
            if (value is null) return false;

            if (value is bool b) { result = b; return true; }

            if (value is string s)
            {
                var normalized = s.Trim().ToLowerInvariant();
                if (normalized is "true" or "1" or "yes" or "y" or "on") { result = true; return true; }
                if (normalized is "false" or "0" or "no" or "n" or "off") { result = false; return true; }
                return bool.TryParse(normalized, out result);
            }

            switch (value)
            {
                case int i: result = i != 0; return true;
                case long l: result = l != 0L; return true;
                case float f: result = Math.Abs(f) > float.Epsilon; return true;
                case double d: result = Math.Abs(d) > double.Epsilon; return true;
                case decimal m: result = m != 0m; return true;
            }

            if (value is JsonElement el)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.True: result = true; return true;
                    case JsonValueKind.False: result = false; return true;
                    case JsonValueKind.Number: result = el.GetDouble() != 0d; return true;
                    case JsonValueKind.String:
                        var val = el.GetString();
                        return val is not null && TryCoerceToBool(val, out result);
                }
            }

            return false;
        }
    }
}

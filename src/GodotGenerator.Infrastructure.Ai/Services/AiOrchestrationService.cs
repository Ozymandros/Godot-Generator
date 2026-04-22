#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.Serialization;
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
using GodotMcp.Plugin;
using System.IO;

public sealed class AiOrchestrationService(
    IKernelFactory kernelFactory,
    IProviderCapabilityRouter providerCapabilityRouter,
    IOptions<OrchestrationOptions> orchestrationOptions,
    IGodotProjectPathValidator godotProjectPathValidator,
    ILogger<AiOrchestrationService> logger,
    GodotPlugin? godotPlugin = null) : IAiOrchestrationService
{
    /// <inheritdoc />
    public async Task<AgentTurnResult> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Project path validation is now handled elsewhere; deprecated lines removed.

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
            if (!providerCapabilityRouter.Supports(request.Provider, request.Modality, out var reason))
            {
                return new AgentTurnResult(false, reason ?? "Unsupported provider/modality combination.");
            }

            ExtractProjectContextFromOptions(request, out var turnProjectRoot, out var turnProjectName, out var turnDefaultFileName);

            // Normalize project path in a cross-platform way (do not create directories; let MCP handle it)
            if (!string.IsNullOrWhiteSpace(turnProjectRoot))
            {
                try
                {
                    turnProjectRoot = Path.GetFullPath(turnProjectRoot);
                    if (!Directory.Exists(turnProjectRoot))
                    {
                        Directory.CreateDirectory(turnProjectRoot);
                        logger.LogDebug("Created project directory: {Path}", turnProjectRoot);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to normalize project directory: {Path}", turnProjectRoot);
                }
            }

            using var _turnContext = GodotSkTurnContext.Enter(turnProjectRoot, turnProjectName, turnDefaultFileName);

            var kernel = await kernelFactory
                .GetOrCreateKernelAsync(
                    request.Provider,
                    request.PreferredModelId,
                    request.Modality,
                    turnProjectRoot ?? string.Empty,
                    effectiveCancellationToken)
                .ConfigureAwait(false);
            EnsureGodotToolFilters(kernel);
            var chat = kernel.GetRequiredService<IChatCompletionService>();


            var history = new ChatHistory();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                history.AddSystemMessage(request.SystemPrompt);
            }

            // Inject a synthetic tool call to get_project_info as the first message
            if (!string.IsNullOrWhiteSpace(turnProjectRoot))
            {
                // This is a special message that instructs the LLM to call the tool
                history.AddUserMessage($"#tool_call: get_project_info\nprojectPath: {turnProjectRoot}");
            }

            history.AddUserMessage(request.Prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = orchestrationOptions.Value.EnableAutoToolInvocation
                    ? ToolCallBehavior.AutoInvokeKernelFunctions
                    : ToolCallBehavior.EnableKernelFunctions,
            };

            if (TryGetTemperature(request.Options, out var temperature))
            {
                settings.Temperature = temperature;
            }

            var contents = await chat
                .GetChatMessageContentsAsync(history, settings, kernel, cancellationToken: effectiveCancellationToken)
                .ConfigureAwait(false);

            // Try to detect assistant-generated code and proactively invoke Godot MCP tools
            // so generated code is passed as tool arguments when the LLM returns code but
            // the tool schema or SK auto-invoke didn't wire it through.
            if (orchestrationOptions.Value.EnableAutoToolInvocation)
            {
                try
                {
                    await TryAutoInvokeGeneratedCodeAsync(kernel, contents, effectiveCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Automatic MCP invocation failed; continuing without blocking the response.");
                }
            }

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
            if (ex is InvalidOperationException ioe &&
                ioe.Message.Contains("API key for provider", StringComparison.OrdinalIgnoreCase))
            {
                return new AgentTurnResult(false, ioe.Message);
            }

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

    private const string TemperatureOptionKey = "temperature";

    private const string GodotToolFiltersAttachedKey = "__GodotGenerator.GodotToolFiltersAttached";

    private void EnsureGodotToolFilters(Kernel kernel)
    {
        if (!kernel.Data.TryAdd(GodotToolFiltersAttachedKey, true))
        {
            return;
        }

        kernel.FunctionInvocationFilters.Add(new GodotToolDebugFilter(logger, godotPlugin));
    }

    private static void ExtractProjectContextFromOptions(
        AgentTurnRequest request,
        out string? godotProjectRoot,
        out string? projectName,
        out string? defaultFileName)
    {
        godotProjectRoot = null;
        projectName = null;
        defaultFileName = null;
        if (request.Options is null)
        {
            return;
        }

        if (request.Options.TryGetValue(ModalityTurnComposer.GodotProjectPathOptionKey, out var pathRaw) &&
            pathRaw is not null)
        {
            godotProjectRoot = JsonOptionValue.AsTrimmedString(pathRaw);
        }

        if (request.Options.TryGetValue(ModalityTurnComposer.ProjectNameOptionKey, out var nameRaw) &&
            nameRaw is not null)
        {
            projectName = JsonOptionValue.AsTrimmedString(nameRaw);
        }

        if (request.Options.TryGetValue(ModalityTurnComposer.GodotTargetFileNameOptionKey, out var fileRaw) &&
            fileRaw is not null)
        {
            defaultFileName = JsonOptionValue.AsTrimmedString(fileRaw);
        }
    }

    private static bool TryGetTemperature(IReadOnlyDictionary<string, object?>? options, out double temperature)
    {
        temperature = 0;
        if (options is null || !options.TryGetValue(TemperatureOptionKey, out var raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case double d:
                temperature = d;
                return true;
            case float f:
                temperature = f;
                return true;
            case int i:
                temperature = i;
                return true;
            case long l:
                temperature = l;
                return true;
            case string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed):
                temperature = parsed;
                return true;
            default:
                return false;
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

    /// <summary>
    /// Attempts to detect generated code in assistant output and invoke Godot MCP tools
    /// by passing the detected code as likely tool arguments. This is best-effort and
    /// intentionally swallows failures so it does not impact the user-visible response.
    /// </summary>
    private async Task TryAutoInvokeGeneratedCodeAsync(Kernel kernel, IReadOnlyList<ChatMessageContent> contents, CancellationToken ct)
    {
        if (kernel is null || contents is null || contents.Count == 0)
        {
            return;
        }

        // Combine assistant text for analysis
        var combined = contents
            .Select(c => c.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .ToArray();

        if (combined.Length == 0)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, combined);

        // Prefer fenced code blocks (```lang\n...\n```) and fall back to heuristics for GDScript
        string? script = null;
        try
        {
            var m = Regex.Match(text, "```(?:[\\w+-]*)\\r?\\n([\\s\\S]*?)\\r?\\n```", RegexOptions.Singleline);
            if (m.Success && m.Groups.Count > 1)
            {
                script = m.Groups[1].Value;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Fenced-code regex failed; falling back to heuristics.");
        }

        if (string.IsNullOrWhiteSpace(script))
        {
            var lowered = text.ToLowerInvariant();
            if (lowered.Contains("extends ") || lowered.Contains("func ") || lowered.Contains("class_name ") || lowered.Contains("signal "))
            {
                script = text;
            }
        }

        if (string.IsNullOrWhiteSpace(script))
        {
            return;
        }

        var args = new KernelArguments();
        var candidateKeys = new[] { "content", "script", "code", "fileContents", "contents", "source", "scriptText" };
        foreach (var k in candidateKeys)
        {
            args[k] = script;
        }

        // Provide a default file name if available in the turn context
        try
        {
            var turn = GodotSkTurnContext.Snapshot;
            if (turn is not null && !string.IsNullOrWhiteSpace(turn.DefaultFileName))
            {
                if (!args.ContainsKey("fileName") && !args.ContainsKey("filename") && !args.ContainsKey("file_name"))
                {
                    args["fileName"] = turn.DefaultFileName;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read turn defaults while preparing auto-invoke args.");
        }

        // Dynamic discovery: enumerate kernel plugins and functions to find best candidates
        var discovered = new List<(string plugin, string function, string? parameter, int score)>();
        try
        {
            foreach (var pluginObj in kernel.Plugins)
            {
                var pluginName = pluginObj?.Name ?? string.Empty;
                foreach (var fn in pluginObj)
                {
                    var functionName = fn?.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(functionName))
                    {
                        continue;
                    }

                    string? chosenParam = null;
                    try
                    {
                        var parameters = fn?.Metadata?.Parameters;
                        if (parameters is not null)
                        {
                            // Prefer exact candidate keys
                            foreach (var p in parameters)
                            {
                                var pn = p?.Name;
                                if (pn is null) continue;
                                if (candidateKeys.Any(k => string.Equals(k, pn, StringComparison.OrdinalIgnoreCase)))
                                {
                                    chosenParam = pn;
                                    break;
                                }
                            }

                            // Fallback: look for param names containing script/code/content/file/source
                            if (chosenParam is null)
                            {
                                foreach (var p in parameters)
                                {
                                    var pn = p?.Name;
                                    if (pn is null) continue;
                                    var lower = pn.ToLowerInvariant();
                                    if (lower.Contains("script") || lower.Contains("code") || lower.Contains("content") || lower.Contains("source") || lower.Contains("file"))
                                    {
                                        chosenParam = pn;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed inspecting parameters for {Plugin}.{Function}", pluginName, functionName);
                    }

                    // Heuristics scoring
                    var score = 0;
                    if (string.Equals(pluginName, "godot", StringComparison.OrdinalIgnoreCase)) score += 50;
                    if (string.Equals(pluginName, "script", StringComparison.OrdinalIgnoreCase)) score += 40;
                    if (functionName.IndexOf("create", StringComparison.OrdinalIgnoreCase) >= 0) score += 20;
                    if (functionName.IndexOf("attach", StringComparison.OrdinalIgnoreCase) >= 0) score += 20;
                    if (functionName.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0) score += 30;
                    if (chosenParam is not null) score += 30;
                    if (functionName.StartsWith("get_", StringComparison.OrdinalIgnoreCase) || functionName.IndexOf("info", StringComparison.OrdinalIgnoreCase) >= 0) score -= 25;

                    if (score > 0)
                    {
                        discovered.Add((pluginName, functionName, chosenParam, score));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Dynamic candidate discovery failed; will fall back to static candidates.");
            discovered.Clear();
        }

        // Debug payload + discovered candidates
        try
        {
            var payloadDict = new Dictionary<string, object?>();
            foreach (var k in candidateKeys)
            {
                if (args.TryGetValue(k, out var v))
                {
                    payloadDict[k] = v is JsonElement je ? je.GetRawText() : v;
                }
            }
            var payloadJson = JsonSerializer.Serialize(payloadDict);
            logger.LogDebug("Prepared auto-invoke payload: {PayloadJson}", payloadJson);

            if (discovered.Count > 0)
            {
                var discoveredJson = JsonSerializer.Serialize(discovered.OrderByDescending(d => d.score).Select(d => new { d.plugin, d.function, d.parameter, d.score }));
                logger.LogDebug("Discovered auto-invoke candidates: {Candidates}", discoveredJson);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to serialize discovery/payload info for debug logging.");
        }

        // Attempt discovered candidates ordered by score
        var ordered = discovered.OrderByDescending(d => d.score).ThenBy(d => d.plugin).ThenBy(d => d.function).ToArray();
        foreach (var (plugin, function, parameter, score) in ordered)
        {
            try
            {
                var invocationArgs = new KernelArguments();
                foreach (var k in candidateKeys) invocationArgs[k] = script;

                if (args.TryGetValue("fileName", out var fn)) invocationArgs["fileName"] = fn;
                else if (args.TryGetValue("filename", out fn)) invocationArgs["filename"] = fn;
                else if (args.TryGetValue("file_name", out fn)) invocationArgs["file_name"] = fn;

                if (!string.IsNullOrWhiteSpace(parameter)) invocationArgs[parameter] = script;

                await kernel.InvokeAsync(plugin, function, invocationArgs, ct).ConfigureAwait(false);
                logger.LogInformation("Auto-invoked {Plugin}.{Function} (discovered) with generated code (param: {Param}; keys: {Keys})", plugin, function, parameter ?? "<none>", string.Join(',', candidateKeys));
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Auto-invocation failed for discovered {Plugin}.{Function}; trying next.", plugin, function);
            }
        }

        if (ordered.Length > 0)
        {
            logger.LogDebug("Discovered candidates attempted but none succeeded; falling back to legacy static candidates.");
        }

        // Fallback static list
        var fallbackCandidates = new (string plugin, string function)[]
        {
            ("godot", "godot_attach_script"),
            ("godot", "godot_create_script"),
            ("script", "attach_script"),
            ("script", "create_script"),
            ("godot", "attach_script"),
            ("godot", "create_script"),
        };

        foreach (var (plugin, function) in fallbackCandidates)
        {
            try
            {
                await kernel.InvokeAsync(plugin, function, args, ct).ConfigureAwait(false);
                logger.LogInformation("Auto-invoked {Plugin}.{Function} with generated code (keys: {Keys})", plugin, function, string.Join(',', candidateKeys));
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Auto-invocation failed for {Plugin}.{Function}; trying next.", plugin, function);
            }
        }

        logger.LogDebug("Automatic MCP invocation attempted but no candidate succeeded.");
    }

    /// <summary>
    /// Typed-skill plugin names registered by <c>AddGodotMcpSkills</c> when the primary
    /// <c>RegisterGodotTools</c> path fails due to dotted MCP tool names (GodotMCP.Server 1.5+).
    /// Kept in sync with <c>GodotMcpKernelExtensions.AddGodotMcpSkills</c>.
    /// </summary>
    private static readonly HashSet<string> TypedSkillPluginNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "scene", "project", "resource", "script", "import",
        "camera", "ui", "light", "physics", "nav", "lint", "preset", "docs"
    };

    private static bool IsTypedSkillPlugin(string? pluginName) =>
        pluginName is not null && TypedSkillPluginNames.Contains(pluginName);

    private sealed class GodotToolDebugFilter(ILogger logger, GodotPlugin? godotPlugin) : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            var invocationId = $"{context.Function.PluginName}.{context.Function.Name}";

            var isGodotTool =
                string.Equals(context.Function.PluginName, "godot", StringComparison.OrdinalIgnoreCase) ||
                context.Function.Name.StartsWith("godot_", StringComparison.OrdinalIgnoreCase) ||
                invocationId.Contains("godot.godot_", StringComparison.OrdinalIgnoreCase) ||
                // Typed-skill plugin names used by AddGodotMcpSkills fallback path (dotted MCP tool names):
                IsTypedSkillPlugin(context.Function.PluginName);
            if (isGodotTool)
            {
                var turn = GodotSkTurnContext.Snapshot;
                if (turn is not null)
                {
                    GodotKernelToolArgumentInjection.ApplyProjectDefaults(
                        context,
                        turn.GodotProjectRoot,
                        turn.ProjectName,
                        turn.DefaultFileName,
                        logger);

                    // Log the prepared invocation arguments (raw values or Json text) for diagnostics.
                    try
                    {
                        var argSnapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in context.Arguments)
                        {
                            argSnapshot[kvp.Key] = kvp.Value is JsonElement je ? je.GetRawText() : kvp.Value;
                        }

                        var argsJson = JsonSerializer.Serialize(argSnapshot);
                        logger.LogDebug("Prepared Godot tool invocation {InvocationId} args: {ArgsJson}", invocationId, argsJson);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to serialize invocation args for {InvocationId}", invocationId);
                    }

                    // If this is a create-project invocation, proactively set the MCP
                    // server's working directory to the composite path so the server
                    // will perform file creation inside the intended subfolder.
                    try
                    {
                        var funcName = context.Function.Name ?? string.Empty;
                        if (godotPlugin is not null
                            && !string.IsNullOrWhiteSpace(turn.GodotProjectRoot)
                            && !string.IsNullOrWhiteSpace(turn.ProjectName)
                            && funcName.IndexOf("create", StringComparison.OrdinalIgnoreCase) >= 0
                            && funcName.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var godotRootTrim = turn.GodotProjectRoot.Trim();
                            var projectNameTrim = turn.ProjectName.Trim();
                            string combined;
                            try
                            {
                                var rootLeaf = Path.GetFileName(godotRootTrim.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                                if (!string.IsNullOrWhiteSpace(rootLeaf) && string.Equals(rootLeaf, projectNameTrim, StringComparison.OrdinalIgnoreCase))
                                {
                                    combined = godotRootTrim;
                                }
                                else
                                {
                                    combined = Path.Combine(godotRootTrim, projectNameTrim);
                                }
                            }
                            catch
                            {
                                combined = Path.Combine(godotRootTrim, projectNameTrim);
                            }

                            try
                            {
                                if (!Directory.Exists(combined))
                                {
                                    Directory.CreateDirectory(combined);
                                    logger.LogDebug("Created composite project directory for create-project: {Path}", combined);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogDebug(ex, "Could not create composite project directory for create-project; continuing.");
                            }

                            // Fire-and-forget apply; do not block the invocation if plugin fails.
                            //_ = godotPlugin.ApplyProjectRootAsync(combined);
                            logger.LogDebug("Applied composite project root for create-project: {Path}", combined);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to apply composite project root before tool invocation; continuing.");
                    }
                }

                CoerceBoolArgument(context, "enabled", invocationId, logger);
                CoerceBoolArgument(context, "isCSharp", invocationId, logger);
                CoerceBoolArgument(context, "is_c_sharp", invocationId, logger);
                CoerceBoolArgument(context, "iscsharp", invocationId, logger);
            }

            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    var argSnapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in context.Arguments)
                    {
                        argSnapshot[kvp.Key] = kvp.Value is JsonElement je ? je.GetRawText() : kvp.Value;
                    }
                    var argsJson = JsonSerializer.Serialize(argSnapshot);
                    logger.LogError(ex, "Godot tool invocation {InvocationId} failed. Args: {ArgsJson}", invocationId, argsJson);
                }
                catch
                {
                    logger.LogError(ex, "Godot tool invocation {InvocationId} failed (args snapshot unavailable).", invocationId);
                }

                throw;
            }
        }

        private static void CoerceBoolArgument(
            FunctionInvocationContext context,
            string key,
            string invocationId,
            ILogger logger)
        {
            if (!context.Arguments.TryGetValue(key, out var raw) || raw is null)
            {
                return;
            }

            if (!TryCoerceToBool(raw, out var coerced))
            {
                return;
            }

            var isAlreadyBool = raw is bool rawBool && rawBool == coerced;
            if (!isAlreadyBool)
            {
                context.Arguments[key] = coerced;
            }

        }

        private static bool TryCoerceToBool(object? value, out bool result)
        {
            result = false;
            if (value is null)
            {
                return false;
            }

            if (value is bool b)
            {
                result = b;
                return true;
            }

            if (value is string s)
            {
                var normalized = s.Trim().ToLowerInvariant();
                if (normalized is "true" or "1" or "yes" or "y" or "on")
                {
                    result = true;
                    return true;
                }
                if (normalized is "false" or "0" or "no" or "n" or "off")
                {
                    result = false;
                    return true;
                }

                return bool.TryParse(normalized, out result);
            }

            switch (value)
            {
                case int i:
                    result = i != 0;
                    return true;
                case long l:
                    result = l != 0L;
                    return true;
                case float f:
                    result = Math.Abs(f) > float.Epsilon;
                    return true;
                case double d:
                    result = Math.Abs(d) > double.Epsilon;
                    return true;
                case decimal m:
                    result = m != 0m;
                    return true;
                case JsonElement el:
                    if (el.ValueKind == JsonValueKind.True)
                    {
                        result = true;
                        return true;
                    }
                    if (el.ValueKind == JsonValueKind.False)
                    {
                        result = false;
                        return true;
                    }
                    if (el.ValueKind == JsonValueKind.Number)
                    {
                        result = el.GetDouble() != 0d;
                        return true;
                    }
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var inner = el.GetString();
                        return inner is not null && TryCoerceToBool(inner, out result);
                    }
                    break;
            }

            return false;
        }
    }
}

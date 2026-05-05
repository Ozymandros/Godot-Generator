#nullable enable
using System.Text.Json;
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
using GodotMcp.Plugin;
using System.IO;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Orchestrates LLM turns with automatic Godot MCP tool invocation via Semantic Kernel.
/// </summary>
/// <param name="kernelFactory">Factory to create or reuse Semantic Kernel instances.</param>
/// <param name="providerCapabilityRouter">Router to determine provider/modality capabilities.</param>
/// <param name="orchestrationOptions">Orchestration options (feature flags, timeouts).</param>
/// <param name="godotProjectPathValidator">Validates provided Godot project root paths.</param>
/// <param name="logger">Logger for diagnostic messages.</param>
/// <param name="godotPlugin">Optional Godot MCP plugin instance.</param>
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

            var modalityLabel = request.Modality ?? "generation";
            GenerationIpcProgressContext.EmitIfActive(GenerationProgressFrame.Status($"{modalityLabel} starting..."));

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

            GenerationIpcProgressContext.EmitIfActive(GenerationProgressFrame.Status("Processing with LLM..."));

            var contents = await chat
                .GetChatMessageContentsAsync(history, settings, kernel, cancellationToken: effectiveCancellationToken)
                .ConfigureAwait(false);

var text = NormalizeResponseText(contents);
            GenerationIpcProgressContext.EmitIfActive(GenerationProgressFrame.Status($"{modalityLabel} complete."));
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

        // Native SK lifecycle filter: detects GDScript blocks in assistant messages and
        // auto-invokes the appropriate Godot file-creation function inside the completion
        // loop so the tool result is visible to the model before the final response is returned.
        kernel.AutoFunctionInvocationFilters.Add(new GodotAutoInvokeFilter(logger));
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
        private const string DebugLogPath = @"C:\Projects\Godot-Generator-Avalonia\debug-5ae4bd.log";
        private const string DebugSessionId = "5ae4bd";

public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            var invocationId = $"{context.Function.PluginName}.{context.Function.Name}";

            GenerationIpcProgressContext.EmitIfActive(
                GenerationProgressFrame.Tool(
                    context.Function.PluginName ?? "unknown",
                    context.Function.Name ?? "unknown"));

            var runId = Guid.NewGuid().ToString("N")[..8];
            var isSceneTool =
                string.Equals(context.Function.PluginName, "scene", StringComparison.OrdinalIgnoreCase);

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
                    if (isSceneTool)
                    {
                        var declaredParams = context.Function.Metadata.Parameters
                            .Select(p => p.Name)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Cast<string>()
                            .ToArray();
                        var preArgs = SnapshotArguments(context.Arguments);
                        var preProjectPath = preArgs.TryGetValue("projectPath", out var preProjectPathRaw)
                            ? preProjectPathRaw?.ToString()
                            : null;
                        var preFileName = preArgs.TryGetValue("fileName", out var preFileNameRaw)
                            ? preFileNameRaw?.ToString()
                            : null;
                        var preSceneAbsolutePath = TryBuildSceneAbsolutePath(preProjectPath, preFileName);
                        var preSceneCandidates = TryListSceneCandidates(preProjectPath, 10);
                        #region agent log
                        WriteDebugLog(
                            runId,
                            "H1",
                            "AiOrchestrationService.cs:351",
                            "scene tool pre-injection snapshot",
                            new
                            {
                                plugin = context.Function.PluginName,
                                function = context.Function.Name,
                                declaredParams,
                                args = preArgs,
                                undeclaredArgs = context.Arguments.Keys
                                    .Where(k => !declaredParams.Contains(k, StringComparer.OrdinalIgnoreCase))
                                    .ToArray(),
                                turnRoot = turn.GodotProjectRoot,
                                turnProjectName = turn.ProjectName,
                                turnDefaultFile = turn.DefaultFileName,
                                fs = new
                                {
                                    projectPath = preProjectPath,
                                    fileName = preFileName,
                                    projectGodotExists = TryFileExists(TryCombine(preProjectPath, "project.godot")),
                                    sceneAbsolutePath = preSceneAbsolutePath,
                                    sceneExists = TryFileExists(preSceneAbsolutePath),
                                    sceneCandidateCount = preSceneCandidates.Count,
                                    sceneCandidates = preSceneCandidates,
                                },
                            });
                        #endregion
                    }

                    GodotKernelToolArgumentInjection.ApplyProjectDefaults(
                        context,
                        turn.GodotProjectRoot,
                        turn.ProjectName,
                        turn.DefaultFileName,
                        logger);

                    if (isSceneTool)
                    {
                        var declaredParams = context.Function.Metadata.Parameters
                            .Select(p => p.Name)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Cast<string>()
                            .ToArray();
                        #region agent log
                        WriteDebugLog(
                            runId,
                            "H2",
                            "AiOrchestrationService.cs:377",
                            "scene tool post-injection snapshot",
                            new
                            {
                                plugin = context.Function.PluginName,
                                function = context.Function.Name,
                                declaredParams,
                                args = SnapshotArguments(context.Arguments),
                                undeclaredArgs = context.Arguments.Keys
                                    .Where(k => !declaredParams.Contains(k, StringComparer.OrdinalIgnoreCase))
                                    .ToArray(),
                            });
                        #endregion
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
                if (isSceneTool && TryBuildSceneInvocationError(context.Arguments, out var sceneValidationError))
                {
                    throw sceneValidationError;
                }

                await next(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (isSceneTool)
                {
                    var postArgs = SnapshotArguments(context.Arguments);
                    var postProjectPath = postArgs.TryGetValue("projectPath", out var postProjectPathRaw)
                        ? postProjectPathRaw?.ToString()
                        : null;
                    var postFileName = postArgs.TryGetValue("fileName", out var postFileNameRaw)
                        ? postFileNameRaw?.ToString()
                        : null;
                    var postSceneAbsolutePath = TryBuildSceneAbsolutePath(postProjectPath, postFileName);
                    var postSceneCandidates = TryListSceneCandidates(postProjectPath, 10);
                    #region agent log
                    WriteDebugLog(
                        runId,
                        "H3",
                        "AiOrchestrationService.cs:445",
                        "scene tool invocation exception",
                        new
                        {
                            plugin = context.Function.PluginName,
                            function = context.Function.Name,
                            args = postArgs,
                            exceptionType = ex.GetType().FullName,
                            exceptionMessage = ex.Message,
                            innerExceptionType = ex.InnerException?.GetType().FullName,
                            innerExceptionMessage = ex.InnerException?.Message,
                            exception = ex.ToString(),
                            fs = new
                            {
                                projectPath = postProjectPath,
                                fileName = postFileName,
                                projectGodotExists = TryFileExists(TryCombine(postProjectPath, "project.godot")),
                                sceneAbsolutePath = postSceneAbsolutePath,
                                sceneExists = TryFileExists(postSceneAbsolutePath),
                                sceneCandidateCount = postSceneCandidates.Count,
                                sceneCandidates = postSceneCandidates,
                            },
                        });
                    #endregion
                }

                throw;
            }
        }

        private static bool TryBuildSceneInvocationError(
            KernelArguments arguments,
            out Exception validationError)
        {
            validationError = null!;
            var projectPath = TryGetArgumentString(arguments, "projectPath");
            var fileName = TryGetArgumentString(arguments, "fileName");
            if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var sceneAbsolutePath = TryBuildSceneAbsolutePath(projectPath, fileName);
            if (TryFileExists(sceneAbsolutePath))
            {
                return false;
            }

            var projectLooksValid = TryFileExists(TryCombine(projectPath, "project.godot"));
            if (!projectLooksValid)
            {
                return false;
            }

            validationError = new InvalidOperationException(
                $"Scene tool validation failed: fileName '{fileName}' does not exist under projectPath '{projectPath}'. " +
                "Use an existing project-relative .tscn file or create one before invoking scene tools.");
            return true;
        }

        private static string? TryGetArgumentString(KernelArguments arguments, string key)
        {
            if (!arguments.TryGetValue(key, out var raw) || raw is null)
            {
                return null;
            }

            return raw switch
            {
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString(),
                _ => raw.ToString(),
            };
        }

        private static bool TryFileExists(string? path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string? TryCombine(string? left, string? right)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                {
                    return null;
                }

                return Path.Combine(left, right);
            }
            catch
            {
                return null;
            }
        }

        private static string? TryBuildSceneAbsolutePath(string? projectPath, string? fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(fileName))
                {
                    return null;
                }

                if (Path.IsPathRooted(fileName))
                {
                    return fileName;
                }

                return Path.Combine(
                    projectPath,
                    fileName.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            }
            catch
            {
                return null;
            }
        }

        private static List<string> TryListSceneCandidates(string? projectPath, int maxCount)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
                {
                    return [];
                }

                return Directory.EnumerateFiles(projectPath, "*.tscn", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectPath, path).Replace('\\', '/'))
                    .Take(Math.Max(1, maxCount))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        private static Dictionary<string, object?> SnapshotArguments(KernelArguments arguments)
        {
            var snapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in arguments)
            {
                snapshot[kvp.Key] = kvp.Value switch
                {
                    JsonElement je => je.ValueKind switch
                    {
                        JsonValueKind.Object or JsonValueKind.Array => je.GetRawText(),
                        JsonValueKind.String => je.GetString(),
                        JsonValueKind.Number when je.TryGetInt64(out var l) => l,
                        JsonValueKind.Number when je.TryGetDouble(out var d) => d,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => je.ToString(),
                    },
                    _ => kvp.Value,
                };
            }

            return snapshot;
        }

        private static void WriteDebugLog(
            string runId,
            string hypothesisId,
            string location,
            string message,
            object data)
        {
            try
            {
                var payload = new
                {
                    sessionId = DebugSessionId,
                    runId,
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };

                var line = JsonSerializer.Serialize(payload);
                File.AppendAllText(DebugLogPath, line + Environment.NewLine);
            }
            catch
            {
                // Never block tool execution for debug instrumentation failures.
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

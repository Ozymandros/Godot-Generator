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
            if (!providerCapabilityRouter.Supports(request.Provider, request.Modality, out var reason))
            {
                return new AgentTurnResult(false, reason ?? "Unsupported provider/modality combination.");
            }

            ExtractProjectContextFromOptions(request, out var turnProjectRoot, out var turnProjectName, out var turnDefaultFileName);
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
                            _ = godotPlugin.ApplyProjectRootAsync(combined);
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

            await next(context).ConfigureAwait(false);
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

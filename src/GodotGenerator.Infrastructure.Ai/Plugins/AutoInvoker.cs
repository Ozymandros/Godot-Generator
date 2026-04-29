#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GodotGenerator.Infrastructure.Ai.Plugins;

/// <summary>
/// Default implementation of <see cref="IAutoInvoker"/>.
/// The implementation is intentionally best-effort and swallows exceptions
/// to avoid impacting user-visible responses.
/// </summary>
public sealed class AutoInvoker : IAutoInvoker
{
    private readonly ILogger<AutoInvoker> logger;
    private readonly GodotMcpOptions options;

    public AutoInvoker(ILogger<AutoInvoker> logger, IOptions<GodotMcpOptions>? options = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? new GodotMcpOptions();
    }

    public async Task<bool> TryAutoInvokeGeneratedCodeAsync(Kernel kernel, IReadOnlyList<ChatMessageContent> contents, CancellationToken ct)
    {
        if (!this.options.EnableAutoInvokeGeneratedCode)
        {
            return false;
        }

        if (kernel is null || contents is null || contents.Count == 0)
        {
            return false;
        }

        try
        {
            // Combine assistant text
            var combined = contents
                .Select(c => c.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim())
                .ToArray();

            if (combined.Length == 0)
            {
                return false;
            }

            var text = string.Join(Environment.NewLine, combined);

            // Prefer fenced code blocks
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
                return false;
            }

            var args = new KernelArguments();
            var candidateKeys = new[] { "content", "script", "code", "fileContents", "contents", "source", "scriptText", "rawContent", "raw_content", "raw" };
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

            // Dynamic discovery
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

            // Debug payload
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

            // Attempt discovered candidates
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

                    if (this.options.AutoInvokeDryRun)
                    {
                        logger.LogInformation("Auto-invoke dry-run: would have invoked {Plugin}.{Function} (discovered) with param {Param}", plugin, function, parameter ?? "<none>");
                        return true;
                    }

                    await kernel.InvokeAsync(plugin, function, invocationArgs, ct).ConfigureAwait(false);
                    logger.LogInformation("Auto-invoked {Plugin}.{Function} (discovered) with generated code (param: {Param}; keys: {Keys})", plugin, function, parameter ?? "<none>", string.Join(',', candidateKeys));
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Auto-invocation failed for discovered {Plugin}.{Function}; trying next.", plugin, function);
                }
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
                    if (this.options.AutoInvokeDryRun)
                    {
                        logger.LogInformation("Auto-invoke dry-run: would have invoked {Plugin}.{Function} (fallback)", plugin, function);
                        return true;
                    }

                    await kernel.InvokeAsync(plugin, function, args, ct).ConfigureAwait(false);
                    logger.LogInformation("Auto-invoked {Plugin}.{Function} with generated code (keys: {Keys})", plugin, function, string.Join(',', candidateKeys));
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Auto-invocation failed for {Plugin}.{Function}; trying next.", plugin, function);
                }
            }

            logger.LogDebug("Automatic MCP invocation attempted but no candidate succeeded.");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Auto-invoker failed unexpectedly.");
            return false;
        }
    }
}

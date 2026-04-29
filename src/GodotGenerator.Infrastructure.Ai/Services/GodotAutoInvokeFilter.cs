#nullable enable
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// A Semantic Kernel <see cref="IAutoFunctionInvocationFilter"/> that detects GDScript code
/// blocks in assistant messages and automatically invokes the appropriate Godot MCP file-creation
/// or script-attachment function when the model generates code without a corresponding tool call.
/// </summary>
/// <remarks>
/// This filter runs inside the <c>GetChatMessageContentsAsync</c> execution loop, so any tool
/// results it produces are appended to the chat history before the final response is returned.
/// All invocation logic is best-effort: exceptions are swallowed and logged at
/// <see cref="LogLevel.Debug"/> so the primary user response is never blocked.
/// </remarks>
internal sealed class GodotAutoInvokeFilter(ILogger logger) : IAutoFunctionInvocationFilter
{
    // Matches any fenced code block and captures optional language label in group 1
    // and the code body in group 2.
    private static readonly Regex CodeBlockRegex = new(
        @"```(?:\s*([^\r\n`]+)\s*\r?\n)?([\s\S]*?)\r?\n```",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // GDScript heuristics: keywords that distinguish it from other languages.
    private static readonly Regex GdScriptHeuristicRegex = new(
        @"\b(?:extends|class_name|class\b|func\b|signal\b|onready\b|tool\b|export\b|setget\b|const\b|enum\b|match\b|pass\b|var\b|get_node\b|_ready\b|_process\b|_physics_process\b|emit_signal\b|load\s*\(|preload\s*\(|connect\s*\(|Resource|PackedScene|Node2D|Node3D|CharacterBody2D|RigidBody2D|Area2D|AnimationPlayer|Vector2|Vector3|Sprite2D|Sprite3D|GDScript)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // C# heuristics: keywords/patterns commonly found in Godot C# scripts.
    private static readonly Regex CSharpHeuristicRegex = new(
        @"\b(?:using\s+Godot|using\s+System|namespace\b|class\b|partial\b|public\b|private\b|protected\b|void\b|int\b|string\b|Task<|async\b|Console\.)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Tracks already-processed GDScript block hashes keyed by the turn's <see cref="ChatHistory"/>
    /// instance, so duplicates are never auto-invoked twice within a single turn.
    /// The weak reference ensures the set is garbage-collected when the chat history is discarded.
    /// </summary>
    private static readonly ConditionalWeakTable<ChatHistory, HashSet<string>> TurnProcessedHashes = new();

    /// <summary>
    /// Plugin names considered candidates for GDScript file creation or attachment operations.
    /// Kept in sync with <c>AiOrchestrationService.TypedSkillPluginNames</c>.
    /// </summary>
    private static readonly string[] CandidatePluginNames =
    [
        "godot", "script", "scene", "project", "resource"
    ];

    /// <summary>
    /// Function name fragments that indicate a create/write/attach operation.
    /// </summary>
    private static readonly string[] CreateKeywords =
    [
        "create", "write", "save", "attach", "add"
    ];

    /// <summary>
    /// Function name fragments that indicate the function operates on a script or file.
    /// </summary>
    private static readonly string[] ScriptKeywords =
    [
        "script", "file", "attach", "gdscript", "code"
    ];

    /// <summary>
    /// Ordered list of parameter name candidates that carry the script source code.
    /// </summary>
    private static readonly string[] CodeParamNames =
    [
        "content", "script", "code", "fileContents", "scriptContent",
        "file_contents", "script_content", "source"
    ];

    /// <summary>
    /// Ordered list of parameter name candidates that carry the destination file name / path.
    /// </summary>
    private static readonly string[] FileNameParamNames =
    [
        "fileName", "file_name", "filename", "path", "filePath", "file_path",
        "scriptName", "script_name", "name"
    ];

    /// <summary>
    /// Ordered list of parameter name candidates that carry the Godot project root path.
    /// </summary>
    private static readonly string[] ProjectPathParamNames =
    [
        "projectPath", "project_path", "projectRoot", "project_root",
        "rootPath", "root_path", "projectDir", "project_dir"
    ];

    /// <inheritdoc />
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        // Always let the normal invocation proceed first.
        await next(context).ConfigureAwait(false);

        // Best-effort post-invocation: scan history for unprocessed GDScript blocks.
        try
        {
            await TryAutoInvokeScriptCreationAsync(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "GodotAutoInvokeFilter: best-effort auto-invoke failed; primary response is unaffected.");
        }
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Scans all assistant messages in the current <see cref="AutoFunctionInvocationContext"/>
    /// chat history for GDScript blocks that have not yet been processed this turn.
    /// For each new block found, attempts to locate and invoke a suitable Godot plugin function.
    /// </summary>
    private async Task TryAutoInvokeScriptCreationAsync(AutoFunctionInvocationContext context)
    {
        var processedHashes = GetOrCreateProcessedHashes(context.ChatHistory);
        var turn = GodotSkTurnContext.Snapshot;

        foreach (var message in context.ChatHistory)
        {
            if (message.Role != AuthorRole.Assistant)
            {
                continue;
            }

            var content = message.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            foreach (Match match in CodeBlockRegex.Matches(content))
            {
                var lang = match.Groups.Count > 1 && match.Groups[1].Success
                    ? match.Groups[1].Value.Trim().ToLowerInvariant()
                    : null;
                var codeBlock = match.Groups.Count > 2 ? match.Groups[2].Value.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(codeBlock))
                {
                    continue;
                }

                // Decide language: label wins; otherwise use heuristics.
                var isGdScript = false;
                var isCSharp = false;
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    if (lang.Contains("gdscript", StringComparison.OrdinalIgnoreCase) || lang.Contains("gd", StringComparison.OrdinalIgnoreCase))
                    {
                        isGdScript = true;
                    }
                    else if (lang.Contains("csharp", StringComparison.OrdinalIgnoreCase) || lang.Contains("c#") || lang == "cs")
                    {
                        isCSharp = true;
                    }
                }
                else
                {
                    // Unlabeled: require heuristic evidence.
                    if (IsLikelyGdScript(codeBlock)) isGdScript = true;
                    else if (IsLikelyCSharp(codeBlock)) isCSharp = true;
                }

                if (!isGdScript && !isCSharp)
                {
                    continue;
                }

                var blockHash = ComputeHash(codeBlock);
                if (!processedHashes.Add(blockHash))
                {
                    // Already processed in a previous filter invocation this turn.
                    continue;
                }

                var targetFunction = FindScriptCreateFunction(context.Kernel);
                if (targetFunction is null)
                {
                    logger.LogDebug(
                        "GodotAutoInvokeFilter: code block detected but no suitable creation function found in kernel plugins.");
                    continue;
                }

                var args = BuildArguments(targetFunction, codeBlock, turn, isCSharp);

                logger.LogDebug(
                    "GodotAutoInvokeFilter: auto-invoking {Plugin}.{Function} for detected code block (hash: {Hash})",
                    targetFunction.PluginName,
                    targetFunction.Name,
                    blockHash[..8]);

                await context.Kernel.InvokeAsync(targetFunction, args).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Retrieves or creates the per-turn set that tracks already-processed GDScript block hashes.
    /// Keyed on the <see cref="ChatHistory"/> instance so the set is scoped to the current turn
    /// and is garbage-collected automatically when the history is discarded.
    /// </summary>
    private static HashSet<string> GetOrCreateProcessedHashes(ChatHistory chatHistory)
        => TurnProcessedHashes.GetValue(chatHistory, _ => new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Searches <see cref="Kernel.Plugins"/> for the most suitable function to create or attach
    /// a GDScript file. Priority: a function whose name contains both a <see cref="CreateKeywords"/>
    /// and a <see cref="ScriptKeywords"/> fragment, and that exposes a code-content parameter.
    /// </summary>
    private static KernelFunction? FindScriptCreateFunction(Kernel kernel)
    {
        foreach (var plugin in kernel.Plugins)
        {
            var pluginName = plugin.Name ?? string.Empty;

            var isCandidate = CandidatePluginNames.Any(
                p => pluginName.Contains(p, StringComparison.OrdinalIgnoreCase));

            if (!isCandidate)
            {
                continue;
            }

            foreach (var function in plugin)
            {
                var funcName = function.Name ?? string.Empty;

                var hasCreateKeyword = CreateKeywords.Any(
                    k => funcName.Contains(k, StringComparison.OrdinalIgnoreCase));

                var hasScriptKeyword = ScriptKeywords.Any(
                    k => funcName.Contains(k, StringComparison.OrdinalIgnoreCase));

                if (!hasCreateKeyword || !hasScriptKeyword)
                {
                    continue;
                }

                var hasCodeParam = function.Metadata.Parameters.Any(
                    p => CodeParamNames.Any(
                        n => string.Equals(n, p.Name, StringComparison.OrdinalIgnoreCase)));

                if (hasCodeParam)
                {
                    return function;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Constructs a <see cref="KernelArguments"/> instance populated with the script content,
    /// optional file name, and optional project path drawn from <paramref name="turn"/> context.
    /// Only parameters advertised by the target function's metadata are populated.
    /// </summary>
    private static KernelArguments BuildArguments(
        KernelFunction targetFunction,
        string codeBlock,
        GodotSkTurnContext.TurnState? turn,
        bool isCSharp = false)
    {
        var args = new KernelArguments();
        var paramNames = targetFunction.Metadata.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Map code content.
        var codeParam = CodeParamNames.FirstOrDefault(
            n => paramNames.Contains(n));
        if (codeParam is not null)
        {
            args[codeParam] = codeBlock;
        }

        // Map file name from turn context.
        if (turn?.DefaultFileName is not null)
        {
            var fileParam = FileNameParamNames.FirstOrDefault(
                n => paramNames.Contains(n));
            if (fileParam is not null)
            {
                args[fileParam] = turn.DefaultFileName;
            }
        }

        // Map project root from turn context.
        if (turn?.GodotProjectRoot is not null)
        {
            var projectParam = ProjectPathParamNames.FirstOrDefault(
                n => paramNames.Contains(n));
            if (projectParam is not null)
            {
                args[projectParam] = turn.GodotProjectRoot;
            }
        }

        // Indicate C# target when detected so plugins can adapt behavior.
        if (isCSharp)
        {
            // Populate several common flag names to increase interoperability.
            if (!paramNames.Contains("iscsharp") && !paramNames.Contains("isCSharp") && !paramNames.Contains("is_c_sharp"))
            {
                args["iscsharp"] = true;
            }

            if (paramNames.Contains("isCSharp")) args["isCSharp"] = true;
            if (paramNames.Contains("is_c_sharp")) args["is_c_sharp"] = true;
            if (paramNames.Contains("iscsharp")) args["iscsharp"] = true;
        }

        return args;
    }

    /// <summary>
    /// Computes a stable SHA-256 hex string for a code block string, used as a deduplication key.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Heuristic guard that requires at least two distinct GDScript-specific tokens
    /// to be present in the block before we consider auto-invoking.
    /// </summary>
    private static bool IsLikelyGdScript(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var matches = GdScriptHeuristicRegex.Matches(code);
        if (matches.Count == 0) return false;
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in matches)
        {
            var token = m.Value.Trim();
            var paren = token.IndexOf('(');
            if (paren >= 0) token = token.Substring(0, paren);
            token = Regex.Replace(token, @"\W+$", string.Empty);
            token = token.Trim();
            if (!string.IsNullOrEmpty(token)) tokens.Add(token);
        }
        return tokens.Count >= 2;
    }

    private static bool IsLikelyCSharp(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var matches = CSharpHeuristicRegex.Matches(code);
        if (matches.Count == 0) return false;
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in matches)
        {
            var token = m.Value.Trim();
            var paren = token.IndexOf('(');
            if (paren >= 0) token = token.Substring(0, paren);
            token = Regex.Replace(token, @"\W+$", string.Empty);
            token = token.Trim();
            if (!string.IsNullOrEmpty(token)) tokens.Add(token);
        }
        return tokens.Count >= 2;
    }
}

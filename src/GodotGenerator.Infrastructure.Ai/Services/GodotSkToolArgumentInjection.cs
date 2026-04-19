#nullable enable
using System.IO;
using System.Text.Json;
using GodotGenerator.Application.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Fills common Godot MCP tool parameters (project path / name) when the model omits them,
/// using the active request context from <see cref="GodotSkTurnContext"/> and the explicit
/// per-parameter contracts from <see cref="GodotToolContractRegistry"/>.
/// </summary>
/// <remarks>
/// For known Godot tools the registry resolves a deterministic <see cref="ParameterPolicy"/>
/// per parameter. Non-Godot tool parameters fall back to name-based heuristics so that
/// third-party plugins continue to work without explicit contract entries.
/// </remarks>
internal static class GodotSkToolArgumentInjection
{
    /// <summary>
    /// Applies project-context injection to all parameters and arguments of the current
    /// function invocation, emitting debug-level log entries for every decision made.
    /// </summary>
    /// <param name="context">Current kernel function invocation context.</param>
    /// <param name="state">Snapshot of the active turn's project root and name.</param>
    /// <param name="logger">Optional logger for debug tracing; pass <see langword="null"/> to suppress logs.</param>
    internal static void Apply(
        FunctionInvocationContext context,
        GodotSkTurnContext.TurnState state,
        ILogger? logger = null)
    {
        var pluginName = context.Function.PluginName;
        var functionName = context.Function.Name;

        // ── Pass 1: metadata parameters ─────────────────────────────────────────
        // Iterate declared parameters first so that registry-driven decisions
        // always precede the arg-key sweep.
        foreach (var param in context.Function.Metadata.Parameters)
        {
            var name = param.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            context.Arguments.TryGetValue(name, out var current);
            var policy = GodotToolContractRegistry.Resolve(pluginName, functionName, name);

            switch (policy)
            {
                case ParameterPolicy.ForceProject:
                    if (TryForceProjectRoot(name, state, out var forcedRoot))
                    {
                        logger?.LogDebug(
                            "[Injection] Force-set {Param}={Value} ({Tool}) via contract.",
                            name, forcedRoot, functionName);
                        context.Arguments[name] = forcedRoot!;
                    }
                    else if (GodotToolContractRegistry.IsCreateGodotProject(functionName)
                             && TryForceCreateProjectDisplayName(name, state, out var forcedName))
                    {
                        logger?.LogDebug(
                            "[Injection] Force-set {Param}={Value} ({Tool}) via contract (create project).",
                            name, forcedName, functionName);
                        context.Arguments[name] = forcedName!;
                    }
                    continue;

                case ParameterPolicy.MergeDefaultFileName:
                    if (JsonOptionValue.IsNullOrEmptyStringLike(current)
                        && !string.IsNullOrWhiteSpace(state.DefaultFileName))
                    {
                        logger?.LogDebug(
                            "[Injection] Merge default fileName {Param}={Value} ({Tool}).",
                            name, state.DefaultFileName, functionName);
                        context.Arguments[name] = state.DefaultFileName;
                    }
                    continue;

                case ParameterPolicy.MergeIfEmpty:
                    if (JsonOptionValue.IsNullOrEmptyStringLike(current)
                        && TryMergeDefaults(name, state.GodotProjectRoot, current, out var merged))
                    {
                        logger?.LogDebug(
                            "[Injection] Merge-if-empty {Param}={Value} ({Tool}) via contract.",
                            name, merged, functionName);
                        context.Arguments[name] = merged!;
                    }
                    continue;

                case ParameterPolicy.Exclude:
                    logger?.LogDebug(
                        "[Injection] Excluded {Param} ({Tool}) — node-tree path, skipping.",
                        name, functionName);
                    continue;

                case ParameterPolicy.NormalizePath:
                    // Handled in the normalization sweep at the end; nothing to do here.
                    continue;

                case ParameterPolicy.Skip:
                    continue;

                case ParameterPolicy.Fallback:
                default:
                    // Legacy heuristic: force-set project root / create-project name, else fill project root if empty.
                    if (TryForceProjectRoot(name, state, out var fallbackRoot))
                    {
                        context.Arguments[name] = fallbackRoot!;
                        continue;
                    }

                    if (GodotToolContractRegistry.IsCreateGodotProject(functionName)
                        && TryForceCreateProjectDisplayName(name, state, out var fallbackNm))
                    {
                        context.Arguments[name] = fallbackNm!;
                        continue;
                    }

                    if (!JsonOptionValue.IsNullOrEmptyStringLike(current))
                    {
                        continue;
                    }

                    if (TryMergeDefaults(name, state.GodotProjectRoot, current, out var fallbackMerged))
                    {
                        context.Arguments[name] = fallbackMerged!;
                    }

                    break;
            }
        }

        // ── Pass 2: argument keys (JsonElement placeholders) ─────────────────────
        // The model/tool bridge may supply keys that still need defaults even when
        // metadata iteration order or naming differs slightly.
        foreach (var kvp in context.Arguments)
        {
            var policy = GodotToolContractRegistry.Resolve(pluginName, functionName, kvp.Key);

            if (policy is ParameterPolicy.Exclude or ParameterPolicy.Skip or ParameterPolicy.NormalizePath)
            {
                continue;
            }

            if (policy == ParameterPolicy.MergeDefaultFileName)
            {
                if (JsonOptionValue.IsNullOrEmptyStringLike(kvp.Value)
                    && !string.IsNullOrWhiteSpace(state.DefaultFileName))
                {
                    context.Arguments[kvp.Key] = state.DefaultFileName;
                }
                continue;
            }

            if (policy == ParameterPolicy.ForceProject)
            {
                if (TryForceProjectRoot(kvp.Key, state, out var forced))
                {
                    logger?.LogDebug(
                        "[Injection] Force-set (arg) {Param}={Value} ({Tool}) via contract.",
                        kvp.Key, forced, functionName);
                    context.Arguments[kvp.Key] = forced!;
                }
                else if (GodotToolContractRegistry.IsCreateGodotProject(functionName)
                         && TryForceCreateProjectDisplayName(kvp.Key, state, out var forcedNm))
                {
                    logger?.LogDebug(
                        "[Injection] Force-set (arg) {Param}={Value} ({Tool}) via contract (create project).",
                        kvp.Key, forcedNm, functionName);
                    context.Arguments[kvp.Key] = forcedNm!;
                }
                continue;
            }

            // Fallback: existing heuristic for non-Godot or unregistered arg keys.
            if (TryForceProjectRoot(kvp.Key, state, out var fallbackForced))
            {
                context.Arguments[kvp.Key] = fallbackForced!;
                continue;
            }

            if (GodotToolContractRegistry.IsCreateGodotProject(functionName)
                && TryForceCreateProjectDisplayName(kvp.Key, state, out var fallbackCreateNm))
            {
                context.Arguments[kvp.Key] = fallbackCreateNm!;
                continue;
            }

            if (!IsProjectRootParameter(kvp.Key))
            {
                continue;
            }

            if (!JsonOptionValue.IsNullOrEmptyStringLike(kvp.Value))
            {
                continue;
            }

            if (TryMergeDefaults(kvp.Key, state.GodotProjectRoot, kvp.Value, out var fallbackMerged))
            {
                context.Arguments[kvp.Key] = fallbackMerged!;
            }
        }

        // ── Pass 3: path normalization ────────────────────────────────────────────
        // Normalize relative filesystem-like paths against the selected project root
        // so tools that don't take projectRootPath still operate inside the intended project.
        if (string.IsNullOrWhiteSpace(state.GodotProjectRoot))
        {
            return;
        }

        foreach (var kvp in context.Arguments)
        {
            if (!TryNormalizePathLikeArgument(kvp.Key, kvp.Value, state.GodotProjectRoot!, out var normalized))
            {
                continue;
            }

            logger?.LogDebug(
                "[Injection] Normalized path {Param}: '{Old}' → '{New}' ({Tool}).",
                kvp.Key, kvp.Value, normalized, functionName);
            context.Arguments[kvp.Key] = normalized!;
        }
    }

    /// <summary>
    /// Exposed for unit tests — maps a single parameter name to a default string when appropriate.
    /// Only fills when <paramref name="currentValue"/> is null or empty.
    /// </summary>
    /// <param name="parameterName">The MCP parameter name to evaluate.</param>
    /// <param name="turnProjectRoot">Project root directory for the current turn.</param>
    /// <param name="currentValue">The value currently held in the invocation arguments.</param>
    /// <param name="mergedValue">Receives the default value when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a default was applied.</returns>
    internal static bool TryMergeDefaults(
        string parameterName,
        string? turnProjectRoot,
        object? currentValue,
        out object? mergedValue)
    {
        mergedValue = null;
        if (!JsonOptionValue.IsNullOrEmptyStringLike(currentValue))
        {
            return false;
        }

        if (IsProjectRootParameter(parameterName) && !string.IsNullOrEmpty(turnProjectRoot))
        {
            mergedValue = turnProjectRoot;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes a relative filesystem-like path argument against the project root.
    /// Absolute paths, <c>res://</c> paths, and node-tree paths are left unchanged.
    /// </summary>
    /// <param name="parameterName">The parameter name to classify.</param>
    /// <param name="currentValue">Current argument value (string or <see cref="System.Text.Json.JsonElement"/>).</param>
    /// <param name="projectRoot">The validated Godot project root directory.</param>
    /// <param name="normalizedValue">Receives the combined path when normalization is applied.</param>
    /// <returns><see langword="true"/> when a normalized path was produced.</returns>
    internal static bool TryNormalizePathLikeArgument(
        string parameterName,
        object? currentValue,
        string projectRoot,
        out object? normalizedValue)
    {
        normalizedValue = null;
        if (!IsPathLikeParameter(parameterName))
        {
            return false;
        }

        var raw = JsonOptionValue.AsTrimmedString(currentValue);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Godot resource paths should stay untouched.
        if (raw.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Absolute paths are already explicit and should not be rewritten.
        if (Path.IsPathRooted(raw))
        {
            return false;
        }

        // Keep node-path-like arguments out of this branch; only filesystem-like params should reach here.
        var relative = raw.TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        normalizedValue = Path.Combine(projectRoot, relative);
        return true;
    }

    /// <summary>
    /// Forces a project-root path parameter to the active turn's project root.
    /// </summary>
    internal static bool TryForceProjectRoot(
        string parameterName,
        GodotSkTurnContext.TurnState state,
        out object? forcedValue)
    {
        forcedValue = null;
        if (IsProjectRootParameter(parameterName) && !string.IsNullOrWhiteSpace(state.GodotProjectRoot))
        {
            forcedValue = state.GodotProjectRoot;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Forces create-project display name parameters for <c>godot_create_godot_project</c> only.
    /// </summary>
    internal static bool TryForceCreateProjectDisplayName(
        string parameterName,
        GodotSkTurnContext.TurnState state,
        out object? forcedValue)
    {
        forcedValue = null;
        if (string.IsNullOrWhiteSpace(state.ProjectName))
        {
            return false;
        }

        if (IsCreateProjectDisplayNameParameter(parameterName))
        {
            forcedValue = state.ProjectName;
            return true;
        }

        return false;
    }

    private static bool IsCreateProjectDisplayNameParameter(string name) =>
        name.Equals("projectName", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_name", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godot_project_name", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godotProjectName", StringComparison.OrdinalIgnoreCase)
        || name.Equals("name", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project", StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectRootParameter(string name) =>
        name.Equals("projectPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("projectRootPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_root_path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("rootPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("root_path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("projectDirectory", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_directory", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godot_project_path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godotProjectRoot", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godot_project_root", StringComparison.OrdinalIgnoreCase)
        || name.Equals("GodotProjectPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("targetPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("targetDirectory", StringComparison.OrdinalIgnoreCase)
        || name.Equals("dir", StringComparison.OrdinalIgnoreCase)
        || name.Equals("path", StringComparison.OrdinalIgnoreCase);

    private static bool IsPathLikeParameter(string name)
    {
        if (IsNodeTreePathParameter(name))
        {
            return false;
        }

        if (name.Equals("directory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.EndsWith("Path", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("_path", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNodeTreePathParameter(string name) =>
        name.Equals("nodePath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("parentPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("newParentPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("lightPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("bodyPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("shapePath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("cameraPath", StringComparison.OrdinalIgnoreCase);
}

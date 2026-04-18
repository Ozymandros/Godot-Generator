#nullable enable
using System.Collections.Frozen;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Defines how the injection layer should treat a single Godot MCP tool parameter.
/// </summary>
internal enum ParameterPolicy
{
    /// <summary>
    /// Parameter belongs to an unknown or non-Godot tool.
    /// Fall back to name-based heuristics for injection decisions.
    /// </summary>
    Fallback,

    /// <summary>
    /// Always overwrite the parameter value with the selected project context
    /// (project root path or project name), even when the model supplied a value.
    /// </summary>
    ForceProject,

    /// <summary>
    /// Fill from project context only when the current argument value is null or empty.
    /// </summary>
    MergeIfEmpty,

    /// <summary>
    /// When empty, merge <see cref="GodotSkTurnContext.TurnState.DefaultFileName"/> into MCP 1.5 <c>fileName</c> parameters.
    /// </summary>
    MergeDefaultFileName,

    /// <summary>
    /// Resolve relative filesystem paths against the selected project root.
    /// Absolute paths and Godot resource paths (<c>res://</c>) are left unchanged.
    /// </summary>
    NormalizePath,

    /// <summary>
    /// Explicitly excluded from injection — typically node-tree paths that must not be
    /// rewritten as filesystem paths (e.g. <c>nodePath</c>, <c>parentPath</c>).
    /// </summary>
    Exclude,

    /// <summary>
    /// Skip without logging. Used for non-path parameters of known Godot tools that
    /// require no injection logic.
    /// </summary>
    Skip,
}

/// <summary>
/// Contract registry that resolves the <see cref="ParameterPolicy"/> for a given Godot MCP
/// tool parameter, replacing broad name heuristics with explicit per-parameter contracts.
/// </summary>
/// <remarks>
/// Non-Godot tool parameters always receive <see cref="ParameterPolicy.Fallback"/> so that
/// existing name-heuristic logic continues to apply for third-party plugins.
/// </remarks>
internal static class GodotToolContractRegistry
{
    // Parameters that always receive the selected project root path (force-set).
    private static readonly FrozenSet<string> s_projectRootParams = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "projectPath",
        "project_path",
        "projectRootPath",
        "project_root_path",
        "rootPath",
        "root_path",
        "projectDirectory",
        "project_directory",
        "godot_project_path",
        "godotProjectRoot",
        "godot_project_root",
        "GodotProjectPath",
        "path");

    // Parameters that always receive the selected project name (force-set).
    private static readonly FrozenSet<string> s_projectNameParams = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "projectName",
        "project_name",
        "godotProjectName",
        "godot_project_name");

    // MCP 1.5 project-relative file identifiers (scene/resource/script); merge default when empty.
    private static readonly FrozenSet<string> s_fileNameParams = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "fileName",
        "file_name",
        "sceneFileName",
        "scene_file_name",
        "scriptFileName",
        "script_file_name",
        "resourceFileName",
        "resource_file_name");

    // Node-tree path parameters that must never be rewritten as filesystem paths.
    private static readonly FrozenSet<string> s_nodeTreeParams = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "nodePath",
        "parentPath",
        "newParentPath",
        "lightPath",
        "bodyPath",
        "shapePath",
        "cameraPath");

    /// <summary>
    /// Returns <see langword="true"/> when the plugin name or function name identifies a
    /// Godot MCP tool governed by this contract registry.
    /// </summary>
    /// <param name="pluginName">SK plugin name (e.g. <c>"godot"</c>).</param>
    /// <param name="functionName">SK function name (e.g. <c>"godot_create_script"</c>).</param>
    internal static bool IsGodotTool(string? pluginName, string? functionName) =>
        string.Equals(pluginName, "godot", StringComparison.OrdinalIgnoreCase) ||
        (functionName?.StartsWith("godot_", StringComparison.OrdinalIgnoreCase) ?? false);

    internal static bool IsCreateGodotProject(string? functionName) =>
        string.Equals(functionName, "godot_create_godot_project", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the injection policy for a single parameter of a tool invocation.
    /// </summary>
    /// <param name="pluginName">SK plugin name as registered in the kernel.</param>
    /// <param name="functionName">SK function name for the tool being invoked.</param>
    /// <param name="paramName">Parameter name as declared in the MCP tool schema.</param>
    /// <returns>
    /// The <see cref="ParameterPolicy"/> that governs injection for this parameter.
    /// Returns <see cref="ParameterPolicy.Fallback"/> for non-Godot tools.
    /// </returns>
    internal static ParameterPolicy Resolve(string? pluginName, string? functionName, string paramName)
    {
        if (!IsGodotTool(pluginName, functionName))
        {
            return ParameterPolicy.Fallback;
        }

        // Node-tree paths must never be rewritten as filesystem paths.
        if (s_nodeTreeParams.Contains(paramName))
        {
            return ParameterPolicy.Exclude;
        }

        // Project root parameters are always force-set from the selected project context.
        if (s_projectRootParams.Contains(paramName))
        {
            return ParameterPolicy.ForceProject;
        }

        // MCP 1.5: merge optional default fileName when the model omits it.
        if (s_fileNameParams.Contains(paramName))
        {
            return ParameterPolicy.MergeDefaultFileName;
        }

        // Function-aware contract: create_godot_project uses generic name/dir keys.
        if (IsCreateGodotProject(functionName))
        {
            if (string.Equals(paramName, "name", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paramName, "project", StringComparison.OrdinalIgnoreCase)
                || s_projectNameParams.Contains(paramName))
            {
                return ParameterPolicy.ForceProject;
            }

            if (string.Equals(paramName, "directory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paramName, "dir", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paramName, "targetDirectory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paramName, "targetPath", StringComparison.OrdinalIgnoreCase))
            {
                return ParameterPolicy.ForceProject;
            }
        }
        else if (s_projectNameParams.Contains(paramName))
        {
            // projectName is only meaningful for create_godot_project; do not inject elsewhere.
            return ParameterPolicy.Skip;
        }

        // Additional high-confidence fallback for project-root keys only.
        if (LooksLikeProjectRootParameter(paramName))
        {
            return ParameterPolicy.ForceProject;
        }

        // Remaining *Path or *_path parameters (that are not node-tree paths) should be
        // normalized against the selected project root when they carry a relative value.
        if (paramName.EndsWith("Path", StringComparison.OrdinalIgnoreCase)
            || paramName.EndsWith("_path", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paramName, "directory", StringComparison.OrdinalIgnoreCase))
        {
            return ParameterPolicy.NormalizePath;
        }

        return ParameterPolicy.Skip;
    }

    private static bool LooksLikeProjectRootParameter(string paramName) =>
        paramName.Contains("project", StringComparison.OrdinalIgnoreCase)
        && (paramName.Contains("path", StringComparison.OrdinalIgnoreCase)
            || paramName.Contains("root", StringComparison.OrdinalIgnoreCase)
            || paramName.Contains("dir", StringComparison.OrdinalIgnoreCase)
            || paramName.Contains("directory", StringComparison.OrdinalIgnoreCase));
}

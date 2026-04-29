#nullable enable
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Public entry point for filling Godot MCP tool parameters (project root / name) when arguments are missing.
/// Used by <see cref="AiOrchestrationService"/> (via <see cref="GodotSkTurnContext"/>) and by the wizard
/// orchestration pipeline with explicit path/name from <see cref="GodotGenerator.Application.Dtos.WizardRequest"/>.
/// </summary>
public static class GodotKernelToolArgumentInjection
{
    /// <summary>
    /// Injects default project path and/or name into <paramref name="context"/> for parameters that use
    /// known MCP schema names (e.g. <c>projectPath</c>, <c>projectRootPath</c>) when the model left them null or empty.
    /// Contract-marked parameters (Godot tools) are always force-set from the project context regardless
    /// of what the model supplied; non-Godot tool parameters use name-based heuristics.
    /// </summary>
    /// <param name="context">Current kernel function invocation.</param>
    /// <param name="godotProjectRoot">Optional validated Godot project root directory.</param>
    /// <param name="projectName">Optional project display name (used for <c>create_godot_project</c> only).</param>
    /// <param name="defaultFileName">Optional default <c>fileName</c> (MCP 1.5) merged when a tool's <c>fileName</c> is empty.</param>
    /// <param name="logger">Optional logger; when supplied, debug entries are emitted for force-set, normalize, and skip decisions.</param>
    public static void ApplyProjectDefaults(
        FunctionInvocationContext context,
        string? godotProjectRoot,
        string? projectName,
        string? defaultFileName = null,
        ILogger? logger = null)
    {
        var root = string.IsNullOrWhiteSpace(godotProjectRoot) ? null : godotProjectRoot.Trim();
        var name = string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        var file = string.IsNullOrWhiteSpace(defaultFileName) ? null : defaultFileName.Trim();
        if (root is null && name is null && file is null)
        {
            return;
        }

        GodotSkToolArgumentInjection.Apply(
            context,
            new GodotSkTurnContext.TurnState(root, name, file),
            logger);
    }
}

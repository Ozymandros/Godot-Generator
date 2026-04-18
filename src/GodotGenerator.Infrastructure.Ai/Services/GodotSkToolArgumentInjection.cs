#nullable enable
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Fills common Godot MCP tool parameters (project path / name) when the model omits them, using the active
/// request context from <see cref="GodotSkTurnContext"/>.
/// </summary>
internal static class GodotSkToolArgumentInjection
{
    internal static void Apply(FunctionInvocationContext context, GodotSkTurnContext.TurnState state)
    {
        foreach (var param in context.Function.Metadata.Parameters)
        {
            var name = param.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            context.Arguments.TryGetValue(name, out var current);
            if (!ShouldReplaceWithDefault(current))
            {
                continue;
            }

            if (TryMergeDefaults(name, state.GodotProjectRoot, state.ProjectName, current, out var merged))
            {
                context.Arguments[name] = merged!;
            }
        }
    }

    /// <summary>
    /// Exposed for unit tests — maps a single parameter name to a default string when appropriate.
    /// </summary>
    internal static bool TryMergeDefaults(
        string parameterName,
        string? turnProjectRoot,
        string? turnProjectName,
        object? currentValue,
        out object? mergedValue)
    {
        mergedValue = null;
        if (!ShouldReplaceWithDefault(currentValue))
        {
            return false;
        }

        if (IsProjectRootParameter(parameterName) && !string.IsNullOrEmpty(turnProjectRoot))
        {
            mergedValue = turnProjectRoot;
            return true;
        }

        if (IsProjectNameParameter(parameterName) && !string.IsNullOrEmpty(turnProjectName))
        {
            mergedValue = turnProjectName;
            return true;
        }

        return false;
    }

    private static bool ShouldReplaceWithDefault(object? raw)
    {
        if (raw is null)
        {
            return true;
        }

        return raw is string s && string.IsNullOrWhiteSpace(s);
    }

    private static bool IsProjectRootParameter(string name) =>
        name.Equals("projectPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("projectRootPath", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_root_path", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godot_project_path", StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectNameParameter(string name) =>
        name.Equals("projectName", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project_name", StringComparison.OrdinalIgnoreCase)
        || name.Equals("godot_project_name", StringComparison.OrdinalIgnoreCase);
}

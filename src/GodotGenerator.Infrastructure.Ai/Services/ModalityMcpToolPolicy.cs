#nullable enable
using GodotGenerator.Application.Orchestration;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Maps generation modality keys to Godot MCP Semantic Kernel function name patterns.
/// Expanded tool names typically look like <c>godot_light_list</c>, <c>godot_camera_create</c>, etc.
/// See <see href="https://github.com/Ozymandros/Godot-MCP-Server">Godot-MCP-Server</see> tool families.
/// </summary>
public static class ModalityMcpToolPolicy
{
    /// <summary>Returns true when the modality should narrow registered MCP tools.</summary>
    public static bool ShouldApplyFiltering(string? modalityKey)
    {
        if (string.IsNullOrWhiteSpace(modalityKey))
        {
            return false;
        }

        return TryGetFilterTokens(modalityKey, out _);
    }

    /// <summary>
    /// Returns whether a kernel function should remain registered for the given modality.
    /// When <paramref name="modalityKey"/> does not map to a filter, returns true for every function.
    /// </summary>
    public static bool IsFunctionAllowed(string? modalityKey, string pluginName, string functionName)
    {
        if (!TryGetFilterTokens(modalityKey, out var tokens) || tokens is null || tokens.Length == 0)
        {
            return true;
        }

        var blob = $"{pluginName}::{functionName}".ToLowerInvariant();
        if (IsSharedDiagnosticFunction(blob))
        {
            return true;
        }

        foreach (var t in tokens)
        {
            if (blob.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSharedDiagnosticFunction(string pluginAndFunctionLower)
    {
        // Keep health/discovery helpers if the MCP host exposes them under any name.
        ReadOnlySpan<string> markers =
        [
            "health",
            "server_info",
            "serverinfo",
            "capabilit",
            "ping",
            "initialize",
            "list_tool",
            "tools_list",
        ];

        foreach (var m in markers)
        {
            if (pluginAndFunctionLower.Contains(m, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFilterTokens(string? modalityKey, out string[]? tokens)
    {
        tokens = null;
        if (string.IsNullOrWhiteSpace(modalityKey))
        {
            return false;
        }

        var m = EffectiveSelectionPolicy.NormalizeModality(modalityKey.Trim());
        tokens = m switch
        {
            "godot-lighting" => ["light"],
            "godot-camera" => ["camera"],
            "godot-shaders" => ["shader", "resource", "script", "reimport", "material"],
            "godot-signals" => ["signal", "scene", "script", "connect"],
            "godot-nodes" => ["scene", "node"],
            "godot-ui" => ["ui", "control", "theme"],
            "godot-physics" => ["physics", "body", "shape", "collision"],
            "scenes" => ["scene"],
            "godot-project" => ["project", "godot_project", "autoload", "plugin"],
            _ => null,
        };

        return tokens is not null;
    }
}

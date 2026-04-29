#nullable enable
using System.Text.Json;
using GodotGenerator.Infrastructure.Ai.Services;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Validates <see cref="GodotToolContractRegistry"/> policy resolution and the injection-helper
/// methods on <see cref="GodotSkToolArgumentInjection"/> for edge cases that arise specifically
/// from the AI orchestration path (JsonElement-backed arguments, force-over-fill semantics).
/// </summary>
public sealed class AiOrchestrationServiceToolFilterTests
{
    // ── GodotToolContractRegistry.IsGodotTool ─────────────────────────────────

    [Theory]
    [InlineData("godot", "anything")]
    [InlineData("GODOT", "anything")]
    [InlineData("Godot", "godot_create_script")]
    public void IsGodotTool_returns_true_when_plugin_name_is_godot(string pluginName, string funcName)
    {
        Assert.True(GodotToolContractRegistry.IsGodotTool(pluginName, funcName));
    }

    [Theory]
    [InlineData(null, "godot_create_script")]
    [InlineData("", "godot_add_node")]
    [InlineData("other", "godot_list_files")]
    public void IsGodotTool_returns_true_when_function_name_starts_with_godot_prefix(
        string? pluginName, string funcName)
    {
        Assert.True(GodotToolContractRegistry.IsGodotTool(pluginName, funcName));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "create_scene")]
    [InlineData("elevenlabs", "tts")]
    [InlineData("imagegen", "generate_image")]
    public void IsGodotTool_returns_false_for_non_godot_plugin_and_function(
        string? pluginName, string? funcName)
    {
        Assert.False(GodotToolContractRegistry.IsGodotTool(pluginName, funcName));
    }

    // ── GodotToolContractRegistry.Resolve — ForceProject ─────────────────────

    [Theory]
    [InlineData("projectPath")]
    [InlineData("project_path")]
    [InlineData("projectRootPath")]
    [InlineData("project_root_path")]
    [InlineData("rootPath")]
    [InlineData("root_path")]
    [InlineData("projectDirectory")]
    [InlineData("project_directory")]
    [InlineData("godot_project_path")]
    [InlineData("godotProjectRoot")]
    [InlineData("godot_project_root")]
    [InlineData("GodotProjectPath")]
    [InlineData("path")]
    public void Resolve_returns_ForceProject_for_project_root_params_on_godot_tool(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", "godot_test", paramName);
        Assert.Equal(ParameterPolicy.ForceProject, policy);
    }

    [Theory]
    [InlineData("projectName")]
    [InlineData("project_name")]
    [InlineData("godotProjectName")]
    [InlineData("godot_project_name")]
    public void Resolve_returns_Skip_for_project_name_params_when_not_create_project(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", "godot_test", paramName);
        Assert.Equal(ParameterPolicy.Skip, policy);
    }

    [Theory]
    [InlineData("fileName")]
    [InlineData("sceneFileName")]
    [InlineData("script_file_name")]
    public void Resolve_returns_MergeDefaultFileName_for_file_name_params(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", "godot_create_scene", paramName);
        Assert.Equal(ParameterPolicy.MergeDefaultFileName, policy);
    }

    [Theory]
    [InlineData("godot_create_godot_project", "name")]
    [InlineData("godot_create_godot_project", "project")]
    [InlineData("godot_create_godot_project", "directory")]
    [InlineData("godot_create_godot_project", "targetDirectory")]
    [InlineData("godot_create_godot_project", "targetPath")]
    public void Resolve_returns_ForceProject_for_create_project_function_specific_params(string functionName, string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", functionName, paramName);
        Assert.Equal(ParameterPolicy.ForceProject, policy);
    }

    // ── GodotToolContractRegistry.Resolve — Exclude ───────────────────────────

    [Theory]
    [InlineData("nodePath")]
    [InlineData("parentPath")]
    [InlineData("newParentPath")]
    [InlineData("lightPath")]
    [InlineData("bodyPath")]
    [InlineData("shapePath")]
    [InlineData("cameraPath")]
    public void Resolve_returns_Exclude_for_node_tree_params_on_godot_tool(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", "godot_test", paramName);
        Assert.Equal(ParameterPolicy.Exclude, policy);
    }

    // ── GodotToolContractRegistry.Resolve — NormalizePath ────────────────────

    [Theory]
    [InlineData("scenePath")]
    [InlineData("scriptPath")]
    [InlineData("outputPath")]
    [InlineData("resourcePath")]
    [InlineData("asset_path")]
    [InlineData("directory")]
    public void Resolve_returns_NormalizePath_for_path_like_params_on_godot_tool(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", "godot_test", paramName);
        Assert.Equal(ParameterPolicy.NormalizePath, policy);
    }

    // ── GodotToolContractRegistry.Resolve — Skip ─────────────────────────────

    [Theory]
    [InlineData("enabled")]
    [InlineData("isCSharp")]
    [InlineData("type")]
    [InlineData("count")]
    public void Resolve_returns_Skip_for_non_path_params_on_godot_tool(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("godot", "godot_test", paramName);
        Assert.Equal(ParameterPolicy.Skip, policy);
    }

    // ── GodotToolContractRegistry.Resolve — Fallback ─────────────────────────

    [Theory]
    [InlineData("projectPath")]
    [InlineData("nodePath")]
    [InlineData("scenePath")]
    [InlineData("enabled")]
    public void Resolve_returns_Fallback_for_any_param_on_non_godot_plugin(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve("imagegen", "generate_image", paramName);
        Assert.Equal(ParameterPolicy.Fallback, policy);
    }

    [Theory]
    [InlineData("projectPath")]
    [InlineData("scenePath")]
    public void Resolve_returns_Fallback_for_any_param_when_neither_plugin_nor_function_is_godot(string paramName)
    {
        var policy = GodotToolContractRegistry.Resolve(null, "create_thing", paramName);
        Assert.Equal(ParameterPolicy.Fallback, policy);
    }

    // ── JsonElement edge cases for TryMergeDefaults ───────────────────────────

    [Fact]
    public void TryMergeDefaults_injects_when_current_is_json_null()
    {
        var jsonNull = JsonDocument.Parse("null").RootElement;
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            "projectPath",
            @"C:\game",
            jsonNull,
            out var merged);

        Assert.True(ok);
        Assert.Equal(@"C:\game", merged);
    }

    [Fact]
    public void TryMergeDefaults_injects_when_current_is_json_empty_string()
    {
        var empty = JsonDocument.Parse("\"\"").RootElement;
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            "projectPath",
            @"C:\game",
            empty,
            out var merged);

        Assert.True(ok);
        Assert.Equal(@"C:\game", merged);
    }

    [Fact]
    public void TryMergeDefaults_does_not_inject_when_current_is_json_whitespace_string()
    {
        // Whitespace-only JSON string is treated as empty → injection should apply.
        var ws = JsonDocument.Parse("\"   \"").RootElement;
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            "projectPath",
            @"C:\game",
            ws,
            out var merged);

        // Whitespace is treated as empty by JsonOptionValue.AsTrimmedString → injection applies.
        Assert.True(ok);
        Assert.Equal(@"C:\game", merged);
    }

    [Fact]
    public void TryMergeDefaults_does_not_override_when_current_is_json_nonempty_string()
    {
        var nonEmpty = JsonDocument.Parse("\"D:\\\\other\"").RootElement;
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            "projectPath",
            @"C:\game",
            nonEmpty,
            out var merged);

        Assert.False(ok);
        Assert.Null(merged);
    }

    // ── TryForceProjectRoot — force semantics ───────────────────────────────

    [Fact]
    public void TryForceProjectRoot_forces_projectPath_even_when_value_exists()
    {
        var state = new GodotSkTurnContext.TurnState(@"C:\real-game", "RealGame", null);
        var ok = GodotSkToolArgumentInjection.TryForceProjectRoot("projectPath", state, out var forced);

        Assert.True(ok);
        Assert.Equal(@"C:\real-game", forced);
    }

    [Fact]
    public void TryForceProjectRoot_forces_godot_project_path_snake_case()
    {
        var state = new GodotSkTurnContext.TurnState(@"C:\real-game", null, null);
        var ok = GodotSkToolArgumentInjection.TryForceProjectRoot("godot_project_path", state, out var forced);

        Assert.True(ok);
        Assert.Equal(@"C:\real-game", forced);
    }

    [Fact]
    public void TryForceProjectRoot_returns_false_when_state_has_no_project_root()
    {
        var state = new GodotSkTurnContext.TurnState(null, null, null);
        var ok = GodotSkToolArgumentInjection.TryForceProjectRoot("projectPath", state, out var forced);

        Assert.False(ok);
        Assert.Null(forced);
    }

    [Fact]
    public void TryForceProjectRoot_returns_false_for_unrecognized_param()
    {
        var state = new GodotSkTurnContext.TurnState(@"C:\game", "MyGame", null);
        var ok = GodotSkToolArgumentInjection.TryForceProjectRoot("enabled", state, out var forced);

        Assert.False(ok);
        Assert.Null(forced);
    }

    // ── NormalizePath — JsonElement relative path ─────────────────────────────

    [Fact]
    public void TryNormalizePathLikeArgument_normalizes_json_element_relative_path()
    {
        var rel = JsonDocument.Parse("\"scripts/Enemy.gd\"").RootElement;
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "scriptPath",
            rel,
            @"C:\game",
            out var normalized);

        Assert.True(ok);
        Assert.Equal(System.IO.Path.Combine(@"C:\game", "scripts", "Enemy.gd"), normalized);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_does_not_normalize_json_element_absolute_path()
    {
        var abs = JsonDocument.Parse("\"C:\\\\MyGame\\\\scenes\\\\Main.tscn\"").RootElement;
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "scenePath",
            abs,
            @"C:\game",
            out var normalized);

        Assert.False(ok);
        Assert.Null(normalized);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_does_not_normalize_excluded_node_tree_paths()
    {
        // nodePath is a node-tree path — must never be normalized.
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "nodePath",
            "/root/Player/Sword",
            @"C:\game",
            out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_does_not_normalize_res_protocol_paths()
    {
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "resourcePath",
            "res://scenes/Main.tscn",
            @"C:\game",
            out _);

        Assert.False(ok);
    }
}

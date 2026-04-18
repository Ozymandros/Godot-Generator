#nullable enable
using System.IO;
using System.Text.Json;
using GodotGenerator.Infrastructure.Ai.Services;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Unit tests for <see cref="GodotSkToolArgumentInjection"/> parameter matching.
/// </summary>
public sealed class GodotSkToolArgumentInjectionTests
{
    [Theory]
    [InlineData("projectPath")]
    [InlineData("project_path")]
    [InlineData("projectRootPath")]
    [InlineData("godot_project_path")]
    [InlineData("godotProjectRoot")]
    [InlineData("godot_project_root")]
    [InlineData("GodotProjectPath")]
    [InlineData("rootPath")]
    [InlineData("projectDirectory")]
    [InlineData("path")]
    public void TryMergeDefaults_injects_project_root_when_missing(string paramName)
    {
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            paramName,
            @"C:\game",
            null,
            out var merged);

        Assert.True(ok);
        Assert.Equal(@"C:\game", merged);
    }

    [Theory]
    [InlineData("projectName")]
    [InlineData("project_name")]
    [InlineData("godotProjectName")]
    [InlineData("godot_project_name")]
    [InlineData("name")]
    [InlineData("project")]
    public void TryMergeDefaults_does_not_merge_project_name_merge_is_create_project_only(string paramName)
    {
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            paramName,
            @"C:\game",
            null,
            out var merged);

        Assert.False(ok);
        Assert.Null(merged);
    }

    [Theory]
    [InlineData("projectName")]
    [InlineData("name")]
    public void TryForceCreateProjectDisplayName_forces_when_project_name_set(string paramName)
    {
        var state = new GodotSkTurnContext.TurnState(@"C:\game", "MyGame", null);
        var ok = GodotSkToolArgumentInjection.TryForceCreateProjectDisplayName(paramName, state, out var forced);

        Assert.True(ok);
        Assert.Equal("MyGame", forced);
    }

    [Fact]
    public void TryMergeDefaults_does_not_override_nonempty_string()
    {
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            "projectPath",
            @"C:\game",
            @"D:\other",
            out var merged);

        Assert.False(ok);
        Assert.Null(merged);
    }

    [Theory]
    [InlineData("projectPath")]
    [InlineData("projectRootPath")]
    [InlineData("rootPath")]
    [InlineData("path")]
    public void TryMergeDefaults_override_is_handled_by_apply_not_merge_defaults(string paramName)
    {
        // TryMergeDefaults keeps "fill when missing" semantics; force-overwrite is validated
        // at the higher Apply() level.
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            paramName,
            @"C:\game",
            @"C:\wrong",
            out var merged);

        Assert.False(ok);
        Assert.Null(merged);
    }

    [Theory]
    [InlineData("projectPath", @"C:\game")]
    [InlineData("projectRootPath", @"C:\game")]
    [InlineData("path", @"C:\game")]
    [InlineData("targetPath", @"C:\game")]
    [InlineData("dir", @"C:\game")]
    public void TryForceProjectRoot_returns_forced_value_for_project_root_params(string paramName, string expected)
    {
        var state = new GodotSkTurnContext.TurnState(@"C:\game", "MyGame", null);
        var ok = GodotSkToolArgumentInjection.TryForceProjectRoot(paramName, state, out var forced);

        Assert.True(ok);
        Assert.Equal(expected, forced);
    }

    [Theory]
    [InlineData("projectName", "MyGame")]
    [InlineData("godot_project_name", "MyGame")]
    [InlineData("name", "MyGame")]
    [InlineData("project", "MyGame")]
    public void TryForceCreateProjectDisplayName_returns_forced_value(string paramName, string expected)
    {
        var state = new GodotSkTurnContext.TurnState(@"C:\game", "MyGame", null);
        var ok = GodotSkToolArgumentInjection.TryForceCreateProjectDisplayName(paramName, state, out var forced);

        Assert.True(ok);
        Assert.Equal(expected, forced);
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

    [Fact]
    public void TryNormalizePathLikeArgument_combines_relative_scene_path_with_project_root()
    {
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "scenePath",
            "scenes/Main.tscn",
            @"C:\game",
            out var normalized);

        Assert.True(ok);
        Assert.Equal(Path.Combine(@"C:\game", "scenes", "Main.tscn"), normalized);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_does_not_change_res_path()
    {
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "resourcePath",
            "res://scripts/Player.gd",
            @"C:\game",
            out var normalized);

        Assert.False(ok);
        Assert.Null(normalized);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_does_not_change_node_path_argument()
    {
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "nodePath",
            "/root/Player",
            @"C:\game",
            out var normalized);

        Assert.False(ok);
        Assert.Null(normalized);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_normalizes_generic_path_parameter()
    {
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "outputPath",
            "generated/scripts/Enemy.gd",
            @"C:\game",
            out var normalized);

        Assert.True(ok);
        Assert.Equal(Path.Combine(@"C:\game", "generated", "scripts", "Enemy.gd"), normalized);
    }

    [Fact]
    public void TryNormalizePathLikeArgument_does_not_change_parent_path_argument()
    {
        var ok = GodotSkToolArgumentInjection.TryNormalizePathLikeArgument(
            "parentPath",
            "/root/Main",
            @"C:\game",
            out var normalized);

        Assert.False(ok);
        Assert.Null(normalized);
    }

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
        Assert.Equal(Path.Combine(@"C:\game", "scripts", "Enemy.gd"), normalized);
    }
}

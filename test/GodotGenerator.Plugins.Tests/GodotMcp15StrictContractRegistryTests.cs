#nullable enable
using GodotGenerator.Infrastructure.Ai.Services;
using Xunit;

namespace GodotGenerator.Plugins.Tests;

/// <summary>
/// Guards the strict MCP 1.5.0 injection contract: <c>projectPath</c> everywhere,
/// <c>fileName</c> merge defaults for file-oriented params, and <c>projectName</c>
/// forcing only for <c>godot_create_godot_project</c>.
/// </summary>
public sealed class GodotMcp15StrictContractRegistryTests
{
    private const string GodotPlugin = "godot";

    [Theory]
    [InlineData("projectPath")]
    [InlineData("project_path")]
    [InlineData("godot_project_path")]
    public void Godot_tools_force_project_root_for_path_aliases(string param)
    {
        var p = GodotToolContractRegistry.Resolve(GodotPlugin, "godot_get_server_info", param);
        Assert.Equal(ParameterPolicy.ForceProject, p);
    }

    [Theory]
    [InlineData("fileName")]
    [InlineData("scene_file_name")]
    [InlineData("scriptFileName")]
    public void Godot_tools_merge_default_file_name_for_file_oriented_params(string param)
    {
        var p = GodotToolContractRegistry.Resolve(GodotPlugin, "godot_scene_save", param);
        Assert.Equal(ParameterPolicy.MergeDefaultFileName, p);
    }

    [Fact]
    public void Create_godot_project_forces_name_and_directory_style_keys()
    {
        Assert.Equal(ParameterPolicy.ForceProject,
            GodotToolContractRegistry.Resolve(GodotPlugin, "godot_create_godot_project", "name"));
        Assert.Equal(ParameterPolicy.ForceProject,
            GodotToolContractRegistry.Resolve(GodotPlugin, "godot_create_godot_project", "projectName"));
        Assert.Equal(ParameterPolicy.ForceProject,
            GodotToolContractRegistry.Resolve(GodotPlugin, "godot_create_godot_project", "directory"));
        Assert.Equal(ParameterPolicy.ForceProject,
            GodotToolContractRegistry.Resolve(GodotPlugin, "godot_create_godot_project", "targetPath"));
    }

    [Fact]
    public void Non_create_tools_skip_projectName_injection()
    {
        var p = GodotToolContractRegistry.Resolve(GodotPlugin, "godot_scene_list_nodes", "projectName");
        Assert.Equal(ParameterPolicy.Skip, p);
    }

    [Theory]
    [InlineData("nodePath")]
    [InlineData("parentPath")]
    public void Node_tree_paths_remain_excluded(string param)
    {
        var p = GodotToolContractRegistry.Resolve(GodotPlugin, "godot_scene_add_node", param);
        Assert.Equal(ParameterPolicy.Exclude, p);
    }

    [Fact]
    public void Non_godot_plugins_use_fallback_policy()
    {
        var p = GodotToolContractRegistry.Resolve("other", "anything", "projectPath");
        Assert.Equal(ParameterPolicy.Fallback, p);
    }
}

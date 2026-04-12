#nullable enable
using GodotGenerator.Infrastructure.Ai.Services;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Unit tests for <see cref="ModalityMcpToolPolicy"/>.
/// </summary>
public sealed class ModalityMcpToolPolicyTests
{
    [Fact]
    public void ShouldApplyFiltering_is_false_for_null_or_unknown_modality()
    {
        Assert.False(ModalityMcpToolPolicy.ShouldApplyFiltering(null));
        Assert.False(ModalityMcpToolPolicy.ShouldApplyFiltering("   "));
        Assert.False(ModalityMcpToolPolicy.ShouldApplyFiltering("text"));
    }

    [Fact]
    public void ShouldApplyFiltering_is_true_for_godot_lighting()
    {
        Assert.True(ModalityMcpToolPolicy.ShouldApplyFiltering("godot-lighting"));
    }

    [Theory]
    [InlineData("godot-lighting", "p", "godot_light_list", true)]
    [InlineData("godot-lighting", "p", "godot_camera_list", false)]
    [InlineData("godot-camera", "p", "godot_camera_validate", true)]
    [InlineData("godot-nodes", "p", "godot_scene_list_nodes", true)]
    [InlineData(null, "p", "anything", true)]
    public void IsFunctionAllowed_respects_modality_tokens(
        string? modality,
        string plugin,
        string function,
        bool expected)
    {
        Assert.Equal(expected, ModalityMcpToolPolicy.IsFunctionAllowed(modality, plugin, function));
    }
}

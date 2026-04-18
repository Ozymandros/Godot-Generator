#nullable enable
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
    public void TryMergeDefaults_injects_project_root_when_missing(string paramName)
    {
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            paramName,
            @"C:\game",
            "MyGame",
            null,
            out var merged);

        Assert.True(ok);
        Assert.Equal(@"C:\game", merged);
    }

    [Theory]
    [InlineData("projectName")]
    [InlineData("project_name")]
    public void TryMergeDefaults_injects_project_name_when_missing(string paramName)
    {
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            paramName,
            @"C:\game",
            "MyGame",
            null,
            out var merged);

        Assert.True(ok);
        Assert.Equal("MyGame", merged);
    }

    [Fact]
    public void TryMergeDefaults_does_not_override_nonempty_string()
    {
        var ok = GodotSkToolArgumentInjection.TryMergeDefaults(
            "projectPath",
            @"C:\game",
            null,
            @"D:\other",
            out var merged);

        Assert.False(ok);
        Assert.Null(merged);
    }
}

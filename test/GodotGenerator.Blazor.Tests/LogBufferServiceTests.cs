using GodotGenerator.Blazor.Client.Services;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Tests for in-memory log buffer used by the log panel.
/// </summary>
public sealed class LogBufferServiceTests
{
    [Fact]
    public void Append_invokes_Changed()
    {
        var sut = new LogBufferService();
        var calls = 0;
        sut.Changed += () => calls++;

        sut.Append("line");

        Assert.Equal(1, calls);
        Assert.Single(sut.GetSnapshot());
    }

    [Fact]
    public void Append_trims_to_500_lines()
    {
        var sut = new LogBufferService();
        for (var i = 0; i < 520; i++)
        {
            sut.Append($"n{i}");
        }

        Assert.Equal(500, sut.GetSnapshot().Count);
    }
}

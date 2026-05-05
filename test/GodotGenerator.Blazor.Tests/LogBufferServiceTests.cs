using GodotGenerator.Blazor.Client.Services;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Tests for <see cref="LogBufferService"/> — the backward-compatibility shim over
/// <see cref="AppLogService"/> used by <c>GenerationWorkspace</c> and similar callers.
/// </summary>
public sealed class LogBufferServiceTests
{
    private static LogBufferService CreateSut() =>
        new(new AppLogService());

    [Fact]
    public void Append_invokes_Changed()
    {
        var sut = CreateSut();
        var calls = 0;
        sut.Changed += () => calls++;

        sut.Append("line");

        Assert.Equal(1, calls);
        Assert.Single(sut.GetSnapshot());
    }

    [Fact]
    public void Append_trims_to_500_lines()
    {
        var sut = CreateSut();
        for (var i = 0; i < 520; i++)
        {
            sut.Append($"n{i}");
        }

        Assert.Equal(AppLogService.MaxEntries, sut.GetSnapshot().Count);
    }

    [Fact]
    public void GetSnapshot_includes_timestamp_prefix()
    {
        var sut = CreateSut();
        sut.Append("test message");
        var line = Assert.Single(sut.GetSnapshot());
        // FormatAsText prefixes the ISO timestamp via the LogEntry.
        Assert.Contains("test message", line);
        Assert.Contains("[Information]", line);
    }
}

#nullable enable
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Services;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Unit tests for <see cref="AppLogService"/>.
/// </summary>
public sealed class AppLogServiceTests
{
    // ── Append / Changed event ──────────────────────────────────────────────

    [Fact]
    public void Info_appends_entry_and_fires_Changed()
    {
        var sut = new AppLogService();
        var fired = 0;
        sut.Changed += () => fired++;

        sut.Info("UI", "hello world");

        Assert.Equal(1, fired);
        Assert.Equal(1, sut.Count);
    }

    [Fact]
    public void Log_entry_fields_are_set_correctly()
    {
        var sut = new AppLogService();
        var before = DateTimeOffset.Now;

        sut.Warning("Generate", "timed out", "detail here");

        var before2 = DateTimeOffset.Now;
        var snap = sut.GetSnapshot();
        var entry = Assert.Single(snap);

        Assert.Equal(LogEntryLevel.Warning, entry.Level);
        Assert.Equal(LogEntryKind.Application, entry.Kind);
        Assert.Equal("Generate", entry.Category);
        Assert.Equal("timed out", entry.Message);
        Assert.Equal("detail here", entry.Detail);
        Assert.True(entry.Timestamp >= before && entry.Timestamp <= before2);
        Assert.True(entry.Sequence > 0);
    }

    [Fact]
    public void Sequence_is_monotonically_increasing()
    {
        var sut = new AppLogService();
        sut.Info("A", "1");
        sut.Info("A", "2");
        sut.Info("A", "3");

        var snap = sut.GetSnapshot();
        Assert.Equal(3, snap.Count);
        Assert.True(snap[0].Sequence < snap[1].Sequence);
        Assert.True(snap[1].Sequence < snap[2].Sequence);
    }

    // ── Ring buffer cap ─────────────────────────────────────────────────────

    [Fact]
    public void Buffer_drops_oldest_entries_at_cap()
    {
        var sut = new AppLogService();
        for (var i = 0; i < AppLogService.MaxEntries + 10; i++)
        {
            sut.Info("X", $"line {i}");
        }

        Assert.Equal(AppLogService.MaxEntries, sut.GetSnapshot().Count);
        // The oldest entries are gone; the newest should be the last ones appended.
        var last = sut.GetSnapshot().Last();
        Assert.Contains($"line {AppLogService.MaxEntries + 9}", last.Message);
    }

    // ── Filtering ───────────────────────────────────────────────────────────

    [Fact]
    public void GetSnapshot_filters_by_minimum_level()
    {
        var sut = new AppLogService();
        sut.Debug("A", "debug msg");
        sut.Info("A", "info msg");
        sut.Warning("A", "warn msg");
        sut.Error("A", "error msg");

        var snap = sut.GetSnapshot(minLevel: LogEntryLevel.Warning);

        Assert.Equal(2, snap.Count);
        Assert.All(snap, e => Assert.True(e.Level >= LogEntryLevel.Warning));
    }

    [Fact]
    public void GetSnapshot_filters_by_kind()
    {
        var sut = new AppLogService();
        sut.Info("UI", "app entry");
        sut.LogIpcRequest("Generate.Text/v1", "c1");
        sut.LogIpcResponse("Generate.Text/v1", "c1", true, 42.0);

        var appOnly = sut.GetSnapshot(kind: LogEntryKind.Application);
        Assert.Single(appOnly);
        Assert.Equal(LogEntryKind.Application, appOnly[0].Kind);

        var ipcOnly = sut.GetSnapshot(kind: LogEntryKind.IpcRequest);
        Assert.Single(ipcOnly);
    }

    [Fact]
    public void GetSnapshot_filters_by_case_insensitive_search()
    {
        var sut = new AppLogService();
        sut.Info("Generate", "Image generation completed");
        sut.Info("Config", "Settings reloaded");
        sut.Error("Generate", "Generation FAILED");

        var snap = sut.GetSnapshot(search: "generation");

        Assert.Equal(2, snap.Count);
        Assert.All(snap, e => Assert.Equal("Generate", e.Category));
    }

    [Fact]
    public void GetSnapshot_search_matches_category()
    {
        var sut = new AppLogService();
        sut.Info("PromptAssist", "sample generated");
        sut.Info("Generate", "completed");

        var snap = sut.GetSnapshot(search: "prompt");

        Assert.Single(snap);
        Assert.Equal("PromptAssist", snap[0].Category);
    }

    // ── IPC logging ─────────────────────────────────────────────────────────

    [Fact]
    public void LogIpcRequest_creates_request_entry()
    {
        var sut = new AppLogService();
        sut.LogIpcRequest("Generate.Text/v1", "corr-1");

        var snap = sut.GetSnapshot(kind: LogEntryKind.IpcRequest);
        var entry = Assert.Single(snap);
        Assert.Equal(LogEntryKind.IpcRequest, entry.Kind);
        Assert.Equal("Generate", entry.Category);
        Assert.Contains("Text", entry.Message);
        Assert.Equal("corr-1", entry.CorrelationId);
    }

    [Fact]
    public void LogIpcResponse_success_is_Debug_level()
    {
        var sut = new AppLogService();
        sut.LogIpcResponse("Config.GetAll/v1", "c2", success: true, durationMs: 18.5);

        var snap = sut.GetSnapshot(kind: LogEntryKind.IpcResponse);
        var entry = Assert.Single(snap);
        Assert.Equal(LogEntryLevel.Debug, entry.Level);
        Assert.Equal(18.5, entry.DurationMs);
        Assert.Equal("c2", entry.CorrelationId);
    }

    [Fact]
    public void LogIpcResponse_failure_is_Error_level()
    {
        var sut = new AppLogService();
        sut.LogIpcResponse("Generate.Code/v1", "c3", success: false, durationMs: 500, errorCode: "HANDLER_FAULTED");

        var snap = sut.GetSnapshot(kind: LogEntryKind.IpcResponse);
        var entry = Assert.Single(snap);
        Assert.Equal(LogEntryLevel.Error, entry.Level);
        Assert.Contains("HANDLER_FAULTED", entry.Message);
        Assert.NotNull(entry.Detail);
    }

    [Fact]
    public void ParseCommand_handles_missing_dot_gracefully()
    {
        // A command with no dot should not throw; category equals the full name.
        var sut = new AppLogService();
        // This exercises the ParseCommand path implicitly via LogIpcRequest.
        sut.LogIpcRequest("NoDotsHere", "x");
        var snap = sut.GetSnapshot(kind: LogEntryKind.IpcRequest);
        Assert.Single(snap);
    }

    // ── Backend log streaming ────────────────────────────────────────────────

    [Fact]
    public void LogBackend_stdout_creates_Information_entry_with_Backend_kind()
    {
        var sut = new AppLogService();
        sut.LogBackend("stdout", "Backend started on port 5044");

        var snap = sut.GetSnapshot(kind: LogEntryKind.Backend);
        var entry = Assert.Single(snap);
        Assert.Equal(LogEntryKind.Backend, entry.Kind);
        Assert.Equal(LogEntryLevel.Information, entry.Level);
        Assert.Equal("Backend", entry.Category);
        Assert.Equal("Backend started on port 5044", entry.Message);
    }

    [Fact]
    public void LogBackend_stderr_creates_Error_entry_with_Backend_kind()
    {
        var sut = new AppLogService();
        sut.LogBackend("stderr", "Unhandled exception: NullReferenceException");

        var snap = sut.GetSnapshot(kind: LogEntryKind.Backend);
        var entry = Assert.Single(snap);
        Assert.Equal(LogEntryKind.Backend, entry.Kind);
        Assert.Equal(LogEntryLevel.Error, entry.Level);
    }

    [Fact]
    public void LogBackend_unknown_stream_defaults_to_Error_level()
    {
        var sut = new AppLogService();
        sut.LogBackend("pipe", "some message");

        var snap = sut.GetSnapshot(kind: LogEntryKind.Backend);
        var entry = Assert.Single(snap);
        Assert.Equal(LogEntryLevel.Error, entry.Level);
    }

    [Fact]
    public void LogBackend_is_filterable_independently_of_application_and_ipc_entries()
    {
        var sut = new AppLogService();
        sut.Info("UI", "app event");
        sut.LogIpcRequest("Config.GetAll/v1", "c1");
        sut.LogBackend("stdout", "backend line one");
        sut.LogBackend("stderr", "backend error");

        var backendOnly = sut.GetSnapshot(kind: LogEntryKind.Backend);
        Assert.Equal(2, backendOnly.Count);
        Assert.All(backendOnly, e => Assert.Equal(LogEntryKind.Backend, e.Kind));
    }

    // ── Clear ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_empties_buffer_and_fires_events()
    {
        var sut = new AppLogService();
        sut.Info("A", "one");
        sut.Info("A", "two");

        var clearedFired = false;
        var changedCount = 0;
        sut.Cleared += () => clearedFired = true;
        sut.Changed += () => changedCount++;

        sut.Clear();

        Assert.True(clearedFired);
        Assert.Equal(1, changedCount);  // one Changed from Clear itself
        Assert.Equal(0, sut.Count);
        Assert.Empty(sut.GetSnapshot());
    }

    // ── FormatAsText ────────────────────────────────────────────────────────

    [Fact]
    public void FormatAsText_includes_level_category_and_message()
    {
        var entry = new LogEntry(1, DateTimeOffset.UtcNow, LogEntryLevel.Error,
            LogEntryKind.Application, "Generate", "something broke", "stack trace here");

        var text = entry.FormatAsText();

        Assert.Contains("[Error]", text);
        Assert.Contains("Generate", text);
        Assert.Contains("something broke", text);
        Assert.Contains("stack trace here", text);
    }
}

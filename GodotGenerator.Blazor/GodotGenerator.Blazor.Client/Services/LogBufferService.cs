#nullable enable

using GodotGenerator.Blazor.Client.Models;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Thin backward-compatibility shim over <see cref="AppLogService"/>.
/// Existing call sites (<c>GenerationWorkspace</c>, <c>ManagementDashboard</c>) that use
/// <c>Logs.Append(string)</c> continue to work unchanged; entries are stored as structured
/// <see cref="LogEntry"/> records in the shared <see cref="AppLogService"/> buffer.
/// </summary>
/// <remarks>
/// Register this as Singleton in DI <em>after</em> <see cref="AppLogService"/> so that both
/// services share the same underlying store.  The <see cref="Changed"/> event is forwarded
/// directly from <see cref="AppLogService"/>.
/// </remarks>
public sealed class LogBufferService
{
    private readonly AppLogService _appLog;

    /// <summary>Initialises the shim with the shared structured log service.</summary>
    public LogBufferService(AppLogService appLog)
    {
        _appLog = appLog;
    }

    /// <summary>
    /// Raised whenever a new entry is appended.
    /// Forwarded from <see cref="AppLogService.Changed"/>.
    /// </summary>
    public event Action? Changed
    {
        add => _appLog.Changed += value;
        remove => _appLog.Changed -= value;
    }

    /// <summary>
    /// Returns plain-text snapshots of all entries, preserving the original format
    /// (<c>ISO-timestamp message</c>) consumed by legacy callers.
    /// </summary>
    public IReadOnlyList<string> GetSnapshot() =>
        _appLog.GetSnapshot()
               .Select(e => e.FormatAsText())
               .ToArray();

    /// <summary>
    /// Appends a free-form line as an <see cref="LogEntryLevel.Information"/> / Application entry.
    /// The caller's raw string becomes the <see cref="LogEntry.Message"/>;
    /// the category is inferred as <c>"UI"</c>.
    /// </summary>
    public void Append(string line) =>
        _appLog.Info("UI", line);
}

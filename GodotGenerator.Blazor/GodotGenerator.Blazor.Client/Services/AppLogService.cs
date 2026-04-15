#nullable enable

using GodotGenerator.Blazor.Client.Models;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// In-memory structured log service.  Stores up to <see cref="MaxEntries"/> entries in a
/// fixed-capacity ring buffer; the oldest entry is dropped when the cap is reached.
/// Thread-safe for concurrent appends from the WASM render thread and any background tasks.
/// </summary>
/// <remarks>
/// Register as a Singleton so the buffer is shared across all component scopes within a session.
/// </remarks>
public sealed class AppLogService
{
    /// <summary>Maximum number of entries retained in memory.</summary>
    public const int MaxEntries = 500;

    private readonly List<LogEntry> _entries = new(MaxEntries + 1);
    private readonly object _gate = new();
    private long _sequence;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the thread that appended the entry.  Subscribers must marshal
    /// to the Blazor renderer via <c>InvokeAsync(StateHasChanged)</c>.
    /// </summary>
    public event Action? Changed;

    /// <summary>Raised when <see cref="Clear"/> empties the buffer.</summary>
    public event Action? Cleared;

    // ── Query ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a snapshot of all entries, optionally filtered.  The snapshot is a
    /// defensive copy; callers may enumerate it freely without locking.
    /// </summary>
    /// <param name="minLevel">Exclude entries below this severity.</param>
    /// <param name="kind">Restrict to a specific entry kind.</param>
    /// <param name="search">
    /// Case-insensitive substring applied to <see cref="LogEntry.Category"/> and
    /// <see cref="LogEntry.Message"/>.
    /// </param>
    public IReadOnlyList<LogEntry> GetSnapshot(
        LogEntryLevel? minLevel = null,
        LogEntryKind? kind = null,
        string? search = null)
    {
        LogEntry[] copy;
        lock (_gate)
        {
            copy = _entries.ToArray();
        }

        if (minLevel is null && kind is null && string.IsNullOrWhiteSpace(search))
        {
            return copy;
        }

        var normalizedSearch = search?.Trim();
        return copy
            .Where(e =>
                (minLevel is null || e.Level >= minLevel.Value) &&
                (kind is null    || e.Kind == kind.Value) &&
                (string.IsNullOrEmpty(normalizedSearch) ||
                 e.Category.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 e.Message.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 (e.Detail?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToArray();
    }

    /// <summary>Returns the current entry count (snapshot-consistent).</summary>
    public int Count
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    // ── Write — general ───────────────────────────────────────────────────────

    /// <summary>Appends a structured entry.</summary>
    public void Log(
        LogEntryLevel level,
        string category,
        string message,
        string? detail = null) =>
        Append(new LogEntry(
            Sequence: NextSequence(),
            Timestamp: DateTimeOffset.Now,
            Level: level,
            Kind: LogEntryKind.Application,
            Category: category,
            Message: message,
            Detail: detail));

    /// <summary>Appends a <see cref="LogEntryLevel.Debug"/> entry.</summary>
    public void Debug(string category, string message, string? detail = null) =>
        Log(LogEntryLevel.Debug, category, message, detail);

    /// <summary>Appends a <see cref="LogEntryLevel.Information"/> entry.</summary>
    public void Info(string category, string message, string? detail = null) =>
        Log(LogEntryLevel.Information, category, message, detail);

    /// <summary>Appends a <see cref="LogEntryLevel.Warning"/> entry.</summary>
    public void Warning(string category, string message, string? detail = null) =>
        Log(LogEntryLevel.Warning, category, message, detail);

    /// <summary>Appends a <see cref="LogEntryLevel.Error"/> entry.</summary>
    public void Error(string category, string message, string? detail = null) =>
        Log(LogEntryLevel.Error, category, message, detail);

    // ── Write — IPC ────────────────────────────────────────────────────────────

    /// <summary>
    /// Records an outbound IPC command and returns its assigned correlation id.
    /// Call before dispatching the command; pair the result with
    /// <see cref="LogIpcResponse"/> after the round-trip completes.
    /// </summary>
    /// <param name="command">Versioned command name, e.g. <c>Generate.Text/v1</c>.</param>
    /// <param name="correlationId">Caller-generated opaque id for request/response pairing.</param>
    public void LogIpcRequest(string command, string correlationId)
    {
        var (category, label) = ParseCommand(command);
        Append(new LogEntry(
            Sequence: NextSequence(),
            Timestamp: DateTimeOffset.Now,
            Level: LogEntryLevel.Debug,
            Kind: LogEntryKind.IpcRequest,
            Category: category,
            Message: $"→ {label}",
            CorrelationId: correlationId));
    }

    /// <summary>
    /// Records an inbound IPC response.  Pairs with the matching
    /// <see cref="LogIpcRequest"/> call via <paramref name="correlationId"/>.
    /// </summary>
    /// <param name="command">Versioned command name.</param>
    /// <param name="correlationId">Must match the value returned by <see cref="LogIpcRequest"/>.</param>
    /// <param name="success">Whether the backend reported success.</param>
    /// <param name="durationMs">Round-trip time in milliseconds.</param>
    /// <param name="errorCode">Machine-readable error code on failure; <c>null</c> on success.</param>
    public void LogIpcResponse(
        string command,
        string correlationId,
        bool success,
        double durationMs,
        string? errorCode = null)
    {
        var (category, label) = ParseCommand(command);
        var level = success ? LogEntryLevel.Debug : LogEntryLevel.Error;
        var status = success ? "OK" : $"ERR {errorCode ?? "?"}";
        var detail = !success && errorCode is not null ? $"Error code: {errorCode}" : null;

        Append(new LogEntry(
            Sequence: NextSequence(),
            Timestamp: DateTimeOffset.Now,
            Level: level,
            Kind: LogEntryKind.IpcResponse,
            Category: category,
            Message: $"← {label}  {status}  {durationMs:F0} ms",
            Detail: detail,
            CorrelationId: correlationId,
            DurationMs: durationMs));
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Removes all entries and raises <see cref="Cleared"/> then <see cref="Changed"/>.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }

        Cleared?.Invoke();
        Changed?.Invoke();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private long NextSequence() =>
        System.Threading.Interlocked.Increment(ref _sequence);

    private void Append(LogEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Count >= MaxEntries)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(entry);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Splits a versioned command name (e.g. <c>Generate.Text/v1</c>) into a
    /// short category (<c>"Generate"</c>) and display label (<c>"Text"</c>).
    /// Handles missing dots or version suffixes gracefully.
    /// </summary>
    private static (string Category, string Label) ParseCommand(string command)
    {
        // Strip version suffix: "Generate.Text/v1" → "Generate.Text"
        var withoutVersion = command.Contains('/')
            ? command[..command.IndexOf('/')]
            : command;

        var dotIndex = withoutVersion.IndexOf('.');
        if (dotIndex < 0)
        {
            return (withoutVersion, withoutVersion);
        }

        var category = withoutVersion[..dotIndex];
        var label    = withoutVersion[(dotIndex + 1)..];
        return (category, label);
    }
}

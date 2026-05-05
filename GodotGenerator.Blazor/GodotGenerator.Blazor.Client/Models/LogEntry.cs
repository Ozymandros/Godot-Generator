#nullable enable

namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Severity levels used by <see cref="AppLogService"/>.
/// Intentionally mirrors <c>Microsoft.Extensions.Logging.LogLevel</c> names
/// without taking a hard dependency on it from the WASM client.
/// </summary>
public enum LogEntryLevel
{
    /// <summary>Verbose diagnostics; hidden by default.</summary>
    Debug = 0,

    /// <summary>Normal operational messages.</summary>
    Information = 1,

    /// <summary>Unexpected but recoverable conditions.</summary>
    Warning = 2,

    /// <summary>Failures that affect a user-visible operation.</summary>
    Error = 3,
}

/// <summary>
/// Classifies the origin / intent of a <see cref="LogEntry"/> so the UI can
/// apply appropriate icons, colours, and grouping.
/// </summary>
public enum LogEntryKind
{
    /// <summary>General UI or service event (generation, settings, lifecycle).</summary>
    Application,

    /// <summary>Outbound IPC command sent to the backend.</summary>
    IpcRequest,

    /// <summary>Inbound IPC response received from the backend.</summary>
    IpcResponse,

    /// <summary>
    /// Line captured from the backend process stdout or stderr.
    /// Category is always <c>"Backend"</c>; level reflects the stream
    /// (<see cref="LogEntryLevel.Information"/> for stdout,
    /// <see cref="LogEntryLevel.Error"/> for stderr).
    /// </summary>
    Backend,
}

/// <summary>
/// Immutable, uniquely sequenced log record produced by <see cref="AppLogService"/>.
/// </summary>
/// <param name="Sequence">Monotonically increasing counter for stable ordering and Razor @key.</param>
/// <param name="Timestamp">Wall-clock instant when the entry was created.</param>
/// <param name="Level">Severity.</param>
/// <param name="Kind">Origin classification.</param>
/// <param name="Category">
/// Short source label, e.g. <c>"Generate"</c>, <c>"PromptAssist"</c>, <c>"Config"</c>.
/// </param>
/// <param name="Message">Human-readable summary line.</param>
/// <param name="Detail">
/// Optional expanded content (error detail, stack fragment, payload excerpt).
/// Shown only when the user expands the row.
/// </param>
/// <param name="CorrelationId">
/// Opaque ID used to pair an <see cref="LogEntryKind.IpcRequest"/> with its
/// corresponding <see cref="LogEntryKind.IpcResponse"/>.
/// </param>
/// <param name="DurationMs">
/// Round-trip duration in milliseconds; populated only for <see cref="LogEntryKind.IpcResponse"/>.
/// </param>
public sealed record LogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    LogEntryLevel Level,
    LogEntryKind Kind,
    string Category,
    string Message,
    string? Detail = null,
    string? CorrelationId = null,
    double? DurationMs = null)
{
    /// <summary>
    /// Formats the entry as a plain text line compatible with the legacy
    /// <see cref="LogBufferService"/> snapshot format.
    /// </summary>
    public string FormatAsText() =>
        $"{Timestamp:O} [{Level}] {Category}: {Message}" +
        (Detail is not null ? $" | {Detail}" : string.Empty);
}

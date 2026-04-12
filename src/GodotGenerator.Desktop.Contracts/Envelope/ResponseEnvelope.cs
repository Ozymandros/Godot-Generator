#nullable enable

namespace GodotGenerator.Desktop.Contracts.Envelope;

/// <summary>
/// Versioned response envelope returned from the .NET backend to the Electron main process
/// over a named pipe. Mirrors the correlation id from the originating <see cref="CommandEnvelope"/>
/// so callers can match responses to in-flight requests.
/// </summary>
/// <param name="CorrelationId">Matches the <see cref="CommandEnvelope.CorrelationId"/> of the originating request.</param>
/// <param name="Success">Whether the command was executed successfully.</param>
/// <param name="PayloadJson">Optional UTF-8 JSON string carrying the command-specific response payload.</param>
/// <param name="ErrorCode">Machine-readable error code from <see cref="DesktopErrorCode"/>; null on success.</param>
/// <param name="ErrorMessage">Human-readable error description; null on success.</param>
/// <param name="SchemaVersion">Envelope schema version; default <c>"1"</c>.</param>
public sealed record ResponseEnvelope(
    string CorrelationId,
    bool Success,
    string? PayloadJson,
    string? ErrorCode,
    string? ErrorMessage,
    string SchemaVersion = "1");

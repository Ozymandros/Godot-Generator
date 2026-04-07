#nullable enable

namespace GodotGenerator.Desktop.Contracts.Envelope;

/// <summary>
/// Versioned command envelope sent from the Electron main process to the .NET backend
/// over a named pipe. Each envelope carries a unique correlation id for request/response matching,
/// the versioned command name (e.g. <c>Config.GetAll/v1</c>), an optional JSON payload,
/// and the schema version guard.
/// </summary>
/// <param name="CorrelationId">Unique identifier used to match this request to its response.</param>
/// <param name="Command">Versioned command name, e.g. <c>Config.GetAll/v1</c>.</param>
/// <param name="PayloadJson">Optional UTF-8 JSON string carrying command-specific parameters.</param>
/// <param name="SchemaVersion">Envelope schema version; default <c>"1"</c>.</param>
public sealed record CommandEnvelope(
    string CorrelationId,
    string Command,
    string? PayloadJson,
    string SchemaVersion = "1");

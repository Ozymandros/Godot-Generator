namespace GodotGenerator.Desktop.Contracts.Envelope;

/// <summary>
/// Well-known machine-readable error codes used in <see cref="ResponseEnvelope.ErrorCode"/>.
/// Consumers should compare against these constants rather than raw strings.
/// </summary>
public static class DesktopErrorCode
{
    /// <summary>No handler is registered for the requested command name.</summary>
    public const string UnknownCommand = "UNKNOWN_COMMAND";

    /// <summary>The command payload failed schema or business-rule validation.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>The handler threw an unhandled exception during execution.</summary>
    public const string HandlerFaulted = "HANDLER_FAULTED";

    /// <summary>The command exceeded its allowed execution time.</summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>The incoming payload exceeded the maximum allowed size.</summary>
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
}

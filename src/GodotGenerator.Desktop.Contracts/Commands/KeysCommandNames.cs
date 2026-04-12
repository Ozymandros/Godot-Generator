#nullable enable

namespace GodotGenerator.Desktop.Contracts.Commands;

/// <summary>Versioned command name constants for the Keys domain.</summary>
public static class KeysCommandNames
{
    /// <summary>Saves (or removes) API keys in batch by service handle.</summary>
    public const string Save = "Keys.Save/v1";
}

/// <summary>
/// Request payload for <see cref="KeysCommandNames.Save"/>.
/// A <c>null</c> value for a handle removes the stored key.
/// </summary>
/// <param name="Keys">Map of service handle → API key value (null removes).</param>
public sealed record KeysSaveRequest(Dictionary<string, string?> Keys);

/// <summary>Response payload for <see cref="KeysCommandNames.Save"/>.</summary>
/// <param name="SavedHandles">The list of service handles that were successfully persisted.</param>
public sealed record KeysSaveResponse(IReadOnlyList<string> SavedHandles);

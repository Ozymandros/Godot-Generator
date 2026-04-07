#nullable enable

namespace GodotGenerator.Desktop.Contracts.Commands;

/// <summary>Versioned command name constants for the Preference domain.</summary>
public static class PreferenceCommandNames
{
    /// <summary>Gets a single preference value by key.</summary>
    public const string Get = "Preference.Get/v1";

    /// <summary>Sets (or clears) a single preference value by key.</summary>
    public const string Set = "Preference.Set/v1";
}

/// <summary>Request payload for <see cref="PreferenceCommandNames.Get"/>.</summary>
/// <param name="Key">The preference key to retrieve.</param>
public sealed record PreferenceGetRequest(string Key);

/// <summary>Response payload for <see cref="PreferenceCommandNames.Get"/>.</summary>
/// <param name="Key">The requested preference key.</param>
/// <param name="Value">The stored value, or <c>null</c> if not set.</param>
public sealed record PreferenceGetResponse(string Key, string? Value);

/// <summary>Request payload for <see cref="PreferenceCommandNames.Set"/>.</summary>
/// <param name="Key">The preference key to write.</param>
/// <param name="Value">The value to store; <c>null</c> removes the key.</param>
public sealed record PreferenceSetRequest(string Key, string? Value);

/// <summary>Response payload for <see cref="PreferenceCommandNames.Set"/>.</summary>
/// <param name="Key">The written preference key.</param>
/// <param name="Value">The stored value after the write.</param>
public sealed record PreferenceSetResponse(string Key, string? Value);

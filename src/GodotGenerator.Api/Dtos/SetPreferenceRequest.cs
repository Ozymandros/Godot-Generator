#nullable enable

namespace GodotGenerator.Api.Dtos;

/// <summary>
/// Request contract for setting one preference key/value.
/// </summary>
public sealed record SetPreferenceRequest(string Key, string? Value);

#nullable enable
using System.Text.Json;

namespace GodotGenerator.Application.Serialization;

/// <summary>
/// Normalizes values from JSON-deserialized option bags (e.g. IPC <c>Dictionary&lt;string, object?&gt;</c>),
/// where string values are often represented as <see cref="JsonElement"/> rather than <see cref="string"/>.
/// </summary>
public static class JsonOptionValue
{
    /// <summary>
    /// Returns a trimmed non-empty string, or null when the value is absent or not meaningfully textual.
    /// </summary>
    public static string? AsTrimmedString(object? raw)
    {
        if (raw is null)
        {
            return null;
        }

        if (raw is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => string.IsNullOrWhiteSpace(je.GetString()) ? null : je.GetString()!.Trim(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => null,
            };
        }

        var fallback = raw.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
    }

    /// <summary>
    /// True when a tool/option value should be treated as empty so we can inject a default
    /// (null, whitespace string, JSON null, JSON empty string).
    /// </summary>
    public static bool IsNullOrEmptyStringLike(object? raw)
    {
        if (raw is null)
        {
            return true;
        }

        if (raw is string s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(je.GetString()),
                _ => false,
            };
        }

        return false;
    }
}

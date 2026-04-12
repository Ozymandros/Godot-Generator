#nullable enable

namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Maps persisted <see cref="GodotGenerator.Application.PreferenceKeys.PreferredLanguage"/> values
/// (<c>gdscript</c> / <c>csharp</c>) to Advanced Options combobox labels (<c>GDScript</c> / <c>C#</c>).
/// </summary>
public static class PreferredScriptLanguagePref
{
    /// <summary>Maps stored preference to UI label; unknown values become empty (default / no preference).</summary>
    public static string ToUiLabel(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return string.Empty;
        }

        var s = stored.Trim();
        return s.ToLowerInvariant() switch
        {
            "gdscript" => "GDScript",
            "csharp" => "C#",
            _ => s.Equals("GDScript", StringComparison.Ordinal) ? "GDScript"
                : s.Equals("C#", StringComparison.Ordinal) ? "C#"
                : string.Empty,
        };
    }

    /// <summary>Persists UI selection; empty means clear preference (model default).</summary>
    public static string? ToStoredValue(string? uiLabel)
    {
        if (string.IsNullOrWhiteSpace(uiLabel))
        {
            return null;
        }

        if (uiLabel.Equals("GDScript", StringComparison.OrdinalIgnoreCase))
        {
            return "gdscript";
        }

        if (uiLabel.Equals("C#", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        return null;
    }
}

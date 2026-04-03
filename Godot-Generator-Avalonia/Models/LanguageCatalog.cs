using GodotGenerator.Application;

namespace Godot_Generator_Avalonia.Models;

/// <summary>
/// Supported language identifiers for global defaults and per-panel overrides.
/// </summary>
public static class LanguageCatalog
{
    /// <summary>Preference key persisted via the API layer.</summary>
    public const string PreferredLanguagePreferenceKey = PreferenceKeys.PreferredLanguage;

    /// <summary>Supported generation language identifiers.</summary>
    public static readonly IReadOnlyList<string> Supported = new[]
    {
        "csharp",
        "gdscript",
        "typescript",
        "javascript",
        "python",
        "rust",
        "go",
        "java",
    };
}

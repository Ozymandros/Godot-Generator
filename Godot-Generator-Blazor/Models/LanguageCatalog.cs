namespace Godot_Generator_Blazor.Models;

/// <summary>
/// Provides supported language values for global defaults and panel overrides.
/// </summary>
public static class LanguageCatalog
{
    /// <summary>
    /// Canonical preference key used to persist global default language.
    /// </summary>
    public const string PreferredLanguagePreferenceKey = "preferred_language";

    /// <summary>
    /// Supported generation language identifiers.
    /// </summary>
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

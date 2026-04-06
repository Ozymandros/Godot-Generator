namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Advanced generation options (Unity-style accordion); merged into <see cref="GodotGenerator.Api.Dtos.GenerateRequest"/>.
/// </summary>
public sealed class GenerationAdvancedOptions
{
    public double Temperature { get; set; } = 0.7;

    public string ApiKeyOverride { get; set; } = string.Empty;

    public string SystemPromptOverride { get; set; } = string.Empty;

    /// <summary>Empty = not set; otherwise GDScript or C#.</summary>
    public string PreferredScriptLanguage { get; set; } = string.Empty;
}

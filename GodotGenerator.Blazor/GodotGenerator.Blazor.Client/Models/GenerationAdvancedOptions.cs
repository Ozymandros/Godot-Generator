namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Advanced generation options (Unity-style accordion); merged into <see cref="GodotGenerator.Api.Dtos.GenerateRequest"/>.
/// </summary>
public sealed class GenerationAdvancedOptions
{
    public double Temperature { get; set; } = 0.7;

    public string ApiKeyOverride { get; set; } = string.Empty;

    public string SystemPromptOverride { get; set; } = string.Empty;

    /// <summary>Empty = no preference; otherwise <c>GDScript</c> or <c>C#</c> (programming language for generated code).</summary>
    public string PreferredScriptLanguage { get; set; } = string.Empty;

    /// <summary>Empty = no preference; otherwise <c>2D</c> or <c>3D</c> (node type for Godot scene generation).</summary>
    public string GodotNodeType { get; set; } = string.Empty;
}

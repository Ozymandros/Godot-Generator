namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Generation modalities aligned with <see cref="GodotGenerator.Api"/> BFF routes and Avalonia shell.
/// </summary>
public enum GenerationModality
{
    Text,
    Code,
    Image,
    Audio,
    Video,
    Sprites,
    GodotUi,
    GodotPhysics,
    Scenes,
    GodotProject,
    Animations,
    GodotLighting,
    GodotCamera,
    GodotShaders,
    GodotSignals,
    GodotNodes,
    /// <summary>
    /// Wizard orchestration: the LLM may invoke multiple generation tools to fulfil a project-scoped goal.
    /// </summary>
    Wizard,
}

namespace Godot_Generator_Blazor.Models;

/// <summary>
/// Supported generation modalities exposed in the Blazor frontend.
/// </summary>
public enum GenerationModality
{
    /// <summary>Text generation modality.</summary>
    Text,
    /// <summary>Code generation modality.</summary>
    Code,
    /// <summary>Image generation modality.</summary>
    Image,
    /// <summary>Audio generation modality.</summary>
    Audio,
    /// <summary>Video generation modality.</summary>
    Video,
    /// <summary>Sprite generation modality.</summary>
    Sprites,
    /// <summary>Godot UI generation modality.</summary>
    GodotUi,
    /// <summary>Godot physics generation modality.</summary>
    GodotPhysics,
}

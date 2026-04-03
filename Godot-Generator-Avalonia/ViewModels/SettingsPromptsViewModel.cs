namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// Prompt templates: read-only until prompts are exposed from host storage via GetAllConfig.
/// </summary>
public sealed class SettingsPromptsViewModel : ViewModelBase
{
    /// <summary>User-facing explanation.</summary>
    public string InfoMessage =>
        "System and template prompts are not yet loaded from persistent storage in this build. " +
        "The Prompts tab will be wired when the host exposes non-empty prompts in GetAllConfig or a dedicated store.";
}

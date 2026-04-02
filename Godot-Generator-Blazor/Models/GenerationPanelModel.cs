namespace Godot_Generator_Blazor.Models;

/// <summary>
/// Mutable state model for one generation panel.
/// </summary>
public sealed class GenerationPanelModel
{
    /// <summary>
    /// Initializes a new panel model for the given modality.
    /// </summary>
    /// <param name="modality">Panel modality.</param>
    public GenerationPanelModel(GenerationModality modality)
    {
        Modality = modality;
    }

    /// <summary>
    /// Gets the panel modality.
    /// </summary>
    public GenerationModality Modality { get; }

    /// <summary>
    /// Gets or sets user prompt input.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional per-panel preferred language override.
    /// </summary>
    public string PreferredLanguageOverride { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the panel currently executes a request.
    /// </summary>
    public bool IsBusy { get; set; }

    /// <summary>
    /// Gets or sets the latest panel error.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the latest response payload text.
    /// </summary>
    public string? ResponseText { get; set; }
}

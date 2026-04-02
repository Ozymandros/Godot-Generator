using Godot_Generator_Blazor.Models;
using Godot_Generator_Blazor.Services;

namespace Godot_Generator_Blazor.State;

/// <summary>
/// Manages generation panel state and request execution.
/// </summary>
public sealed class GenerationPanelStateService(IApiClient apiClient)
{
    private readonly Dictionary<GenerationModality, GenerationPanelModel> _panels = Enum
        .GetValues<GenerationModality>()
        .ToDictionary(m => m, m => new GenerationPanelModel(m));

    /// <summary>
    /// Gets panel state for a modality.
    /// </summary>
    /// <param name="modality">Target modality.</param>
    /// <returns>Panel state model.</returns>
    public GenerationPanelModel Get(GenerationModality modality) => _panels[modality];

    /// <summary>
    /// Gets all panel state models.
    /// </summary>
    public IReadOnlyCollection<GenerationPanelModel> All => _panels.Values;

    /// <summary>
    /// Submits one panel generation request using language precedence rules.
    /// </summary>
    /// <param name="panel">Panel state.</param>
    /// <param name="globalPreferredLanguage">Global language fallback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SubmitAsync(
        GenerationPanelModel panel,
        string? globalPreferredLanguage,
        CancellationToken cancellationToken = default)
    {
        panel.IsBusy = true;
        panel.Error = null;
        panel.ResponseText = null;

        try
        {
            var response = await apiClient
                .GenerateAsync(
                    panel.Modality,
                    panel.Prompt,
                    panel.PreferredLanguageOverride,
                    globalPreferredLanguage,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.Success)
            {
                panel.ResponseText = response.Message;
            }
            else
            {
                panel.Error = response.Error;
            }
        }
        finally
        {
            panel.IsBusy = false;
        }
    }
}

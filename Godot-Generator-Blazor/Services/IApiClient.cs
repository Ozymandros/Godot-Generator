using Godot_Generator_Blazor.Models;

namespace Godot_Generator_Blazor.Services;

/// <summary>
/// UI-focused API client abstraction for generation and preference operations.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Executes a generation request for one modality.
    /// </summary>
    /// <param name="modality">Target modality.</param>
    /// <param name="prompt">User prompt.</param>
    /// <param name="preferredLanguageOverride">Optional panel-level language override.</param>
    /// <param name="globalPreferredLanguage">Global fallback language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result payload or an error.</returns>
    Task<(bool Success, string Message, string? Error)> GenerateAsync(
        GenerationModality modality,
        string prompt,
        string? preferredLanguageOverride,
        string? globalPreferredLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the global preferred language from API preferences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stored language or empty string when missing.</returns>
    Task<string> GetGlobalPreferredLanguageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the global preferred language to API preferences.
    /// </summary>
    /// <param name="language">Language to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when save succeeded.</returns>
    Task<bool> SaveGlobalPreferredLanguageAsync(string language, CancellationToken cancellationToken = default);
}

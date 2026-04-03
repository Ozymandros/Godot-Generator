using Godot_Generator_Avalonia.Models;

namespace Godot_Generator_Avalonia.Services;

/// <summary>
/// UI-facing adapter over <see cref="GodotGenerator.Api.Abstractions.IGodotGeneratorApiService"/> (language precedence and modality routing).
/// </summary>
public interface IGeneratorApiClient
{
    /// <summary>Runs generation for the given modality.</summary>
    Task<(bool Success, string Message, string? Error)> GenerateAsync(
        GenerationModality modality,
        string prompt,
        string? preferredLanguageOverride,
        string? globalPreferredLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the persisted global preferred language.</summary>
    Task<string> GetGlobalPreferredLanguageAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the global preferred language.</summary>
    Task<bool> SaveGlobalPreferredLanguageAsync(string language, CancellationToken cancellationToken = default);
}

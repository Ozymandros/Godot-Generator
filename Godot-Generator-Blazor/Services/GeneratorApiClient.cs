using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using Godot_Generator_Blazor.Models;

namespace Godot_Generator_Blazor.Services;

/// <summary>
/// Adapts <see cref="IGodotGeneratorApiService"/> for Blazor UI consumption.
/// </summary>
public sealed class GeneratorApiClient(
    IGodotGeneratorApiService api,
    ILogger<GeneratorApiClient> logger) : IApiClient
{
    /// <inheritdoc />
    public async Task<(bool Success, string Message, string? Error)> GenerateAsync(
        GenerationModality modality,
        string prompt,
        string? preferredLanguageOverride,
        string? globalPreferredLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, string.Empty, "Prompt cannot be empty.");
        }

        var effectiveLanguage = ResolvePreferredLanguage(preferredLanguageOverride, globalPreferredLanguage);
        var request = new GenerateRequest(
            Prompt: prompt.Trim(),
            Options: BuildOptions(effectiveLanguage));

        ApiResponse<Dictionary<string, object?>> result = modality switch
        {
            GenerationModality.Text => await api.GenerateTextAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Code => await api.GenerateCodeAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Image => await api.GenerateImageAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Audio => await api.GenerateAudioAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Video => await api.GenerateVideoAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.Sprites => await api.GenerateSpritesAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.GodotUi => await api.GenerateGodotUiAsync(request, cancellationToken).ConfigureAwait(false),
            GenerationModality.GodotPhysics => await api.GenerateGodotPhysicsAsync(request, cancellationToken).ConfigureAwait(false),
            _ => ApiResponse<Dictionary<string, object?>>.Fail("Unsupported modality."),
        };

        if (!result.Success)
        {
            return (false, string.Empty, result.Error ?? "Generation failed.");
        }

        var message = ExtractMessage(result.Data);
        logger.LogDebug("Generation succeeded for {Modality}", modality);
        return (true, message, null);
    }

    /// <inheritdoc />
    public async Task<string> GetGlobalPreferredLanguageAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.GetPreferenceAsync(LanguageCatalog.PreferredLanguagePreferenceKey, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null)
        {
            return string.Empty;
        }

        return result.Data.TryGetValue("value", out var value) ? value ?? string.Empty : string.Empty;
    }

    /// <inheritdoc />
    public async Task<bool> SaveGlobalPreferredLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var saveResult = await api
            .SetPreferenceAsync(new SetPreferenceRequest(LanguageCatalog.PreferredLanguagePreferenceKey, language), cancellationToken)
            .ConfigureAwait(false);
        return saveResult.Success;
    }

    /// <summary>
    /// Resolves preferred language using panel override precedence over global default.
    /// </summary>
    /// <param name="preferredLanguageOverride">Panel-level language override.</param>
    /// <param name="globalPreferredLanguage">Global default language.</param>
    /// <returns>Effective preferred language for request mapping.</returns>
    public static string ResolvePreferredLanguage(string? preferredLanguageOverride, string? globalPreferredLanguage)
    {
        if (!string.IsNullOrWhiteSpace(preferredLanguageOverride))
        {
            return preferredLanguageOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(globalPreferredLanguage) ? string.Empty : globalPreferredLanguage.Trim();
    }

    /// <summary>
    /// Builds the options dictionary for the generation request.
    /// </summary>
    /// <param name="effectiveLanguage">The effective language for the generation request.</param>
    /// <returns>The options dictionary for the generation request.</returns>
    private static IReadOnlyDictionary<string, object?> BuildOptions(string effectiveLanguage)
    {
        if (string.IsNullOrWhiteSpace(effectiveLanguage))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["preferred_language"] = effectiveLanguage,
        };
    }


    /// <summary>
    /// Extracts the message from the generation result.
    /// </summary>
    /// <param name="data">The generation result data.</param>
    /// <returns>The message from the generation result.</returns>
    private static string ExtractMessage(IReadOnlyDictionary<string, object?>? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        return data.TryGetValue("message", out var messageObj) && messageObj is string message
            ? message
            : string.Empty;
    }
}

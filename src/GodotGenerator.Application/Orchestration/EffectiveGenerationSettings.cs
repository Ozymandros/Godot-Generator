#nullable enable

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Effective runtime settings resolved for one generation request.
/// </summary>
public sealed record EffectiveGenerationSettings(
    string Modality,
    string? Provider,
    string? ModelId,
    string? PreferredLanguage);


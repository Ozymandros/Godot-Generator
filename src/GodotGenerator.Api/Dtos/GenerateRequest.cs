#nullable enable

namespace GodotGenerator.Api.Dtos;

/// <summary>
/// Public generation request contract mirroring FastAPI generation semantics.
/// </summary>
public sealed record GenerateRequest(
    string Prompt,
    string? Provider = null,
    IReadOnlyDictionary<string, object?>? Options = null,
    string? ApiKey = null,
    string? SystemPrompt = null,
    string? ProjectName = null,
    string? PreferredModelId = null);

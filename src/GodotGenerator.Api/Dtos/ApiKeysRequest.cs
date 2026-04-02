#nullable enable

namespace GodotGenerator.Api.Dtos;

/// <summary>
/// Request contract for saving API keys in batch.
/// </summary>
public sealed record ApiKeysRequest(IReadOnlyDictionary<string, string?> Keys);

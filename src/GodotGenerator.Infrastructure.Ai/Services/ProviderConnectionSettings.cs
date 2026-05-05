#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Effective provider connection settings used to configure an OpenAI-compatible chat completion client.
/// </summary>
/// <param name="Provider">Normalized provider id.</param>
/// <param name="ModelId">Effective model id.</param>
/// <param name="ApiKey">Resolved API key.</param>
/// <param name="Endpoint">Resolved endpoint override when required; null for default OpenAI host routing.</param>
public sealed record ProviderConnectionSettings(
    string Provider,
    string ModelId,
    string ApiKey,
    Uri? Endpoint);

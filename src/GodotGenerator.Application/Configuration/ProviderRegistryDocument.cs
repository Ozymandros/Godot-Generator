#nullable enable
using System.Text.Json.Serialization;

namespace GodotGenerator.Application.Configuration;

/// <summary>Versioned JSON blob stored under <see cref="PreferenceKeys.ProvidersRegistryV1"/>.</summary>
public sealed class ProviderRegistryDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("providers")]
    public List<ProviderRegistryEntry> Providers { get; set; } = [];
}

/// <summary>Single registered provider ("engine") row.</summary>
public sealed class ProviderRegistryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("keyStoreHandle")]
    public string KeyStoreHandle { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("openAiCompatibility")]
    public bool OpenAiCompatibility { get; set; }

    [JsonPropertyName("authenticationRequired")]
    public bool AuthenticationRequired { get; set; } = true;

    [JsonPropertyName("vision")]
    public bool Vision { get; set; }

    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; } = true;

    [JsonPropertyName("functionCalling")]
    public bool FunctionCalling { get; set; } = true;

    [JsonPropertyName("genericToolUse")]
    public bool GenericToolUse { get; set; } = true;

    [JsonPropertyName("modalities")]
    public List<string> Modalities { get; set; } = [];
}

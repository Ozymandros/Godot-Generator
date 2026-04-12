#nullable enable
using System.Text.Json.Serialization;

namespace GodotGenerator.Application.Configuration;

/// <summary>Versioned JSON blob stored under <see cref="PreferenceKeys.ProvidersRegistryV1"/>.</summary>
public sealed class ProviderRegistryDocument
{
    /// <summary>Schema version of the provider registry document.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>All configured provider rows.</summary>
    [JsonPropertyName("providers")]
    public List<ProviderRegistryEntry> Providers { get; set; } = [];
}

/// <summary>Single registered provider ("engine") row.</summary>
public sealed class ProviderRegistryEntry
{
    /// <summary>Unique provider id used as canonical key across the app.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Handle used to resolve API credentials from key storage.</summary>
    [JsonPropertyName("keyStoreHandle")]
    public string KeyStoreHandle { get; set; } = string.Empty;

    /// <summary>Optional custom endpoint for provider-compatible APIs.</summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>Whether provider follows OpenAI-compatible API semantics.</summary>
    [JsonPropertyName("openAiCompatibility")]
    public bool OpenAiCompatibility { get; set; }

    /// <summary>Whether this provider requires explicit authentication credentials.</summary>
    [JsonPropertyName("authenticationRequired")]
    public bool AuthenticationRequired { get; set; } = true;

    /// <summary>Whether provider supports vision/image understanding features.</summary>
    [JsonPropertyName("vision")]
    public bool Vision { get; set; }

    /// <summary>Whether provider supports streamed responses.</summary>
    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; } = true;

    /// <summary>Whether provider supports function-calling patterns.</summary>
    [JsonPropertyName("functionCalling")]
    public bool FunctionCalling { get; set; } = true;

    /// <summary>Whether provider supports generic tool-use capabilities.</summary>
    [JsonPropertyName("genericToolUse")]
    public bool GenericToolUse { get; set; } = true;

    /// <summary>List of supported modality keys (llm, image, audio, etc.).</summary>
    [JsonPropertyName("modalities")]
    public List<string> Modalities { get; set; } = [];
}

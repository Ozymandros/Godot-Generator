#nullable enable
using System.Text.Json.Serialization;

namespace GodotGenerator.Application.Configuration;

/// <summary>Versioned JSON blob stored under <see cref="PreferenceKeys.ModelsRegistryV1"/>.</summary>
public sealed class ModelRegistryDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("models")]
    public List<ModelRegistryEntry> Models { get; set; } = [];
}

/// <summary>Registered model endpoint for a provider.</summary>
public sealed class ModelRegistryEntry
{
    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("friendlyName")]
    public string FriendlyName { get; set; } = string.Empty;

    [JsonPropertyName("engineValue")]
    public string EngineValue { get; set; } = string.Empty;

    /// <summary>Normalized modality bucket: llm, image, audio, video, etc.</summary>
    [JsonPropertyName("modality")]
    public string Modality { get; set; } = "llm";
}

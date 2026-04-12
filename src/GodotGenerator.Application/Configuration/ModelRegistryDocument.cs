#nullable enable
using System.Text.Json.Serialization;

namespace GodotGenerator.Application.Configuration;

/// <summary>Versioned JSON blob stored under <see cref="PreferenceKeys.ModelsRegistryV1"/>.</summary>
public sealed class ModelRegistryDocument
{
    /// <summary>Schema version of the model registry document.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Registered model entries grouped logically by provider id.</summary>
    [JsonPropertyName("models")]
    public List<ModelRegistryEntry> Models { get; set; } = [];
}

/// <summary>Registered model endpoint for a provider.</summary>
public sealed class ModelRegistryEntry
{
    /// <summary>Provider identifier that owns this model entry.</summary>
    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Human-friendly display name shown in configuration UI.</summary>
    [JsonPropertyName("friendlyName")]
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>Provider engine/model value sent to downstream APIs.</summary>
    [JsonPropertyName("engineValue")]
    public string EngineValue { get; set; } = string.Empty;

    /// <summary>Normalized modality bucket: llm, image, audio, video, etc.</summary>
    [JsonPropertyName("modality")]
    public string Modality { get; set; } = "llm";
}

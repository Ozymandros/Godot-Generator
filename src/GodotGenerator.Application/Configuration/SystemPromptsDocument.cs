#nullable enable
using System.Text.Json.Serialization;

namespace GodotGenerator.Application.Configuration;

/// <summary>Versioned JSON blob stored under <see cref="PreferenceKeys.PromptsSystemV1"/>.</summary>
public sealed class SystemPromptsDocument
{
    /// <summary>Schema version of the system prompts document.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Modality key (text, code, image, …) to system prompt text.</summary>
    [JsonPropertyName("prompts")]
    public Dictionary<string, string> Prompts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

#nullable enable
using System.Text.Json.Serialization;

namespace GodotGenerator.Infrastructure.Persistence.Json;

/// <summary>
/// Serializable preferences document (key-value).
/// </summary>
internal sealed class PreferencesDocument
{
    [JsonPropertyName("values")]
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.Ordinal);
}

#nullable enable

namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Canonical modality tokens for provider registry and model registration (Settings → Providers / Models).
/// </summary>
public static class RegistryModalityCatalog
{
    /// <summary>Options for <see cref="Components.Generic.SmartField"/> Select (lowercase slugs).</summary>
    public static readonly string[] SelectOptions =
    [
        "llm",
        "image",
        "audio",
        "video",
        "sprites",
        "code",
        "text",
        "music",
        "animations",
        "scenes",
    ];
}

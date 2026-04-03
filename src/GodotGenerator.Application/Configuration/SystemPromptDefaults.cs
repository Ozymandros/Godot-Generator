#nullable enable

namespace GodotGenerator.Application.Configuration;

/// <summary>Factory defaults for system prompts (used when resetting prompts in UI).</summary>
public static class SystemPromptDefaults
{
    public static IReadOnlyDictionary<string, string> ByModality { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "You are a helpful assistant providing concise and accurate information for Godot-related workflows.",
        ["code"] = "You are an expert Godot 4 developer. Generate clean, efficient GDScript and C# when appropriate.",
        ["image"] = "You are a creative prompt engineer for image generation suitable for game assets and Godot import pipelines.",
        ["audio"] = "You are an expert at describing speech, music, and sound effects for games and Godot integration.",
        ["music"] = "You are an expert at describing music generation prompts for games and trailers.",
        ["video"] = "You help describe video generation prompts suitable for games and trailers.",
        ["godot-ui"] = "You design and describe Godot Control nodes, themes, and UI layouts (Godot 4).",
        ["godot-physics"] = "You assist with Godot 4 physics: RigidBody, CharacterBody, Area, joints, and collision layers.",
    };

    public static SystemPromptsDocument CreateDocument() =>
        new()
        {
            Version = 1,
            Prompts = new Dictionary<string, string>(ByModality, StringComparer.OrdinalIgnoreCase),
        };
}

#nullable enable

namespace GodotGenerator.Application;

/// <summary>
/// Canonical preference key names shared by discovery, API, and UI.
/// </summary>
public static class PreferenceKeys
{
    /// <summary>Default generation language (e.g. csharp, gdscript). Same semantic as request option <c>preferred_language</c>.</summary>
    public const string PreferredLanguage = "preferred_language";

    /// <summary>Legacy alias; read for migration only. Prefer <see cref="PreferredLanguage"/>.</summary>
    public const string PreferredLocaleLegacy = "preferred_locale";

    /// <summary>Default LLM provider preference.</summary>
    public const string PreferredLlmProvider = "preferred_llm_provider";

    /// <summary>Default image provider preference.</summary>
    public const string PreferredImageProvider = "preferred_image_provider";

    /// <summary>Default audio provider preference.</summary>
    public const string PreferredAudioProvider = "preferred_audio_provider";

    /// <summary>Default video provider preference.</summary>
    public const string PreferredVideoProvider = "preferred_video_provider";

    /// <summary>Default LLM model id preference.</summary>
    public const string PreferredLlmModel = "preferred_llm_model";

    /// <summary>Default image model id preference.</summary>
    public const string PreferredImageModel = "preferred_image_model";

    /// <summary>Default audio model id preference.</summary>
    public const string PreferredAudioModel = "preferred_audio_model";

    /// <summary>Default video model id preference.</summary>
    public const string PreferredVideoModel = "preferred_video_model";

    /// <summary>JSON document: registered intelligence providers (versioned).</summary>
    public const string ProvidersRegistryV1 = "providers.registry.v1";

    /// <summary>JSON document: registered models per provider (versioned).</summary>
    public const string ModelsRegistryV1 = "models.registry.v1";

    /// <summary>JSON document: system prompts by modality key (versioned).</summary>
    public const string PromptsSystemV1 = "prompts.system.v1";

    /// <summary>Optional backend base URL (stored for future remote transport; in-process may ignore).</summary>
    public const string AppBackendUrl = "app.backend_url";

    /// <summary>Base path for generated output (relative or absolute).</summary>
    public const string AppOutputBasePath = "app.output_base_path";

    /// <summary>Legacy per-panel prompt keys (migrated into <see cref="PromptsSystemV1"/> on read).</summary>
    public const string PromptsTextLegacy = "prompts.text";

    /// <summary>Legacy prompt key for code generation.</summary>
    public const string PromptsCodeLegacy = "prompts.code";

    /// <summary>Legacy prompt key for Godot UI generation.</summary>
    public const string PromptsGodotUiLegacy = "prompts.godot_ui";

    /// <summary>Legacy prompt key for Godot physics generation.</summary>
    public const string PromptsGodotPhysicsLegacy = "prompts.godot_physics";
}

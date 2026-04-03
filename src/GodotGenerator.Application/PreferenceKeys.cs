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
}

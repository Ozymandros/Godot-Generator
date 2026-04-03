namespace GodotGenerator.Domain;

/// <summary>
/// Shared constants for the Godot Generator backend.
/// </summary>
public static class Constants
{
    // Providers
    public const string ProviderOpenAi = "openai";
    public const string ProviderAnthropic = "anthropic";
    public const string ProviderGoogle = "google";
    public const string ProviderVertexAi = "vertex_ai";
    public const string ProviderOllama = "ollama";
    public const string ProviderDeepSeek = "deepseek";
    public const string ProviderOpenRouter = "openrouter";
    public const string ProviderGroq = "groq";
    public const string ProviderStability = "stability";
    public const string ProviderFlux = "flux";
    public const string ProviderElevenLabs = "elevenlabs";
    public const string ProviderPlayHt = "playht";

    // Models
    public const string ModelGpt4O = "gpt-4o";
    public const string ModelClaude35Sonnet = "claude-3-5-sonnet-20240620";
    public const string ModelGemini15Pro = "gemini-1.5-pro-001";
    public const string ModelGemini15Flash = "gemini-1.5-flash-001";
    public const string ModelLlama370B = "llama3-70b-8192";
    public const string ModelDeepSeekCoder = "deepseek-coder";
    public const string ModelDeepSeekChat = "deepseek-chat";

    // Godot (Adapted from Unity reference)
    public const string GodotPlatformWindows = "windows";
    public const string GodotPlatformMac = "mac";
    public const string GodotPlatformLinux = "linux";
    public const string GodotPlatformAndroid = "android";
    public const string GodotPlatformIos = "ios";

    public const string GodotVersion43 = "4.3";
    public const string GodotVersion42 = "4.2";
    public const string GodotVersion41 = "4.1";

    public const string GodotTemplate2D = "2d";
    public const string GodotTemplate3D = "3d";
    public const string GodotTemplateMobile = "mobile";
    public const string GodotTemplateVr = "vr";

    // Paths & Directories
    public const string LogsDirName = "logs";
    public const string TemplatesDirName = "templates";
    public const string OutputDirName = "output";
    public const string AssetsDirName = "Assets";
    public const string ScriptsDirName = "Scripts";

    // Defaults
    public const double DefaultTemperature = 0.7;
    public const int DefaultMaxTokens = 2048;
    public const int DefaultTimeout = 300;
}

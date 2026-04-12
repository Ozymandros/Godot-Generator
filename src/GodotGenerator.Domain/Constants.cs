namespace GodotGenerator.Domain;

/// <summary>
/// Shared constants for the Godot Generator backend.
/// </summary>
public static class Constants
{
    // Providers
    /// <summary>OpenAI provider identifier.</summary>
    public const string ProviderOpenAi = "openai";
    /// <summary>Anthropic provider identifier.</summary>
    public const string ProviderAnthropic = "anthropic";
    /// <summary>Google (Gemini) provider identifier.</summary>
    public const string ProviderGoogle = "google";
    /// <summary>Google Vertex AI provider identifier.</summary>
    public const string ProviderVertexAi = "vertex_ai";
    /// <summary>Ollama local provider identifier.</summary>
    public const string ProviderOllama = "ollama";
    /// <summary>DeepSeek provider identifier.</summary>
    public const string ProviderDeepSeek = "deepseek";
    /// <summary>OpenRouter aggregator provider identifier.</summary>
    public const string ProviderOpenRouter = "openrouter";
    /// <summary>Hugging Face provider identifier.</summary>
    public const string ProviderHuggingFace = "huggingface";
    /// <summary>Groq provider identifier.</summary>
    public const string ProviderGroq = "groq";
    /// <summary>Stability AI provider identifier.</summary>
    public const string ProviderStability = "stability";
    /// <summary>Flux image provider identifier.</summary>
    public const string ProviderFlux = "flux";
    /// <summary>ElevenLabs audio provider identifier.</summary>
    public const string ProviderElevenLabs = "elevenlabs";
    /// <summary>PlayHT provider identifier.</summary>
    public const string ProviderPlayHt = "playht";
    /// <summary>Alibaba Qwen (DashScope OpenAI-compatible) provider identifier.</summary>
    public const string ProviderQwen = "qwen";

    // Models
    /// <summary>Default GPT-4o engine value.</summary>
    public const string ModelGpt4O = "gpt-4o";
    /// <summary>Claude 3.5 Sonnet engine value.</summary>
    public const string ModelClaude35Sonnet = "claude-3-5-sonnet-20240620";
    /// <summary>Gemini 1.5 Pro engine value.</summary>
    public const string ModelGemini15Pro = "gemini-1.5-pro-001";
    /// <summary>Gemini 1.5 Flash engine value.</summary>
    public const string ModelGemini15Flash = "gemini-1.5-flash-001";
    /// <summary>Llama 3 70B (Groq) engine value.</summary>
    public const string ModelLlama370B = "llama3-70b-8192";
    /// <summary>DeepSeek Coder engine value.</summary>
    public const string ModelDeepSeekCoder = "deepseek-coder";
    /// <summary>DeepSeek Chat engine value.</summary>
    public const string ModelDeepSeekChat = "deepseek-chat";
    /// <summary>Qwen Plus chat model (DashScope compatible-mode).</summary>
    public const string ModelQwenPlus = "qwen-plus";
    /// <summary>Qwen2.5 Coder instruct model for code generation.</summary>
    public const string ModelQwen25Coder = "qwen2.5-coder-32b-instruct";

    // Godot (Adapted from Unity reference)
    /// <summary>Windows export platform token.</summary>
    public const string GodotPlatformWindows = "windows";
    /// <summary>macOS export platform token.</summary>
    public const string GodotPlatformMac = "mac";
    /// <summary>Linux export platform token.</summary>
    public const string GodotPlatformLinux = "linux";
    /// <summary>Android export platform token.</summary>
    public const string GodotPlatformAndroid = "android";
    /// <summary>iOS export platform token.</summary>
    public const string GodotPlatformIos = "ios";

    /// <summary>Godot 4.3 version label.</summary>
    public const string GodotVersion43 = "4.3";
    /// <summary>Godot 4.2 version label.</summary>
    public const string GodotVersion42 = "4.2";
    /// <summary>Godot 4.1 version label.</summary>
    public const string GodotVersion41 = "4.1";

    /// <summary>2D project template token.</summary>
    public const string GodotTemplate2D = "2d";
    /// <summary>3D project template token.</summary>
    public const string GodotTemplate3D = "3d";
    /// <summary>Mobile project template token.</summary>
    public const string GodotTemplateMobile = "mobile";
    /// <summary>VR project template token.</summary>
    public const string GodotTemplateVr = "vr";

    // Paths & Directories
    /// <summary>Relative log directory name.</summary>
    public const string LogsDirName = "logs";
    /// <summary>Relative templates directory name.</summary>
    public const string TemplatesDirName = "templates";
    /// <summary>Relative output directory name.</summary>
    public const string OutputDirName = "output";
    /// <summary>Assets folder name (Unity-style path).</summary>
    public const string AssetsDirName = "Assets";
    /// <summary>Scripts folder name.</summary>
    public const string ScriptsDirName = "Scripts";

    // Defaults
    /// <summary>Default sampling temperature when not specified.</summary>
    public const double DefaultTemperature = 0.7;
    /// <summary>Default max tokens when not specified.</summary>
    public const int DefaultMaxTokens = 2048;
    /// <summary>Default request timeout in seconds.</summary>
    public const int DefaultTimeout = 300;
}

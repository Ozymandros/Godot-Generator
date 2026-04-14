#nullable enable
using System.Text;
using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Default modality composer: merges system prompts, language preference, and project context.
/// </summary>
public sealed class ModalityTurnComposer : IModalityTurnComposer
{
    /// <summary>Option key for UI-provided preferred language (passed through to system prompt).</summary>
    public const string PreferredLanguageOptionKey = "preferred_language";

    /// <summary>Option key for GDScript vs C# preference in code-related modalities.</summary>
    public const string PreferredScriptLanguageOptionKey = "preferred_script_language";

    /// <summary>Option key for optional Godot project root path (triggers validation before LLM).</summary>
    public const string GodotProjectPathOptionKey = "godot_project_path";

    /// <inheritdoc />
    public AgentTurnRequest Compose(
        string modalityKey,
        string prompt,
        string? userSystemPrompt,
        string? projectName,
        string? preferredModelId,
        IReadOnlyDictionary<string, object?>? options)
    {
        var effectivePrompt = BuildPromptWithProjectContext(prompt, projectName);
        var system = BuildSystemPrompt(modalityKey, userSystemPrompt, options);
        return new AgentTurnRequest(
            Prompt: effectivePrompt,
            SystemPrompt: system,
            PreferredModelId: preferredModelId,
            Modality: modalityKey,
            Options: options);
    }

    private static string BuildPromptWithProjectContext(string prompt, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return prompt;
        }

        return $"[Project: {projectName.Trim()}]{Environment.NewLine}{Environment.NewLine}{prompt}";
    }

    private static string BuildSystemPrompt(
        string modalityKey,
        string? userSystemPrompt,
        IReadOnlyDictionary<string, object?>? options)
    {
        var sb = new StringBuilder();
        sb.Append(GetModalitySystemInstruction(modalityKey));

        if (!string.IsNullOrWhiteSpace(userSystemPrompt))
        {
            sb.AppendLine();
            sb.AppendLine(userSystemPrompt.Trim());
        }

        var lang = ExtractPreferredLanguage(options);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            sb.AppendLine();
            sb.Append("Respond in the user's preferred language: ");
            sb.Append(lang);
            sb.Append('.');
        }

        var scriptLang = ExtractOptionString(options, PreferredScriptLanguageOptionKey);
        if (!string.IsNullOrWhiteSpace(scriptLang))
        {
            sb.AppendLine();
            sb.Append("When generating code, prefer ");
            sb.Append(scriptLang.Trim());
            sb.Append(" unless the user specifies otherwise.");
        }

        return sb.ToString().Trim();
    }

    private static string? ExtractPreferredLanguage(IReadOnlyDictionary<string, object?>? options) =>
        ExtractOptionString(options, PreferredLanguageOptionKey);

    private static string? ExtractOptionString(IReadOnlyDictionary<string, object?>? options, string key)
    {
        if (options is null || !options.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
            _ => value.ToString()?.Trim(),
        };
    }

    /// <inheritdoc />
    public string GetSystemInstruction(string modalityKey) =>
        GetModalitySystemInstruction(modalityKey);

    private static string GetModalitySystemInstruction(string modalityKey)
    {
        return modalityKey.Trim().ToLowerInvariant() switch
        {
            "text" => "You assist with clear, accurate text for Godot-related workflows.",
            "code" => "You generate concise, idiomatic code for Godot 4 (GDScript and C# when appropriate). Prefer Godot APIs and project structure conventions.",
            "image" => "You help describe or specify image generation prompts suitable for game assets and Godot import pipelines.",
            "audio" => "You help describe or specify audio generation prompts suitable for games and Godot integration.",
            "video" => "You help describe or specify video generation prompts suitable for games and trailers.",
            "sprites" => "You help with sprite and 2D asset generation prompts for Godot games.",
            "godot-ui" => "You design and describe Godot Control nodes, themes, and UI layouts (Godot 4).",
            "godot-physics" => "You assist with Godot 4 physics: RigidBody, CharacterBody, Area, joints, and collision layers.",
            "godot-lighting" => "You configure Godot 4 lighting: DirectionalLight3D, OmniLight3D, SpotLight3D, WorldEnvironment, sky resources, and exposure settings. Emit ready-to-paste GDScript or C# snippets and scene property blocks.",
            "godot-camera" => "You set up Godot 4 cameras: Camera3D and Camera2D properties (FOV, projection, near/far, zoom), CameraPath3D, Viewport configuration, and split-screen layouts.",
            "godot-shaders" => "You write Godot 4 shaders using the Godot shading language or VisualShader graphs. Output complete, well-commented shader code with clear uniform declarations and usage examples.",
            "godot-signals" => "You wire Godot 4 signals: declare custom signals, connect them in code or the editor, write handler stubs, and use call_deferred and connect flags correctly.",
            "godot-nodes" => "You perform Godot 4 node operations via GDScript or C#: instantiate, add_child, reparent, set properties, call methods, queue_free, and manage scene-tree ownership correctly.",
            _ => "You assist the user as a Godot development copilot with access to Godot tools when appropriate.",
        };
    }
}

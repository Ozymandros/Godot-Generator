#nullable enable
using System.Text;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Serialization;

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

    /// <summary>Option key for the active project label (mirrors <c>GenerateRequest.ProjectName</c> for SK tool injection).</summary>
    public const string ProjectNameOptionKey = "project_name";

    /// <summary>Option key for default MCP 1.5 <c>fileName</c> (project-relative, POSIX-style) merged into tool calls when empty.</summary>
    public const string GodotTargetFileNameOptionKey = "godot_file_name";

    /// <summary>Option key for Godot node type preference: 2D or 3D.</summary>
    public const string GodotNodeTypeOptionKey = "godot_node_type";

    /// <inheritdoc />
    public AgentTurnRequest Compose(
        string modalityKey,
        string prompt,
        string? userSystemPrompt,
        string? projectName,
        string? preferredModelId,
        IReadOnlyDictionary<string, object?>? options)
    {
        var mergedOptions = MergeOptionsWithProjectName(projectName, options);
        var effectivePrompt = BuildPromptWithProjectContext(prompt, projectName);
        var system = BuildSystemPrompt(modalityKey, userSystemPrompt, mergedOptions);
        return new AgentTurnRequest(
            Prompt: effectivePrompt,
            SystemPrompt: system,
            PreferredModelId: preferredModelId,
            Modality: modalityKey,
            Options: mergedOptions);
    }

    private static IReadOnlyDictionary<string, object?>? MergeOptionsWithProjectName(
        string? projectName,
        IReadOnlyDictionary<string, object?>? options)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return options;
        }

        var dict = options is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(options, StringComparer.OrdinalIgnoreCase);
        dict.TryAdd(ProjectNameOptionKey, projectName.Trim());
        return dict;
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

        var nodeType = ExtractOptionString(options, GodotNodeTypeOptionKey);
        if (!string.IsNullOrWhiteSpace(nodeType))
        {
            sb.AppendLine();
            sb.Append("Generate Godot content for ");
            sb.Append(nodeType.Trim());
            sb.Append(" projects (e.g., use Node2D/Camera2D for 2D, Node3D/Camera3D for 3D).");
        }

        AppendGodotToolHints(sb, options);

        return sb.ToString().Trim();
    }

    private static void AppendGodotToolHints(StringBuilder sb, IReadOnlyDictionary<string, object?>? options)
    {
        var path = ExtractOptionString(options, GodotProjectPathOptionKey);
        var name = ExtractOptionString(options, ProjectNameOptionKey);
        var fileName = ExtractOptionString(options, GodotTargetFileNameOptionKey);
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(name))
        {
            sb.Append("Active Godot project name: ");
            sb.Append(name);
            sb.Append('.');
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                sb.AppendLine();
            }

            sb.Append("Godot project root path on disk: ");
            sb.Append(path);
            sb.Append(". When calling Godot MCP 1.5 tools, pass this value as projectPath where required.");
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(path))
            {
                sb.AppendLine();
            }

            sb.Append("Default project-relative fileName for scene/resource tools (POSIX-style under the project): ");
            sb.Append(fileName);
            sb.Append('.');
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            sb.AppendLine();
            sb.Append(
                "When tools are available, do not answer with text only. " +
                "Call Godot MCP tools to create or modify at least one project file for the request, " +
                "always passing projectPath, and include fileName for scene/resource/script-oriented tools.");

            // Inject implicit instruction to always call get_project_info before any generation
            sb.AppendLine();
            sb.Append(
                "Before generating or modifying any files, always call the MCP tool 'get_project_info' to retrieve the current project configuration from project.godot. This call should be the first tool invocation in every generation session.");
        }
    }

    private static string? ExtractPreferredLanguage(IReadOnlyDictionary<string, object?>? options) =>
        ExtractOptionString(options, PreferredLanguageOptionKey);

    private static string? ExtractOptionString(IReadOnlyDictionary<string, object?>? options, string key)
    {
        if (options is null || !options.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return JsonOptionValue.AsTrimmedString(value);
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
            "wizard" => "You are the Godot Generator Wizard — an AI orchestrator with access to all Godot 4 generation tools. Break the user's goal into tasks, call tools in logical order, and summarise results.",
            _ => "You assist the user as a Godot development copilot with access to Godot tools when appropriate.",
        };
    }
}

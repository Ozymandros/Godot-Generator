#nullable enable

namespace GodotGenerator.Desktop.Contracts.Commands;

/// <summary>Versioned command name constants for the Generate domain.</summary>
public static class GenerateCommandNames
{
    /// <summary>Generates text content.</summary>
    public const string Text = "Generate.Text/v1";

    /// <summary>Generates code content.</summary>
    public const string Code = "Generate.Code/v1";

    /// <summary>Generates image content.</summary>
    public const string Image = "Generate.Image/v1";

    /// <summary>Generates audio content.</summary>
    public const string Audio = "Generate.Audio/v1";

    /// <summary>Generates video content.</summary>
    public const string Video = "Generate.Video/v1";

    /// <summary>Generates sprite assets.</summary>
    public const string Sprites = "Generate.Sprites/v1";

    /// <summary>Generates Godot UI markup.</summary>
    public const string GodotUi = "Generate.GodotUi/v1";

    /// <summary>Generates Godot physics configuration.</summary>
    public const string GodotPhysics = "Generate.GodotPhysics/v1";

    /// <summary>Generates a Godot project scaffold.</summary>
    public const string GodotProject = "Generate.GodotProject/v1";

    /// <summary>Creates a Godot scene.</summary>
    public const string Scenes = "Generate.Scenes/v1";

    /// <summary>Generates Godot animations.</summary>
    public const string Animations = "Generate.Animations/v1";

    /// <summary>Generates Godot lighting setup (lights, environment, sky).</summary>
    public const string GodotLighting = "Generate.GodotLighting/v1";

    /// <summary>Generates Godot camera configuration (Camera3D/Camera2D, viewports).</summary>
    public const string GodotCamera = "Generate.GodotCamera/v1";

    /// <summary>Generates Godot shaders (VisualShader, ShaderMaterial, GLSL-like code).</summary>
    public const string GodotShaders = "Generate.GodotShaders/v1";

    /// <summary>Generates Godot signal wiring (declarations, connect calls, handlers).</summary>
    public const string GodotSignals = "Generate.GodotSignals/v1";

    /// <summary>Generates Godot node operations (add, configure, reparent, set properties).</summary>
    public const string GodotNodes = "Generate.GodotNodes/v1";

    /// <summary>Ordered list of all generate command names; used to register handlers.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Text, Code, Image, Audio, Video, Sprites,
        GodotUi, GodotPhysics, GodotProject, Scenes, Animations,
        GodotLighting, GodotCamera, GodotShaders, GodotSignals, GodotNodes,
    ];
}

/// <summary>
/// Shared request payload for all Generate commands.
/// Mirrors <c>GodotGenerator.Api.Dtos.GenerateRequest</c> to avoid coupling the
/// transport-independent contract to HTTP DTO assemblies.
/// </summary>
/// <param name="Prompt">Required generation prompt.</param>
/// <param name="Provider">Optional provider override.</param>
/// <param name="Options">Optional modality-specific key/value options.</param>
/// <param name="ApiKey">Optional caller-supplied API key (overrides stored key).</param>
/// <param name="SystemPrompt">Optional system-prompt override for this call.</param>
/// <param name="ProjectName">Optional Godot project name.</param>
/// <param name="PreferredModelId">Optional preferred model ID override.</param>
public sealed record GenerateCommandRequest(
    string Prompt,
    string? Provider = null,
    Dictionary<string, object?>? Options = null,
    string? ApiKey = null,
    string? SystemPrompt = null,
    string? ProjectName = null,
    string? PreferredModelId = null);

/// <summary>Response payload for all Generate commands.</summary>
/// <param name="Result">Modality-specific result fields.</param>
public sealed record GenerateCommandResponse(Dictionary<string, object?> Result);

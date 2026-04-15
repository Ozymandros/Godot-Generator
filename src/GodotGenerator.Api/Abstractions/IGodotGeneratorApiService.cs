#nullable enable
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Api.Abstractions;

/// <summary>
/// Public transport-agnostic API service that mirrors FastAPI route capabilities.
/// </summary>
public interface IGodotGeneratorApiService
{
    /// <summary>Generates text content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateTextAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates code content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateCodeAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates image content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateImageAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates audio content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateAudioAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates video content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateVideoAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates sprite content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateSpritesAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot UI content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotUiAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot physics content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotPhysicsAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot project content.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotProjectAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates a Godot scene.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> CreateSceneAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot animations.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateAnimationsAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot lighting setup (lights, environment, sky).</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotLightingAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot camera configuration (Camera3D/Camera2D, viewports).</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotCameraAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot shaders (VisualShader, ShaderMaterial, GLSL-like code).</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotShadersAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot signal wiring (declarations, connect calls, handlers).</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotSignalsAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates Godot node operations (add, configure, reparent, set properties).</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotNodesAsync(GenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Improves an existing prompt or generates a sample prompt for the given modality.
    /// Uses a direct LLM call with no plugin tools.
    /// </summary>
    Task<ApiResponse<Dictionary<string, object?>>> EnhancePromptAsync(
        PromptAssistRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a preference value by key.</summary>
    Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Sets a preference value by key.</summary>
    Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(SetPreferenceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets configured API keys.</summary>
    Task<ApiResponse<Dictionary<string, string>>> GetApiKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves API keys in batch.</summary>
    Task<ApiResponse<Dictionary<string, IReadOnlyList<string>>>> SaveApiKeysAsync(ApiKeysRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets aggregated config snapshot.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GetAllConfigAsync(CancellationToken cancellationToken = default);
}

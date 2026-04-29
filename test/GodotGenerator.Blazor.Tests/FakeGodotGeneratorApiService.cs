using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application.Dtos;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Deterministic stub for host integration tests (no LLM, MCP, or disk I/O).
/// </summary>
public sealed class FakeGodotGeneratorApiService : IGodotGeneratorApiService
{
    private static ApiResponse<Dictionary<string, object?>> OkGen(string modality) =>
        ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
        {
            ["modality"] = modality,
            ["message"] = "fake",
        });

    /// <summary>
    /// Generates text.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateTextAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("text"));

    /// <summary>
    /// Generates code.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateCodeAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("code"));

    /// <summary>
    /// Generates an image.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateImageAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("image"));

    /// <summary>
    /// Generates audio.
    /// </summary>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAudioAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("audio"));

    /// <summary>
    /// Generates a video.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateVideoAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("video"));

    /// <summary>
    /// Generates sprites.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateSpritesAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("sprites"));

    /// <summary>
    /// Generates Godot UI.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotUiAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-ui"));

    /// <summary>
    /// Generates Godot physics.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotPhysicsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-physics"));

    /// <summary>
    /// Generates Godot project.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotProjectAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-project"));

    /// <summary>
    /// Creates a scene.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> CreateSceneAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("scenes"));


    /// <summary>
    /// Generates animations.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAnimationsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
         Task.FromResult(OkGen("animations"));

    /// <summary>
    /// Generates Godot lighting.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotLightingAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-lighting"));

    /// <summary>
    /// Generates Godot camera.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotCameraAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-camera"));

    /// <summary>
    /// Generates Godot shaders.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotShadersAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-shaders"));

    /// <summary>
    /// Generates Godot signals.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotSignalsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-signals"));

    /// <summary>
    /// Generates Godot nodes.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotNodesAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-nodes"));

    /// <summary>
    /// Runs wizard orchestration.
    /// </summary>
    public Task<ApiResponse<Dictionary<string, object?>>> RunWizardAsync(WizardRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
        {
            ["modality"] = "wizard",
            ["message"] = "fake",
            ["toolsInvoked"] = Array.Empty<string>(),
        }));

    /// <summary>
    /// Gets a preference.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?> { ["value"] = null }));

    /// <summary>
    /// Sets a preference.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(SetPreferenceRequest request, CancellationToken cancellationToken = default) =>
Task.FromResult(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>()));

    /// <summary>
    /// Gets API keys.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, string>>> GetApiKeysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, string>>.Ok(new Dictionary<string, string>()));

    /// <summary>
    /// Saves API keys.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, IReadOnlyList<string>>>> SaveApiKeysAsync(ApiKeysRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, IReadOnlyList<string>>>.Ok(new Dictionary<string, IReadOnlyList<string>>()));

    /// <summary>
    /// Gets all configuration.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> GetAllConfigAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["fake"] = true }));

    /// <summary>
    /// Enhances a prompt.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ApiResponse<Dictionary<string, object?>>> EnhancePromptAsync(PromptAssistRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["message"] = "fake" }));
}

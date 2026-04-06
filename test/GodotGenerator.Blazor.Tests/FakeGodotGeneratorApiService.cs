using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;

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

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateTextAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("text"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateCodeAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("code"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateImageAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("image"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAudioAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("audio"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateVideoAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("video"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateSpritesAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("sprites"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotUiAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-ui"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotPhysicsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-physics"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateGodotProjectAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("godot-project"));

    public Task<ApiResponse<Dictionary<string, object?>>> CreateSceneAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("scenes"));

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAnimationsAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OkGen("animations"));

    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?> { ["value"] = null }));

    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(SetPreferenceRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>()));

    public Task<ApiResponse<Dictionary<string, string>>> GetApiKeysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, string>>.Ok(new Dictionary<string, string>()));

    public Task<ApiResponse<Dictionary<string, IReadOnlyList<string>>>> SaveApiKeysAsync(ApiKeysRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, IReadOnlyList<string>>>.Ok(new Dictionary<string, IReadOnlyList<string>>()));

    public Task<ApiResponse<Dictionary<string, object?>>> GetAllConfigAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["fake"] = true }));
}

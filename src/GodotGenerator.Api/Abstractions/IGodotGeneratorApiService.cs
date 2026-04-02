#nullable enable
using GodotGenerator.Api.Dtos;

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

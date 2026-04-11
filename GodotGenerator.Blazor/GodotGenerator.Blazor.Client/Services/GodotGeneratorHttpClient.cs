using System.Net.Http.Json;
using System.Text.Json;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Calls the server BFF JSON API under <c>/api/*</c>.
/// </summary>
public sealed class GodotGeneratorHttpClient(HttpClient http)
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Dispatches a generation request to the BFF route that matches <paramref name="modality"/>.
    /// </summary>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(GenerationModality modality, GenerateRequest request, CancellationToken cancellationToken = default)
    {
        var relative = modality switch
        {
            GenerationModality.Text => "api/generate/text",
            GenerationModality.Code => "api/generate/code",
            GenerationModality.Image => "api/generate/image",
            GenerationModality.Audio => "api/generate/audio",
            GenerationModality.Video => "api/generate/video",
            GenerationModality.Sprites => "api/generate/sprites",
            GenerationModality.GodotUi => "api/generate/godot-ui",
            GenerationModality.GodotPhysics => "api/generate/godot-physics",
            GenerationModality.Scenes => "api/generate/scenes",
            GenerationModality.GodotProject => "api/generate/godot-project",
            GenerationModality.Animations => "api/generate/animations",
            GenerationModality.GodotLighting => "api/generate/godot-lighting",
            GenerationModality.GodotCamera => "api/generate/godot-camera",
            GenerationModality.GodotShaders => "api/generate/godot-shaders",
            GenerationModality.GodotSignals => "api/generate/godot-signals",
            GenerationModality.GodotNodes => "api/generate/godot-nodes",
            _ => throw new ArgumentOutOfRangeException(nameof(modality), modality, null),
        };

        return PostGenerateAsync(relative, request, cancellationToken);
    }

    /// <summary>
    /// POSTs JSON to a relative URL and deserializes <see cref="ApiResponse{T}"/>; empty or invalid bodies yield a failure envelope (no exceptions).
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, object?>>> PostGenerateAsync(string relativeUrl, GenerateRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(relativeUrl, request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return ApiResponse<Dictionary<string, object?>>.Fail("Empty response.");
        }

        try
        {
            var body = JsonSerializer.Deserialize<ApiResponse<Dictionary<string, object?>>>(text, DeserializeOptions);
            return body ?? ApiResponse<Dictionary<string, object?>>.Fail("Empty response.");
        }
        catch (JsonException)
        {
            return ApiResponse<Dictionary<string, object?>>.Fail("Invalid JSON response.");
        }
    }

    /// <summary>GET <c>api/config</c> (raw HTTP; caller interprets status).</summary>
    public Task<HttpResponseMessage> GetConfigAsync(CancellationToken cancellationToken = default) =>
        http.GetAsync("api/config", cancellationToken);

    /// <summary>GET <c>api/config</c> deserialized as <see cref="ApiResponse{T}"/>.</summary>
    public async Task<ApiResponse<Dictionary<string, object?>>> GetConfigEnvelopeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/config", cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return ApiResponse<Dictionary<string, object?>>.Fail("Empty response.");
        }

        try
        {
            return JsonSerializer.Deserialize<ApiResponse<Dictionary<string, object?>>>(text, DeserializeOptions)
                   ?? ApiResponse<Dictionary<string, object?>>.Fail("Empty response.");
        }
        catch (JsonException)
        {
            return ApiResponse<Dictionary<string, object?>>.Fail("Invalid JSON response.");
        }
    }

    /// <summary>POST <c>api/preference</c> with a key/value pair.</summary>
    public async Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceEnvelopeAsync(SetPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/preference", request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Empty response.");
        }

        try
        {
            return JsonSerializer.Deserialize<ApiResponse<Dictionary<string, string?>>>(text, DeserializeOptions)
                   ?? ApiResponse<Dictionary<string, string?>>.Fail("Empty response.");
        }
        catch (JsonException)
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Invalid JSON response.");
        }
    }

    /// <summary>GET <c>api/preference/{key}</c> (raw HTTP; caller interprets status).</summary>
    public Task<HttpResponseMessage> GetPreferenceAsync(string key, CancellationToken cancellationToken = default) =>
        http.GetAsync($"api/preference/{Uri.EscapeDataString(key)}", cancellationToken);

    /// <summary>GET <c>api/preference/{key}</c> deserialized as <see cref="ApiResponse{T}"/>.</summary>
    public async Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceEnvelopeAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/preference/{Uri.EscapeDataString(key)}", cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Empty response.");
        }

        try
        {
            return JsonSerializer.Deserialize<ApiResponse<Dictionary<string, string?>>>(text, DeserializeOptions)
                   ?? ApiResponse<Dictionary<string, string?>>.Fail("Empty response.");
        }
        catch (JsonException)
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Invalid JSON response.");
        }
    }

    /// <summary>POST <c>api/keys</c> — save or remove API keys by service handle (null value removes).</summary>
    public async Task<ApiResponse<Dictionary<string, string?>>> SaveApiKeysEnvelopeAsync(ApiKeysRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/keys", request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Empty response.");
        }

        try
        {
            return JsonSerializer.Deserialize<ApiResponse<Dictionary<string, string?>>>(text, DeserializeOptions)
                   ?? ApiResponse<Dictionary<string, string?>>.Fail("Empty response.");
        }
        catch (JsonException)
        {
            return ApiResponse<Dictionary<string, string?>>.Fail("Invalid JSON response.");
        }
    }
}

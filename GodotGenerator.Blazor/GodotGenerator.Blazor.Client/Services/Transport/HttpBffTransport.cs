#nullable enable

using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;

namespace GodotGenerator.Blazor.Client.Services.Transport;

/// <summary>
/// HTTP BFF transport: delegates all calls to <see cref="GodotGeneratorHttpClient"/>.
/// This transport is the temporary fallback used in web and development modes while the
/// IPC path is being rolled out. It will be deprecated for the desktop path once
/// Phase C of the migration is complete.
/// </summary>
public sealed class HttpBffTransport : IGodotGeneratorClientTransport
{
    private readonly GodotGeneratorHttpClient _http;

    /// <summary>Initialises the transport with the existing HTTP client.</summary>
    public HttpBffTransport(GodotGeneratorHttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(
        GenerationModality modality,
        GenerateRequest request,
        CancellationToken cancellationToken = default) =>
        _http.GenerateAsync(modality, request, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, object?>>> GetConfigAsync(
        CancellationToken cancellationToken = default) =>
        _http.GetConfigEnvelopeAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        _http.GetPreferenceEnvelopeAsync(key, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SetPreferenceEnvelopeAsync(request, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, string?>>> SaveApiKeysAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SaveApiKeysEnvelopeAsync(request, cancellationToken);
}

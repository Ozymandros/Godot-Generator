#nullable enable

using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Services.Transport;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Unified client service façade that delegates all calls to the
/// <see cref="ElectronIpcTransport"/>.  The application runs exclusively inside
/// the Electron shell; HTTP is not used for UI ↔ backend communication.
/// </summary>
public sealed class GodotGeneratorClientFacade(ElectronIpcTransport transport)
{
    // ── Core API ──────────────────────────────────────────────────────────────

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(
        GenerationModality modality,
        GenerateRequest request,
        CancellationToken cancellationToken = default) =>
        transport.GenerateAsync(modality, request, cancellationToken);

    public Task<ApiResponse<Dictionary<string, object?>>> GetConfigAsync(
        CancellationToken cancellationToken = default) =>
        transport.GetConfigAsync(cancellationToken);

    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        transport.GetPreferenceAsync(key, cancellationToken);

    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        transport.SetPreferenceAsync(request, cancellationToken);

    public Task<ApiResponse<Dictionary<string, object?>>> SaveApiKeysAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default) =>
        transport.SaveApiKeysAsync(request, cancellationToken);

    // ── Envelope-named aliases (component call-site compatibility) ────────────

    public Task<ApiResponse<Dictionary<string, object?>>> GetConfigEnvelopeAsync(
        CancellationToken cancellationToken = default) =>
        GetConfigAsync(cancellationToken);

    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceEnvelopeAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        SetPreferenceAsync(request, cancellationToken);

    public Task<ApiResponse<Dictionary<string, object?>>> SaveApiKeysEnvelopeAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default) =>
        SaveApiKeysAsync(request, cancellationToken);
}

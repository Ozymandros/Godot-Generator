#nullable enable

using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Services.Transport;
using Microsoft.JSInterop;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Unified client service façade that selects the appropriate
/// <see cref="IGodotGeneratorClientTransport"/> at runtime and exposes the same method
/// signatures that pages and components already depend on.
/// </summary>
/// <remarks>
/// Transport selection logic:
/// <list type="bullet">
///   <item>
///     If <c>window.godotElectron</c> is defined (detected via JS interop), the
///     <see cref="ElectronIpcTransport"/> is used for all subsequent calls.
///   </item>
///   <item>
///     Otherwise the <see cref="HttpBffTransport"/> (HTTP BFF) is used as fallback.
///   </item>
/// </list>
/// The result is cached after the first detection call to avoid repeated JS round-trips.
/// </remarks>
public sealed class GodotGeneratorClientFacade
{
    private readonly ElectronIpcTransport _ipcTransport;
    private readonly HttpBffTransport     _httpTransport;
    private readonly IJSRuntime           _js;

    // Lazy detection: null = not yet detected, true/false = cached result.
    private bool? _isElectron;

    /// <summary>
    /// Initialises the façade with both transport implementations and the JS runtime
    /// used for desktop runtime detection.
    /// </summary>
    public GodotGeneratorClientFacade(
        ElectronIpcTransport ipcTransport,
        HttpBffTransport     httpTransport,
        IJSRuntime           js)
    {
        _ipcTransport  = ipcTransport;
        _httpTransport = httpTransport;
        _js            = js;
    }

    // ── Public API (mirrors GodotGeneratorHttpClient surface) ─────────────────

    /// <summary>Dispatches a generation request for the given modality.</summary>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(
        GenerationModality modality,
        GenerateRequest request,
        CancellationToken cancellationToken = default) =>
        GetTransport().GenerateAsync(modality, request, cancellationToken);

    /// <summary>Returns the full aggregated configuration snapshot.</summary>
    public Task<ApiResponse<Dictionary<string, object?>>> GetConfigAsync(
        CancellationToken cancellationToken = default) =>
        GetTransport().GetConfigAsync(cancellationToken);

    /// <summary>Gets a single preference value by key.</summary>
    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        GetTransport().GetPreferenceAsync(key, cancellationToken);

    /// <summary>Sets (or clears) a single preference value by key.</summary>
    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        GetTransport().SetPreferenceAsync(request, cancellationToken);

    /// <summary>Saves API keys in batch by service handle.</summary>
    public Task<ApiResponse<Dictionary<string, string?>>> SaveApiKeysAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default) =>
        GetTransport().SaveApiKeysAsync(request, cancellationToken);

    /// <summary>
    /// Returns <c>true</c> when the Electron IPC transport is active.
    /// The first call performs a JS interop probe; subsequent calls use the cached result.
    /// </summary>
    public bool IsDesktopMode => _isElectron ?? false;

    // ── Transport selection ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the active transport, detecting the runtime on the first call.
    /// Uses the cached result on all subsequent calls.
    /// </summary>
    private IGodotGeneratorClientTransport GetTransport()
    {
        // _isElectron is set synchronously once TryDetectElectronAsync has run;
        // callers in Blazor lifecycle methods should await EnsureTransportDetectedAsync()
        // before making their first transport call.
        return (_isElectron ?? false) ? _ipcTransport : _httpTransport;
    }

    /// <summary>
    /// Probes <c>window.godotElectron</c> via JS interop and caches the result.
    /// Must be awaited before the first transport call in any component that needs
    /// accurate transport selection. Subsequent calls are no-ops.
    /// </summary>
    public async ValueTask EnsureTransportDetectedAsync(CancellationToken cancellationToken = default)
    {
        if (_isElectron.HasValue) return;

        try
        {
            _isElectron = await _js
                .InvokeAsync<bool>("godotElectronInterop.isElectron", cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // JS interop unavailable (SSR, prerendering) — default to HTTP.
            _isElectron = false;
        }
    }
}

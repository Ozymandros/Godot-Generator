#nullable enable

using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;

namespace GodotGenerator.Blazor.Client.Services.Transport;

/// <summary>
/// Abstraction over the transport channel used by the Blazor WASM client to reach
/// the application backend. Two implementations exist:
/// <list type="bullet">
///   <item><see cref="HttpBffTransport"/> — delegates to the existing HTTP BFF; used in web/dev mode.</item>
///   <item><see cref="ElectronIpcTransport"/> — sends typed IPC commands via <c>window.godotElectron.invokeCommand</c>; used in desktop mode.</item>
/// </list>
/// The active transport is selected at runtime by <see cref="GodotGeneratorClientFacade"/>.
/// </summary>
public interface IGodotGeneratorClientTransport
{
    /// <summary>Dispatches a generation request for the given modality.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(
        GenerationModality modality,
        GenerateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the full aggregated configuration snapshot.</summary>
    Task<ApiResponse<Dictionary<string, object?>>> GetConfigAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets a single preference value by key.</summary>
    Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>Sets (or clears) a single preference value by key.</summary>
    Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Saves API keys in batch by service handle.</summary>
    Task<ApiResponse<Dictionary<string, string?>>> SaveApiKeysAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default);
}

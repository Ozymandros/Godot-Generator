#nullable enable

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.JSInterop;

namespace GodotGenerator.Blazor.Client.Services.Transport;

/// <summary>
/// Desktop IPC transport: sends versioned command envelopes to the local .NET backend
/// via <c>window.godotElectron.invokeCommand</c> (exposed by <c>preload.cjs</c>) and
/// deserialises the response into <see cref="ApiResponse{T}"/>.
/// </summary>
/// <remarks>
/// This transport is active only when <c>window.godotElectron</c> is present in the
/// renderer, i.e. when the Blazor app is running inside the Electron shell.
/// All JSON serialisation uses <see cref="ContractJsonOptions.Default"/> to guarantee
/// wire-format parity with the .NET pipe host.
/// Every IPC call is recorded in <see cref="AppLogService"/> as a request/response pair
/// so the Logs view can display full call history with round-trip timing.
/// </remarks>
public sealed class ElectronIpcTransport : IGodotGeneratorClientTransport
{
    private readonly IJSRuntime _js;
    private readonly AppLogService _appLog;
    private long _callSequence;

    /// <summary>Initialises the transport with the Blazor JS runtime and structured log service.</summary>
    public ElectronIpcTransport(IJSRuntime js, AppLogService appLog)
    {
        _appLog = appLog;
        _js = js;
    }

    // ── IGodotGeneratorClientTransport ────────────────────────────────────────

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(
        GenerationModality modality,
        GenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = ModalityToCommand(modality);
        var payload = new GenerateCommandRequest(
            request.Prompt,
            request.Provider,
            request.Options is not null
                ? new Dictionary<string, object?>(request.Options, StringComparer.OrdinalIgnoreCase)
                : null,
            request.ApiKey,
            request.SystemPrompt,
            request.ProjectName,
            request.PreferredModelId);

        return InvokeAsync<Dictionary<string, object?>>(command, payload, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, object?>>> GetConfigAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync<Dictionary<string, object?>>(ConfigCommandNames.GetAll, null, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<Dictionary<string, string?>>(
            PreferenceCommandNames.Get,
            new PreferenceGetRequest(key),
            cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<Dictionary<string, string?>>(
            PreferenceCommandNames.Set,
            new PreferenceSetRequest(request.Key, request.Value),
            cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, object?>>> SaveApiKeysAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<Dictionary<string, object?>>(
            KeysCommandNames.Save,
            new KeysSaveRequest(request.Keys.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)),
            cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<Dictionary<string, object?>>> EnhancePromptAsync(
        PromptAssistEnhanceRequest request,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<Dictionary<string, object?>>(
            PromptAssistCommandNames.Enhance,
            request,
            cancellationToken);

    // ── Core invoke ───────────────────────────────────────────────────────────

    /// <summary>
    /// Serialises <paramref name="payload"/> as JSON, forwards the call to
    /// <c>window.godotElectron.invokeCommand</c>, reads the response envelope, and
    /// deserialises the nested <c>payloadJson</c> field into <typeparamref name="T"/>.
    /// Records the request and response in <see cref="AppLogService"/> with round-trip timing.
    /// </summary>
    private async Task<ApiResponse<T>> InvokeAsync<T>(
        string command,
        object? payload,
        CancellationToken cancellationToken)
    {
        var correlationId = $"ipc-{System.Threading.Interlocked.Increment(ref _callSequence)}";
        _appLog.LogIpcRequest(command, correlationId);
        var sw = Stopwatch.StartNew();

        var payloadJson = payload is not null
            ? JsonSerializer.Serialize(payload, ContractJsonOptions.Default)
            : null;

        IpcResponseJs? envelope;
        try
        {
            envelope = await _js.InvokeAsync<IpcResponseJs?>(
                "godotElectronInterop.invokeCommand",
                cancellationToken,
                command,
                payloadJson);
        }
        catch (JSException jsEx)
        {
            sw.Stop();
            _appLog.LogIpcResponse(command, correlationId, success: false,
                durationMs: sw.Elapsed.TotalMilliseconds, errorCode: "JS_EXCEPTION");
            return ApiResponse<T>.Fail($"IPC call failed: {jsEx.Message}");
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            _appLog.LogIpcResponse(command, correlationId, success: false,
                durationMs: sw.Elapsed.TotalMilliseconds, errorCode: "CANCELLED");
            return ApiResponse<T>.Fail("IPC call was cancelled.");
        }

        if (envelope is null)
        {
            sw.Stop();
            _appLog.LogIpcResponse(command, correlationId, success: false,
                durationMs: sw.Elapsed.TotalMilliseconds, errorCode: "NULL_ENVELOPE");
            return ApiResponse<T>.Fail("IPC call returned null.");
        }

        if (!envelope.Success)
        {
            sw.Stop();
            var errorCode = envelope.ErrorCode ?? "UNKNOWN";
            _appLog.LogIpcResponse(command, correlationId, success: false,
                durationMs: sw.Elapsed.TotalMilliseconds, errorCode: errorCode);
            return ApiResponse<T>.Fail(envelope.ErrorMessage ?? errorCode);
        }

        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            sw.Stop();
            _appLog.LogIpcResponse(command, correlationId, success: false,
                durationMs: sw.Elapsed.TotalMilliseconds, errorCode: "EMPTY_PAYLOAD");
            return ApiResponse<T>.Fail("IPC response payload was empty.");
        }

        try
        {
            var data = JsonSerializer.Deserialize<T>(envelope.PayloadJson, ContractJsonOptions.Default);
            if (data is null)
            {
                sw.Stop();
                _appLog.LogIpcResponse(command, correlationId, success: false,
                    durationMs: sw.Elapsed.TotalMilliseconds, errorCode: "NULL_DATA");
                return ApiResponse<T>.Fail("IPC response payload deserialised to null.");
            }

            sw.Stop();
            _appLog.LogIpcResponse(command, correlationId, success: true,
                durationMs: sw.Elapsed.TotalMilliseconds);
            return ApiResponse<T>.Ok(data);
        }
        catch (JsonException ex)
        {
            sw.Stop();
            _appLog.LogIpcResponse(command, correlationId, success: false,
                durationMs: sw.Elapsed.TotalMilliseconds, errorCode: "JSON_ERROR");
            return ApiResponse<T>.Fail($"IPC response payload could not be deserialised: {ex.Message}");
        }
    }

    // ── Modality mapping ──────────────────────────────────────────────────────

    private static string ModalityToCommand(GenerationModality modality) => modality switch
    {
        GenerationModality.Text => GenerateCommandNames.Text,
        GenerationModality.Code => GenerateCommandNames.Code,
        GenerationModality.Image => GenerateCommandNames.Image,
        GenerationModality.Audio => GenerateCommandNames.Audio,
        GenerationModality.Video => GenerateCommandNames.Video,
        GenerationModality.Sprites => GenerateCommandNames.Sprites,
        GenerationModality.GodotUi => GenerateCommandNames.GodotUi,
        GenerationModality.GodotPhysics => GenerateCommandNames.GodotPhysics,
        GenerationModality.GodotProject => GenerateCommandNames.GodotProject,
        GenerationModality.Scenes => GenerateCommandNames.Scenes,
        GenerationModality.Animations => GenerateCommandNames.Animations,
        GenerationModality.GodotLighting => GenerateCommandNames.GodotLighting,
        GenerationModality.GodotCamera => GenerateCommandNames.GodotCamera,
        GenerationModality.GodotShaders => GenerateCommandNames.GodotShaders,
        GenerationModality.GodotSignals => GenerateCommandNames.GodotSignals,
        GenerationModality.GodotNodes => GenerateCommandNames.GodotNodes,
        GenerationModality.Wizard => GenerateCommandNames.Wizard,
        _ => throw new ArgumentOutOfRangeException(nameof(modality), modality, null),
    };

    // ── JS interop DTO ────────────────────────────────────────────────────────

    /// <summary>Mirrors the response shape returned by <c>pipeBroker.invoke</c>.</summary>
    private sealed class IpcResponseJs
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("payloadJson")]
        public string? PayloadJson { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; init; }
    }
}

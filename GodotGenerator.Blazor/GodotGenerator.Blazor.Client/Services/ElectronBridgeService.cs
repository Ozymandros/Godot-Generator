#nullable enable

using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Bridges Electron push-event subscriptions into C# event handlers.
/// </summary>
/// <remarks>
/// On <see cref="InitializeAsync"/> this service registers JS callbacks for all
/// four Electron push events by passing a <c>DotNetObjectReference</c> to
/// <c>godotElectronInterop.subscribeLifecycleEvents</c> and
/// <c>godotElectronInterop.subscribeFolderSelected</c>.
///
/// When not running in Electron (web mode) the JS functions are no-ops, so this
/// service is safe to initialise unconditionally.
///
/// Registered as Scoped in both the client and the server-side host so that the
/// <see cref="IJSRuntime"/> scope is respected.
/// </remarks>
public sealed class ElectronBridgeService : IDisposable, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly StatusBannerService _statusBanner;
    private readonly AppLogService _appLog;

    private DotNetObjectReference<ElectronBridgeService>? _dotNetRef;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initialises the service with required dependencies.</summary>
    public ElectronBridgeService(IJSRuntime js, StatusBannerService statusBanner, AppLogService appLog)
    {
        _js = js;
        _statusBanner = statusBanner;
        _appLog = appLog;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the Electron File menu selects a folder path via the
    /// "Open Project Folder…" menu item. Carries the selected absolute path.
    /// </summary>
    public event Action<string>? FolderSelected;

    /// <summary>
    /// Raised when the Electron File menu triggers "New Project".
    /// Subscribers should reset all in-memory project state.
    /// </summary>
    public event Action? NewProject;

    /// <summary>
    /// Raised when the local .NET backend IPC pipe becomes ready (first start or after restart).
    /// UI that depends on <c>Config.GetAll</c> can retry loading if the first attempt failed.
    /// </summary>
    public event Action? BackendReady;

    /// <summary>
    /// Registers Electron push-event subscriptions. Must be called once from
    /// <c>OnAfterRenderAsync(firstRender: true)</c> in an interactive WASM
    /// component. Subsequent calls are no-ops.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        _dotNetRef = DotNetObjectReference.Create(this);
        try
        {
            await _js.InvokeVoidAsync(
                "godotElectronInterop.subscribeLifecycleEvents",
                _dotNetRef).ConfigureAwait(false);

            await _js.InvokeVoidAsync(
                "godotElectronInterop.subscribeFolderSelected",
                _dotNetRef).ConfigureAwait(false);

            await _js.InvokeVoidAsync(
                "godotElectronInterop.subscribeNewProject",
                _dotNetRef).ConfigureAwait(false);

            await _js.InvokeVoidAsync(
                "godotElectronInterop.subscribeBackendLog",
                _dotNetRef).ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Not running inside Electron or JS bridge not yet loaded — safe to ignore.
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering — ignore.
        }
    }

    // ── [JSInvokable] callbacks (called by electronBridge.js) ─────────────────

    /// <summary>
    /// Invoked by <c>electronBridge.js</c> when the .NET backend pipe becomes ready
    /// (initial start or after a supervised restart).
    /// </summary>
    [JSInvokable]
    public void OnBackendReady()
    {
        BackendReady?.Invoke();
        _statusBanner.Set("Backend ready.", MessageIntent.Success);
    }

    /// <summary>
    /// Invoked by <c>electronBridge.js</c> when the .NET backend process exits
    /// unexpectedly. The supervisor will attempt to restart it automatically.
    /// </summary>
    /// <param name="code">OS exit code, or <c>null</c> if terminated by a signal.</param>
    /// <param name="signal">Signal name (e.g. <c>SIGTERM</c>), or <c>null</c> if not signal-terminated.</param>
    [JSInvokable]
    public void OnBackendCrashed(int? code, string? signal)
    {
        var detail = code.HasValue ? $" (exit code {code})"
                   : signal is not null ? $" (signal {signal})"
                   : string.Empty;

        _statusBanner.Set($"Backend crashed{detail} — restarting…", MessageIntent.Warning);
    }

    /// <summary>
    /// Invoked by <c>electronBridge.js</c> when the backend has exceeded the
    /// maximum restart attempts and will no longer be supervised automatically.
    /// </summary>
    [JSInvokable]
    public void OnBackendFailed() =>
        _statusBanner.Set(
            "Backend failed to restart. Please close and reopen the app.",
            MessageIntent.Error);

    /// <summary>
    /// Invoked by <c>electronBridge.js</c> when the Electron File menu's
    /// "Open Project Folder…" item picks a directory.
    /// Raises <see cref="FolderSelected"/> for subscribers (e.g. <c>ProjectHeaderClient</c>).
    /// </summary>
    /// <param name="path">Absolute path of the selected folder.</param>
    [JSInvokable]
    public void OnFolderSelected(string path) =>
        FolderSelected?.Invoke(path);

    /// <summary>
    /// Invoked by <c>electronBridge.js</c> when the Electron File menu's
    /// "New Project" item is activated. Raises <see cref="NewProject"/> so
    /// subscribers can reset all in-memory project state.
    /// </summary>
    [JSInvokable]
    public void OnNewProject() =>
        NewProject?.Invoke();

    /// <summary>
    /// Invoked by <c>electronBridge.js</c> for each individual line emitted by
    /// the backend process stdout or stderr.  Forwards the line into
    /// <see cref="AppLogService"/> so it is visible in the in-app log view.
    /// </summary>
    /// <param name="stream">
    /// <c>"stdout"</c> for standard output; <c>"stderr"</c> for standard error.
    /// </param>
    /// <param name="message">A single, non-empty, pre-split log line.</param>
    /// <param name="timestamp">ISO-8601 timestamp from the Electron main process (informational).</param>
    [JSInvokable]
    public void OnBackendLog(string stream, string message, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        // Timestamp from Electron is informational; AppLogService assigns its own
        // wall-clock stamp for consistency with other entry types.
        _ = timestamp;
        _appLog.LogBackend(stream, message);
    }

    // ── IDisposable / IAsyncDisposable ─────────────────────────────────────────

    /// <summary>
    /// Synchronous disposal path used when a host disposes the DI scope without
    /// <see cref="DisposeAsync"/> (for example bUnit's test service provider).
    /// Releases the DotNet reference; async JS teardown is skipped.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _disposed = true;
    }

    /// <summary>
    /// Removes all Electron push-event subscriptions registered during
    /// <see cref="InitializeAsync"/> and disposes the DotNet object reference.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_initialized)
        {
            try
            {
                await _js.InvokeVoidAsync("godotElectronInterop.disposeSubscriptions")
                         .ConfigureAwait(false);
            }
            catch
            {
                // Suppress all JS interop errors during disposal (page may already be unloading).
            }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _disposed = true;
    }
}

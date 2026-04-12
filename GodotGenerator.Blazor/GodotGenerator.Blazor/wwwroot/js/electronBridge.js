/**
 * @file electronBridge.js
 * Thin JS helpers for Blazor ↔ Electron interop.
 * Loaded by the Blazor host page; safe to include in web mode — all methods
 * are no-ops / return null when the Electron preload namespaces are absent.
 *
 * Electron preload exposes three namespaces (via electron-message-bridge):
 *
 *   window.godotElectron        — request/response API (showOpenDialog, invokeCommand)
 *   window.godotElectronEvents  — push-event subscriptions (backendReady, etc.)
 *   window.godotElectronMeta    — read-only static constants (platform)
 *
 * Event subscription functions return an unsubscribe callback.
 * `subscribeLifecycleEvents` and `subscribeFolderSelected` store those
 * callbacks internally; call `disposeSubscriptions()` to remove them all.
 */

/** @returns {boolean} */
function _hasApi() {
  return typeof window.godotElectron !== 'undefined' && window.godotElectron !== null;
}

/** @returns {boolean} */
function _hasEvents() {
  return typeof window.godotElectronEvents !== 'undefined' && window.godotElectronEvents !== null;
}

/** No-op unsubscribe returned when not running in Electron. */
const _noopUnsub = () => {};

/** Internal store of all active unsubscribe functions; cleared by disposeSubscriptions(). */
const _allUnsubs = [];

window.godotElectronInterop = {
  // ── Detection & meta ──────────────────────────────────────────────────────

  /**
   * Returns true when the app is running inside the Electron shell.
   * Used by GodotGeneratorClientFacade to select the active transport.
   * @returns {boolean}
   */
  isElectron: function () {
    return _hasApi();
  },

  /**
   * Returns the current platform string (e.g. `'win32'`, `'darwin'`),
   * or `null` when not running in Electron.
   * @returns {string|null}
   */
  platform: function () {
    return (typeof window.godotElectronMeta !== 'undefined' && window.godotElectronMeta !== null)
      ? (window.godotElectronMeta.platform ?? null)
      : null;
  },

  // ── Request / response API ────────────────────────────────────────────────

  /**
   * Opens the native folder picker and resolves with the selected path,
   * or null if the user cancelled or Electron is not present.
   * @returns {Promise<string|null>}
   */
  pickFolder: async function () {
    if (!_hasApi()) return null;
    const picked = await window.godotElectron.showOpenDialog({ properties: ['openDirectory'] });
    return picked ?? null;
  },

  /**
   * Sends a versioned IPC command to the local .NET backend via the Electron
   * preload bridge and resolves with the raw response envelope object.
   *
   * Called by ElectronIpcTransport (C# JSRuntime.InvokeAsync).
   *
   * @param {string}      command      Versioned command name, e.g. "Config.GetAll/v1".
   * @param {string|null} payloadJson  JSON string of the command payload, or null.
   * @returns {Promise<{success: boolean, payloadJson: string|null, errorCode: string|null, errorMessage: string|null}>}
   */
  invokeCommand: async function (command, payloadJson) {
    if (!_hasApi()) {
      return { success: false, payloadJson: null, errorCode: 'NOT_ELECTRON', errorMessage: 'Not running in Electron.' };
    }
    return await window.godotElectron.invokeCommand(command, payloadJson ?? null);
  },

  // ── Push-event subscriptions (direct) ────────────────────────────────────

  /**
   * Subscribes to folder-selected events pushed from the File menu.
   * Returns an unsubscribe function; call it when the subscriber is disposed.
   * @param {(folderPath: string) => void} callback
   * @returns {() => void} unsubscribe
   */
  onFolderSelected: function (callback) {
    if (!_hasEvents()) return _noopUnsub;
    return window.godotElectronEvents.folderSelected(callback);
  },

  /**
   * Subscribes to backend-ready notifications (initial start or supervised restart).
   * Returns an unsubscribe function; call it when the subscriber is disposed.
   * @param {() => void} callback
   * @returns {() => void} unsubscribe
   */
  onBackendReady: function (callback) {
    if (!_hasEvents()) return _noopUnsub;
    return window.godotElectronEvents.backendReady(callback);
  },

  /**
   * Subscribes to backend-crashed notifications so the UI can show a degraded-state banner.
   * Returns an unsubscribe function; call it when the subscriber is disposed.
   * @param {(detail: {code: number|null, signal: string|null}) => void} callback
   * @returns {() => void} unsubscribe
   */
  onBackendCrashed: function (callback) {
    if (!_hasEvents()) return _noopUnsub;
    return window.godotElectronEvents.backendCrashed(callback);
  },

  /**
   * Subscribes to the permanent backend-failure event (max restarts exceeded).
   * Returns an unsubscribe function; call it when the subscriber is disposed.
   * @param {() => void} callback
   * @returns {() => void} unsubscribe
   */
  onBackendFailed: function (callback) {
    if (!_hasEvents()) return _noopUnsub;
    return window.godotElectronEvents.backendFailed(callback);
  },

  // ── DotNet-bridge subscriptions (used by ElectronBridgeService) ───────────
  //
  // These functions accept a DotNetObjectReference and subscribe to Electron
  // push events, forwarding payloads to [JSInvokable] methods on the C# service.
  // Unsubscribe callbacks are stored in _allUnsubs; call disposeSubscriptions()
  // to remove all listeners (called from ElectronBridgeService.DisposeAsync).

  /**
   * Subscribes to all three backend lifecycle events and routes them to the
   * given DotNet reference's [JSInvokable] methods:
   *   OnBackendReady()
   *   OnBackendCrashed(int? code, string? signal)
   *   OnBackendFailed()
   *
   * @param {DotNetObjectReference} dotNetRef
   */
  subscribeLifecycleEvents: function (dotNetRef) {
    if (!_hasEvents()) return;

    _allUnsubs.push(
      window.godotElectronEvents.backendReady(() => {
        dotNetRef.invokeMethodAsync('OnBackendReady').catch(console.error);
      }),
      window.godotElectronEvents.backendCrashed((detail) => {
        dotNetRef.invokeMethodAsync(
          'OnBackendCrashed',
          detail?.code   ?? null,
          detail?.signal ?? null
        ).catch(console.error);
      }),
      window.godotElectronEvents.backendFailed(() => {
        dotNetRef.invokeMethodAsync('OnBackendFailed').catch(console.error);
      }),
    );
  },

  /**
   * Subscribes to the folder-selected push event and routes it to the given
   * DotNet reference's [JSInvokable] method:
   *   OnFolderSelected(string path)
   *
   * @param {DotNetObjectReference} dotNetRef
   */
  subscribeFolderSelected: function (dotNetRef) {
    if (!_hasEvents()) return;

    _allUnsubs.push(
      window.godotElectronEvents.folderSelected((path) => {
        dotNetRef.invokeMethodAsync('OnFolderSelected', path).catch(console.error);
      }),
    );
  },

  /**
   * Removes all event listeners registered via subscribeLifecycleEvents /
   * subscribeFolderSelected. Called from ElectronBridgeService.DisposeAsync.
   */
  disposeSubscriptions: function () {
    _allUnsubs.forEach((unsub) => unsub());
    _allUnsubs.length = 0;
  },
};

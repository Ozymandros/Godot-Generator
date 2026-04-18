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
 * Additionally, speech-to-text is exposed as `window.speech` via the
 * @ozymandros/electron-message-bridge-plugin-speech-whisper plugin:
 *
 *   window.speech.start()       — Start microphone capture
 *   window.speech.stop()        — Stop capture and run Whisper STT
 *   window.speech.status()      — Get STT capability status
 *   window.speech.onTranscript(cb) — Subscribe to transcript results (returns unsubscribe fn)
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

  /**
   * Subscribes to individual backend log lines streamed from stdout/stderr.
   * Returns an unsubscribe function; call it when the subscriber is disposed.
   * @param {(payload: {stream: 'stdout'|'stderr', message: string, timestamp: string}) => void} callback
   * @returns {() => void} unsubscribe
   */
  onBackendLog: function (callback) {
    if (!_hasEvents()) return _noopUnsub;
    return window.godotElectronEvents.backendLog(callback);
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
   * Subscribes to backend log lines and routes each to the given DotNet
   * reference's [JSInvokable] method:
   *   OnBackendLog(string stream, string message, string timestamp)
   *
   * Each payload carries a single pre-split, non-empty log line.
   * Unsubscribe is stored internally; call `disposeSubscriptions()` to clean up.
   *
   * @param {DotNetObjectReference} dotNetRef
   */
  subscribeBackendLog: function (dotNetRef) {
    if (!_hasEvents()) return;

    _allUnsubs.push(
      window.godotElectronEvents.backendLog((payload) => {
        dotNetRef.invokeMethodAsync(
          'OnBackendLog',
          payload?.stream    ?? 'stdout',
          payload?.message   ?? '',
          payload?.timestamp ?? new Date().toISOString(),
        ).catch(console.error);
      }),
    );
  },

  /**
   * Removes all event listeners registered via subscribeLifecycleEvents /
   * subscribeFolderSelected / subscribeBackendLog. Called from ElectronBridgeService.DisposeAsync.
   */
  disposeSubscriptions: function () {
    _allUnsubs.forEach((unsub) => unsub());
    _allUnsubs.length = 0;
  },

  // ── Wizard progress (scoped, per-component) ───────────────────────────────
  //
  // Unlike the global _allUnsubs pool, wizard progress subscriptions are keyed
  // by a caller-supplied string so each WizardPanel instance can clean up only
  // its own listener when it disposes, without affecting other subscribers.

  /** @type {Map<string, () => void>} */
  _wizardProgressUnsubs: new Map(),

  /**
   * Subscribes to wizard progress frames and routes each to the given DotNet
   * reference's [JSInvokable] method:
   *   OnWizardProgress(string phase, string message, string? toolPlugin, string? toolName)
   *
   * The `key` parameter scopes the subscription; call `unsubscribeWizardProgress(key)`
   * from the component's DisposeAsync to remove only this listener.
   * Replaces any existing subscription registered under the same key.
   *
   * @param {DotNetObjectReference} dotNetRef
   * @param {string} key  Caller-defined identifier, e.g. "wizard-panel".
   */
  subscribeWizardProgress: function (dotNetRef, key) {
    if (!_hasEvents()) return;

    // Remove any previous subscription for this key before re-subscribing.
    const existing = this._wizardProgressUnsubs.get(key);
    if (existing) { existing(); this._wizardProgressUnsubs.delete(key); }

    const unsub = window.godotElectronEvents.wizardProgress((frame) => {
      dotNetRef.invokeMethodAsync(
        'OnWizardProgress',
        frame?.phase      ?? 'status',
        frame?.message    ?? '',
        frame?.toolPlugin ?? null,
        frame?.toolName   ?? null,
      ).catch(console.error);
    });

    this._wizardProgressUnsubs.set(key, unsub);
  },

  /**
   * Removes the wizard progress subscription registered under `key`.
   * Safe to call when no subscription exists for that key.
   *
   * @param {string} key  Same key passed to `subscribeWizardProgress`.
   */
  unsubscribeWizardProgress: function (key) {
    const unsub = this._wizardProgressUnsubs.get(key);
    if (unsub) {
      unsub();
      this._wizardProgressUnsubs.delete(key);
    }
  },

  // ── Speech-to-text helpers (Whisper.cpp via electron-message-bridge-plugin-speech-whisper) ──

  /**
   * Returns true when speech-to-text is available in the Electron shell.
   * @returns {boolean}
   */
  hasSpeech: function () {
    return typeof window.speech !== 'undefined' && window.speech !== null;
  },

  /**
   * Gets the STT status (capabilities, current state, errors).
   * @returns {Promise<{canRecord: boolean, hasModel: boolean, hasBinary: boolean, state: string, error?: string}>}
   */
  getSpeechStatus: async function () {
    if (!this.hasSpeech()) return { canRecord: false, hasModel: false, hasBinary: false, state: 'UNSUPPORTED' };
    return await window.speech.status();
  },

  /**
   * Starts microphone capture for speech-to-text.
   * Call `stopSpeech()` on the same window to finalize and receive transcript.
   * @returns {Promise<void>}
   */
  startSpeech: async function () {
    if (!this.hasSpeech()) throw new Error('Speech-to-text not available.');
    try {
      const status = await window.speech.status();
      console.info('[godotElectronInterop] startSpeech status before start:', status);
    } catch (err) {
      console.warn('[godotElectronInterop] Unable to read speech status before start:', err);
    }
    return await window.speech.start();
  },

  /**
   * Stops microphone capture, runs Whisper STT, and emits transcript via `onTranscript`.
   * Must be called from the same BrowserWindow that called `startSpeech()`.
   * @returns {Promise<void>}
   */
  stopSpeech: async function () {
    if (!this.hasSpeech()) throw new Error('Speech-to-text not available.');
    try {
      const status = await window.speech.status();
      const state = typeof status?.state === 'string' ? status.state.toUpperCase() : '';
      // Avoid stop only when state is explicitly idle.
      // Runtime evidence showed ERROR can still follow an active LISTENING session.
      if (state && state !== 'LISTENING') {
        console.info('[godotElectronInterop] stopSpeech skipped: not recording.', status);
        return;
      }
    } catch {
      // If status probing fails, continue and let stop() decide.
    }

    try {
      const result = await window.speech.stop();
      return result;
    } catch (err) {
      const message = typeof err?.message === 'string' ? err.message : String(err ?? '');
      // Idempotent stop semantics for UI calls (race between transcript auto-stop and UI stop).
      if (message.includes('No active recording')) {
        try {
          const status = await window.speech.status();
          console.info('[godotElectronInterop] stopSpeech received "No active recording". Current status:', status);
        } catch {
          console.info('[godotElectronInterop] stopSpeech received "No active recording".');
        }
        return;
      }
      throw err;
    }
  },

  /**
   * Subscribes to speech transcript results. Callback receives plain text string.
   * Returns an unsubscribe function to clean up the listener.
   * @param {(text: string) => void} callback
   * @returns {() => void} unsubscribe
   */
  onSpeechTranscript: function (callback) {
    if (!this.hasSpeech()) return () => {};
    return window.speech.onTranscript(callback);
  },
};

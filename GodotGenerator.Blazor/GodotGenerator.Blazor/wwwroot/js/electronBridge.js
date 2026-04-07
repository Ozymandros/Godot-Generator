/**
 * @file electronBridge.js
 * Thin JS helpers for Blazor ↔ Electron interop.
 * Loaded by the Blazor host page; safe to include in web mode (all methods
 * are no-ops / return null when window.godotElectron is absent).
 */

window.godotElectronInterop = {
  /**
   * Returns true when the app is running inside the Electron shell.
   * Used by GodotGeneratorClientFacade to select the active transport.
   * @returns {boolean}
   */
  isElectron: function () {
    return typeof window.godotElectron !== 'undefined' && window.godotElectron !== null;
  },

  /**
   * Opens the native folder picker and resolves with the selected path,
   * or null if the user cancelled or Electron is not present.
   * @returns {Promise<string|null>}
   */
  pickFolder: async function () {
    if (!window.godotElectronInterop.isElectron()) {
      return null;
    }
    const path = await window.godotElectron.showOpenDialog({
      properties: ['openDirectory'],
    });
    return path ?? null;
  },

  /**
   * Sends a versioned IPC command to the local .NET backend via the Electron
   * preload bridge and resolves with the raw response envelope object.
   *
   * Called by ElectronIpcTransport (C# JSRuntime.InvokeAsync).
   *
   * @param {string} command     Versioned command name, e.g. "Config.GetAll/v1".
   * @param {string|null} payloadJson  JSON string of the command payload, or null.
   * @returns {Promise<{success: boolean, payloadJson: string|null, errorCode: string|null, errorMessage: string|null}|null>}
   */
  invokeCommand: async function (command, payloadJson) {
    if (!window.godotElectronInterop.isElectron()) {
      return { success: false, payloadJson: null, errorCode: 'NOT_ELECTRON', errorMessage: 'Not running in Electron.' };
    }
    return await window.godotElectron.invokeCommand(command, payloadJson ?? null);
  },

  /**
   * Registers a callback invoked whenever the backend pipe becomes ready
   * (initial start or after a supervised restart).
   * @param {() => void} callback
   */
  onBackendReady: function (callback) {
    if (window.godotElectronInterop.isElectron()) {
      window.godotElectron.onBackendReady(callback);
    }
  },

  /**
   * Registers a callback invoked when the backend process crashes unexpectedly.
   * @param {(detail: {code: number|null, signal: string|null}) => void} callback
   */
  onBackendCrashed: function (callback) {
    if (window.godotElectronInterop.isElectron()) {
      window.godotElectron.onBackendCrashed(callback);
    }
  },

  /**
   * Registers a callback invoked when the backend has exceeded the maximum
   * restart attempts and will no longer be supervised.
   * @param {() => void} callback
   */
  onBackendFailed: function (callback) {
    if (window.godotElectronInterop.isElectron()) {
      window.godotElectron.onBackendFailed(callback);
    }
  },
};

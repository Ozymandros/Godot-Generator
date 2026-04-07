'use strict';

const { contextBridge, ipcRenderer } = require('electron');

/**
 * Exposes a minimal, audited surface to the renderer (Blazor WASM) via
 * contextBridge. No Node.js or Electron internals are forwarded — only the
 * specific, named functions listed here.
 *
 * All renderer→backend command traffic flows through `invokeCommand`, which
 * forwards to `ipcMain.handle('godot:invoke-command')` in main.cjs.
 */
contextBridge.exposeInMainWorld('godotElectron', {
  /** Current platform string, e.g. `'win32'`. */
  platform: process.platform,

  /**
   * Shows the native open-folder dialog and resolves with the selected path,
   * or `null` if the user cancelled.
   * @param {Electron.OpenDialogOptions} [options]
   * @returns {Promise<string|null>}
   */
  showOpenDialog: (options) => ipcRenderer.invoke('godot:show-open-dialog', options),

  /**
   * Subscribes to folder-selected events pushed from the File menu.
   * @param {(folderPath: string) => void} callback
   */
  onFolderSelected: (callback) => {
    ipcRenderer.on('godot:folder-selected', (_e, folderPath) => {
      callback(folderPath);
    });
  },

  /**
   * Sends a versioned IPC command to the local .NET backend via the named pipe
   * and resolves with the response envelope.
   *
   * @param {string} command     Versioned command name, e.g. `Config.GetAll/v1`.
   * @param {string|null} payloadJson  JSON-stringified command payload, or null.
   * @returns {Promise<{success: boolean, payloadJson: string|null, errorCode: string|null, errorMessage: string|null}>}
   */
  invokeCommand: (command, payloadJson) =>
    ipcRenderer.invoke('godot:invoke-command', command, payloadJson),

  /**
   * Subscribes to backend-ready notifications (e.g. after a supervised restart).
   * @param {() => void} callback
   */
  onBackendReady: (callback) => {
    ipcRenderer.on('godot:backend-ready', () => callback());
  },

  /**
   * Subscribes to backend-crashed notifications so the UI can show a degraded-state banner.
   * @param {(detail: {code: number|null, signal: string|null}) => void} callback
   */
  onBackendCrashed: (callback) => {
    ipcRenderer.on('godot:backend-crashed', (_e, detail) => callback(detail));
  },

  /**
   * Subscribes to the permanent backend-failure event (max restarts exceeded).
   * @param {() => void} callback
   */
  onBackendFailed: (callback) => {
    ipcRenderer.on('godot:backend-failed', () => callback());
  },
});

'use strict';

/**
 * @file ipcDefinitions.cjs
 * Single source of truth for IPC channel names shared between the main process
 * and the preload script.
 *
 * Keeping channel names here prevents typo-driven mismatches: `defineIpcApi`
 * in main.cjs uses these strings as handler keys; `exposeApiToRenderer` /
 * `exposeEventsToRenderer` in preload.cjs use them as the `_channels` list.
 *
 * Add or rename a channel in ONE place — both sides stay in sync.
 */

/**
 * Request/response channels (renderer → main via `ipcRenderer.invoke`).
 * @type {readonly string[]}
 */
const API_CHANNELS = Object.freeze([
  /** Opens the native folder-picker dialog; returns selected path or null. */
  'showOpenDialog',
  /** Forwards a versioned command envelope to the .NET backend via named pipe. */
  'invokeCommand',
]);

/**
 * Push-event channels (main → renderer via `webContents.send`).
 * @type {readonly string[]}
 */
const EVENT_CHANNELS = Object.freeze([
  /** File-menu folder selection forwarded to the renderer. */
  'folderSelected',
  /** Backend pipe became ready (initial start or supervised restart). */
  'backendReady',
  /** Backend process crashed unexpectedly; detail: { code, signal }. */
  'backendCrashed',
  /** Backend exceeded max restart attempts; no further supervision. */
  'backendFailed',
  /**
   * A single line of backend stdout/stderr output.
   * Payload: `{ stream: 'stdout'|'stderr', message: string, timestamp: string }`.
   */
  'backendLog',
]);

module.exports = { API_CHANNELS, EVENT_CHANNELS };

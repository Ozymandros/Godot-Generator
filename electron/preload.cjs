'use strict';

const { exposeApiToRenderer, exposeEventsToRenderer, exposeValues } =
  require('@ozymandros/electron-message-bridge/preload');
const { exposeSpeechWhisperToRenderer } =
  require('@ozymandros/electron-message-bridge-plugin-speech-whisper/preload');
const { API_CHANNELS, EVENT_CHANNELS } = require('./ipcDefinitions.cjs');

/**
 * @file preload.cjs
 * Bridges the typed IPC surface from the main process into the sandboxed
 * renderer (Blazor WASM) via `contextBridge`.
 *
 * Three namespaces are exposed on `window`:
 *
 *   window.godotElectron       — request/response API (invokeCommand, showOpenDialog)
 *   window.godotElectronEvents — push-event subscriptions (backendReady, etc.)
 *   window.godotElectronMeta   — read-only static constants (platform)
 *
 * No Node.js or Electron internals are forwarded to the renderer; only the
 * specific, named channels declared in `ipcDefinitions.cjs` are accessible.
 *
 * Channel-to-function mapping is derived entirely from `API_CHANNELS` /
 * `EVENT_CHANNELS` — adding or renaming a channel in `ipcDefinitions.cjs`
 * automatically propagates here and to main.cjs without manual edits.
 *
 * Event subscription functions return an **unsubscribe** callback so callers
 * can clean up listeners when a Blazor component is disposed:
 *
 *   const unsub = window.godotElectronEvents.backendReady(() => { ... });
 *   // later:
 *   unsub();
 */

// Request/response API → window.godotElectron
// Each key maps to ipcRenderer.invoke(channel, ...args) where channel === key.
exposeApiToRenderer({ _channels: API_CHANNELS }, 'godotElectron');

// Push events → window.godotElectronEvents
// Each key maps to ipcRenderer.on(channel, listener) and returns unsub fn.
exposeEventsToRenderer({ _channels: EVENT_CHANNELS }, 'godotElectronEvents');

// Static constants → window.godotElectronMeta
// Exposed without an IPC round-trip; no Node.js globals leak to the renderer.
exposeValues({ platform: process.platform }, 'godotElectronMeta');

// Speech-to-text (Whisper.cpp) → window.speech
// Exposes: start(), stop(), status(), onTranscript(callback) returning unsubscribe fn.
exposeSpeechWhisperToRenderer('speech');

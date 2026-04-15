'use strict';

const path = require('path');
const fs = require('fs');

const { app, BrowserWindow, Menu, dialog, shell, systemPreferences, session } = require('electron');
const childProcess = require('child_process');
const os = require('os');
const { defineIpcApi, defineIpcEvents } = require('@ozymandros/electron-message-bridge');
const { commandAction, buildMenuTemplate, loadMenuSpecFromFile } =
  require('@ozymandros/electron-message-bridge/menus');
const { registerSpeechWhisperMain } =
  require('@ozymandros/electron-message-bridge-plugin-speech-whisper');
const backendLifecycle = require('./backendLifecycle.cjs');
const pipeBroker = require('./pipeBroker.cjs');
const { registerWhisperPlugin } = require('./speechWhisperSetup.cjs');

/** Must match backendLifecycle default (override with GODOT_BLAZOR_URL). */
const defaultDevUrl = 'http://127.0.0.1:5044';

// ── Speech-to-text (Whisper.cpp via node-record-lpcm16) ───────────────────────
// Configure paths for Whisper CLI and model; adjust to your local setup.
// The plugin handles IPC registration under `stt:*` channels by default.
const { stt, options: whisperOptions } = registerWhisperPlugin(registerSpeechWhisperMain, {
  platform: process.platform,
  env: process.env,
  baseDir: __dirname,
});
const whisperBinPreferenceKey = 'app.whisper_bin_path';

if (stt?.manager) {
  const manager = stt.manager;
  const originalStart = manager.start.bind(manager);
  const originalStop = manager.stop.bind(manager);
  const originalGetStatus = manager.getStatus.bind(manager);
  const originalEnsureRecorder = typeof manager.ensureRecorder === 'function'
    ? manager.ensureRecorder.bind(manager)
    : null;
  let _prevObservedState = manager.getState?.() ?? null;
  let _stateValue = manager.state;
  let _whisperBinAvailability = {
    configured: false,
    exists: false,
    whisperBin: null,
    reason: 'Whisper path is not configured.',
  };

  if (originalEnsureRecorder) {
    manager.ensureRecorder = async (...args) => {
      const recorderFactory = await originalEnsureRecorder(...args);
      const explicitDevice = (process.env.SOX_AUDIO_DEVICE || process.env.AUDIODEV || '').trim();
      if (!explicitDevice || typeof recorderFactory !== 'function') {
        return recorderFactory;
      }
      return (options = {}) => recorderFactory({ ...options, device: explicitDevice });
    };
  }

  Object.defineProperty(manager, 'state', {
    configurable: true,
    enumerable: true,
    get() {
      return _stateValue;
    },
    set(next) {
      _stateValue = next;
    },
  });

  function _toErrorMessage(err) {
    if (err instanceof Error) return err.message || err.name || 'Unknown Error';
    if (typeof err === 'string') return err;
    try {
      return JSON.stringify(err);
    } catch {
      return String(err);
    }
  }

  async function _refreshWhisperBinFromPreference(origin) {
    manager.options = manager.options || {};
    const setAvailability = (configured, exists, whisperBin, reason) => {
      _whisperBinAvailability = { configured, exists, whisperBin, reason };
    };
    if (!backendLifecycle.isReady()) {
      manager.options.whisperBin = '';
      setAvailability(false, false, null, 'Backend is not ready yet.');
      return;
    }
    try {
      const response = await pipeBroker.invoke('Preference.Get/v1', { key: whisperBinPreferenceKey });
      const payload = response?.payloadJson ? JSON.parse(response.payloadJson) : null;
      const value = (typeof payload?.value === 'string' && payload.value.trim().length > 0)
        ? payload.value.trim()
        : (typeof payload?.Value === 'string' && payload.Value.trim().length > 0 ? payload.Value.trim() : null);
      let effectiveBin = value;
      if (process.platform === 'win32' && typeof effectiveBin === 'string' && effectiveBin.length > 0) {
        const normalized = effectiveBin.replace(/\//g, '\\').toLowerCase();
        if (normalized.endsWith('\\main.exe')) {
          const cliSibling = path.join(path.dirname(effectiveBin), 'whisper-cli.exe');
          if (safeExistsSync(cliSibling)) {
            effectiveBin = cliSibling;
          }
        }
      }
      const exists = !!effectiveBin && safeExistsSync(effectiveBin);
      manager.options.whisperBin = exists ? effectiveBin : '';
      if (!value) {
        setAvailability(false, false, null, 'Whisper path is not configured in Settings > General.');
      } else if (!exists) {
        setAvailability(true, false, effectiveBin, `Whisper binary was not found at: ${effectiveBin}`);
      } else {
        setAvailability(true, true, effectiveBin, null);
      }
      void origin;
    } catch (err) {
      manager.options.whisperBin = '';
      setAvailability(false, false, null, 'Failed to read Whisper path preference.');
      void origin;
      void err;
    }
  }

  manager.start = async (...args) => {
    try {
      await _refreshWhisperBinFromPreference('start');
      if (!_whisperBinAvailability.exists) {
        const reason = _whisperBinAvailability.reason ?? 'Whisper path is not available.';
        manager.lastError = reason;
        throw new Error(reason);
      }
      const result = await originalStart(...args);
      const session = manager.recordingSession ?? null;

      if (session?.fileStream && typeof session.fileStream.on === 'function') {
        session.fileStream.on('error', (err) => {
          const message = _toErrorMessage(err);
          manager.lastError = message;
          void message;
        });
        session.fileStream.on('finish', () => {});
        session.fileStream.on('close', () => {});
      }

      if (session?.recording) {
        const recording = session.recording;
        if (typeof recording.on === 'function') {
          recording.on('error', (err) => {
            const message = _toErrorMessage(err);
            manager.lastError = message;
            void message;
          });
        }
        if (typeof recording.stream === 'function') {
          try {
            const recStream = recording.stream();
            if (recStream && typeof recStream.on === 'function') {
              recStream.on('error', (err) => {
                const rawMessage = _toErrorMessage(err);
                const currentState = manager.getState?.() ?? null;
                const isBenignSoxCloseDuringProcessing = process.platform === 'win32'
                  && rawMessage.includes('sox has exited with error code null')
                  && (currentState === 'PROCESSING'
                    || (currentState === 'ERROR' && !!manager.whisperChild));
                if (isBenignSoxCloseDuringProcessing) {
                  return;
                }
                const message = rawMessage.includes('no default audio device configured')
                  ? `${rawMessage}\n\nSet a default microphone in Windows Sound settings and retry.`
                  : rawMessage;
                manager.lastError = message;
                void err;
              });
              recStream.on('close', () => {});
              recStream.on('end', () => {});
              recStream.on('unpipe', () => {});
            }
          } catch (err) {
            void err;
          }
        }
      }
      return result;
    } catch (err) {
      throw err;
    }
  };

  manager.stop = async (...args) => {
    try {
      await _refreshWhisperBinFromPreference('stop');
      const currentOutPath = manager.recordingSession?.outPath;
      if (typeof currentOutPath === 'string' && currentOutPath.length > 0 && safeExistsSync(currentOutPath)) {
        try {
          const backupOutPath = path.join(os.tmpdir(), `eiph-stt-safe-${Date.now()}-${Math.random().toString(16).slice(2)}.wav`);
          fs.copyFileSync(currentOutPath, backupOutPath);
          if (manager.recordingSession) {
            manager.recordingSession.outPath = backupOutPath;
          }
        } catch (copyErr) {
          void copyErr;
        }
      }
      const result = await originalStop(...args);
      manager.lastError = null;
      return result;
    } catch (err) {
      const rawError = _toErrorMessage(err);
      const normalizedError = (() => {
        const text = (rawError || '').toLowerCase();
        if (text.includes("no such option: -m")
          || text.includes('usage: whisper [options] command [args]')) {
          return [
            'The detected "whisper" command is not whisper.cpp CLI.',
            'Install whisper.cpp and set WHISPER_BIN to the whisper.cpp executable path.',
          ].join('\n');
        }
        if (text.includes('spawn') && text.includes('whisper') && text.includes('enoent')) {
          return [
            'Whisper executable was not found in PATH.',
            'Install whisper.cpp and/or set WHISPER_BIN to the whisper.cpp executable path.',
          ].join('\n');
        }
        return rawError;
      })();
      manager.lastError = normalizedError;
      throw new Error(normalizedError);
    }
  };

  manager.getStatus = async (...args) => {
    await _refreshWhisperBinFromPreference('getStatus');
    const status = await originalGetStatus(...args);
    const currentState = manager.getState?.() ?? null;
    const effectiveStatus = (currentState === 'ERROR' && !status?.error && manager.lastError)
      ? { ...status, error: manager.lastError }
      : status;
    const normalizedStatus = (() => {
      if (!_whisperBinAvailability.exists) {
        return {
          ...effectiveStatus,
          canRecord: false,
          hasBinary: false,
          error: _whisperBinAvailability.reason ?? 'Whisper path is not available.',
        };
      }
      const errorText = typeof effectiveStatus?.error === 'string'
        ? effectiveStatus.error.toLowerCase()
        : '';
      const looksLikeSoxInputDeviceFailure = errorText.includes('no default audio device configured')
        || (process.platform === 'win32' && errorText.includes('sox has exited with error code 1'));
      if (looksLikeSoxInputDeviceFailure) {
        const actionableError = errorText.includes('no default audio device configured')
          ? effectiveStatus.error
          : `${effectiveStatus?.error ?? 'Microphone capture failed.'}\n\nSet a default microphone in Windows Sound settings and retry.`;
        return {
          ...effectiveStatus,
          canRecord: false,
          error: actionableError,
        };
      }
      return effectiveStatus;
    })();
    void currentState;
    void _prevObservedState;
    _prevObservedState = currentState;
    return normalizedStatus;
  };
}

const _origSpawn = childProcess.spawn;
childProcess.spawn = function patchedSpawn(cmd, args, options) {
  const cmdText = typeof cmd === 'string' ? cmd.toLowerCase() : '';
  const argList = Array.isArray(args) ? args.map((a) => String(a).toLowerCase()) : [];
  const looksLikeWhisperSpawn = cmdText.includes('whisper')
    || cmdText.endsWith('\\main.exe')
    || cmdText.endsWith('/main.exe')
    || argList.includes('-m') && argList.includes('-f');
  if (looksLikeWhisperSpawn) {
    const child = _origSpawn.apply(this, arguments);
    return child;
  }

  if (process.platform === 'win32' && typeof cmd === 'string' && cmd.toLowerCase().endsWith('.cmd')) {
    const nextOptions = { ...(options || {}), shell: true };
    return _origSpawn.call(this, cmd, args, nextOptions);
  }

  if (typeof cmd === 'string' && cmd.toLowerCase().includes('sox')) {
    let nextArgs = Array.isArray(args) ? [...args] : [];
    if (process.platform === 'win32' && nextArgs.includes('--default-device')) {
      nextArgs = [
        '-t',
        'waveaudio',
        'default',
        ...nextArgs.filter((a) => a !== '--default-device'),
      ];
    }
    return _origSpawn.call(this, cmd, nextArgs, options);
  }
  return _origSpawn.apply(this, arguments);
};

const whisperBin = whisperOptions.whisperBin;
const modelPath = whisperOptions.modelPath;
console.log('[Electron] Using whisperBin:', whisperBin);

if (fs.existsSync(modelPath)) {
  console.log('[Electron] Found Whisper model at:', modelPath);
} else {
  console.error('[Electron] Whisper model not found at:', modelPath);
}

if (whisperBin) {
  if (fs.existsSync(whisperBin)) {
    console.log('[Electron] Found Whisper binary at:', whisperBin);
  } else {
    console.error('[Electron] Whisper binary not found at:', whisperBin);
  }
} else {
  console.log('[Electron] Using plugin default whisper binary resolution.');
}

if (stt?.options?.modelPath) {
  if (fs.existsSync(stt.options.modelPath)) {
    console.log('[Electron] Found Whisper model at:', stt.options.modelPath);
  } else {
    console.error('[Electron] Whisper model not found at:', stt.options.modelPath);
  }

  if (stt.options.whisperBin) {
    if (fs.existsSync(stt.options.whisperBin)) {
      console.log('[Electron] Found Whisper binary at:', stt.options.whisperBin);
    } else {
      console.error('[Electron] Whisper binary not found at:', stt.options.whisperBin);
    }
  } else {
    console.log('[Electron] STT plugin is using its internal whisper binary default.');
  }
}
else {
  console.error('[Electron] STT manager options not properly set:', stt?.options);
}

console.log(stt)

/**
 * Returns the Blazor origin from the GODOT_BLAZOR_URL environment variable or the default dev URL.
 * @returns {string}  Blazor origin (e.g., `https://localhost:5044`)
 */
function getBlazorOrigin() {
  try {
    return new URL(process.env.GODOT_BLAZOR_URL || defaultDevUrl).origin;
  } catch {
    return new URL(defaultDevUrl).origin;
  }
}

/**
 * Installs the Electron renderer CSP.  The policy is applied to all sessions and allows the required features for
 * Blazor WebAssembly (including `wasm-unsafe-eval` for the mono runtime) while maintaining a strong default policy.
 * The `connect-src` directive allows WebSocket connections to the Blazor origin for hot reload and IPC, while
 * restricting other external connections.  Adjust the policy as needed if your app requires additional features or
 */
function installContentSecurityPolicy() {
  const blazorOrigin = getBlazorOrigin();

  // Desktop renderer CSP: no unsafe-eval, but allow WASM and required inline
  // blocks used by import maps and framework bootstrapping.
  const csp = [
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-ancestors 'none'",
    "script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    `connect-src 'self' ${blazorOrigin} http://127.0.0.1:7650 ws: wss:`,
    "media-src 'self' data: blob:",
  ].join('; ');

  app.on('session-created', (session) => {
    session.webRequest.onHeadersReceived((details, callback) => {
      const currentOrigin = (() => {
        try { return new URL(details.url).origin; } catch { return ''; }
      })();

      if (currentOrigin !== blazorOrigin) {
        callback({ responseHeaders: details.responseHeaders });
        return;
      }

      callback({
        responseHeaders: {
          ...details.responseHeaders,
          'Content-Security-Policy': [csp],
        },
      });
    });
  });
}

async function ensureMicrophonePermissionOnWindows() {
  if (process.platform !== 'win32') {
    return;
  }
  try {
    const micAccess = typeof systemPreferences?.getMediaAccessStatus === 'function'
      ? systemPreferences.getMediaAccessStatus('microphone')
      : 'unavailable';
    if (micAccess !== 'granted') {
      const opened = await shell.openExternal('ms-settings:privacy-microphone');
      void opened;
    }
  } catch (err) {
    void err;
  }
}

function installMediaPermissionHandlers() {
  try {
    session.defaultSession.setPermissionCheckHandler((_webContents, permission, _requestingOrigin) => {
      const allowed = permission === 'media';
      return allowed;
    });

    session.defaultSession.setPermissionRequestHandler((_webContents, permission, callback, details) => {
      const allowed = permission === 'media';
      void details;
      callback(allowed);
    });
  } catch (err) {
    void err;
  }
}

/**
 * Safely checks if a path exists.  Returns `false` for empty strings or `null`.
 * @param {string} p
 * @returns {boolean}
 */
function safeExistsSync(p) {
  return typeof p === 'string' && p.length > 0 && fs.existsSync(p);
}

// ── IPC API (renderer → main, request / response) ────────────────────────────
//
// `defineIpcApi` registers one `ipcMain.handle` per key and returns a typed
// handle carrying the channel names.  The preload's `exposeApiToRenderer`
// reads those channel names to wire up `ipcRenderer.invoke` proxies.

const ipcApi = defineIpcApi({
  /**
   * Opens the native open-folder dialog and resolves with the selected path,
   * or `null` if the user cancelled.
   * @param {{ properties?: string[] }} [options]
   * @returns {Promise<string|null>}
   */
  showOpenDialog: async (options) => {
    const { canceled, filePaths } = await dialog.showOpenDialog({
      properties: options?.properties ?? ['openDirectory'],
    });
    return canceled ? null : (filePaths[0] ?? null);
  },

  /**
   * Forwards a versioned IPC command to the local .NET backend via the named
   * pipe and resolves with the raw response envelope.
   *
   * @param {string}      command      Versioned command name, e.g. `Config.GetAll/v1`.
   * @param {string|null} payloadJson  JSON-stringified payload, or null.
   * @returns {Promise<{success: boolean, payloadJson: string|null, errorCode: string|null, errorMessage: string|null}>}
   */
  invokeCommand: async (command, payloadJson) => {
    if (!backendLifecycle.isReady()) {
      return {
        success: false,
        payloadJson: null,
        errorCode: 'BACKEND_NOT_READY',
        errorMessage: 'The local backend is not ready yet. Please wait and retry.',
      };
    }

    let payload = null;
    if (payloadJson && typeof payloadJson === 'string') {
      try {
        payload = JSON.parse(payloadJson);
      } catch {
        return {
          success: false,
          payloadJson: null,
          errorCode: 'INVALID_PAYLOAD',
          errorMessage: 'payloadJson is not valid JSON.',
        };
      }
    }

    try {
      return await pipeBroker.invoke(command, payload);
    } catch (err) {
      return {
        success: false,
        payloadJson: null,
        errorCode: 'BROKER_ERROR',
        errorMessage: err.message,
      };
    }
  },
});

// ── IPC Events (main → renderer, push) ───────────────────────────────────────
//
// `defineIpcEvents` stores channel names and exposes a type-safe `emit` method
// (`webContents.send` under the hood).  The preload's `exposeEventsToRenderer`
// reads those channel names to wire up `ipcRenderer.on` subscriptions that
// return cleanup (unsubscribe) callbacks.

const ipcEvents = defineIpcEvents({
  /** File-menu folder selection forwarded to the active renderer window. */
  folderSelected: (_path) => { },
  /** Backend pipe became ready (initial start or supervised restart). */
  backendReady: () => { },
  /** Backend process crashed; payload: `{ code: number|null, signal: string|null }`. */
  backendCrashed: (_detail) => { },
  /** Backend exceeded max restart attempts; no further supervision. */
  backendFailed: () => { },
});

// ── Window factory ────────────────────────────────────────────────────────────

/**
 * Creates a new Electron window.  The window is configured with the following settings:
 *
 *   • Width: 1280px
 *   • Height: 800px
 *   • WebPreferences:
 *     • preload: path.join(__dirname, 'preload.cjs')
 *     • contextIsolation: true (to isolate renderer context and enhance security)
 *     • sandbox: false (to allow loading of local files)
 *     • nodeIntegration: false (to prevent loading of Node.js modules)
 *     • enableRemoteModule: false (to prevent loading of Electron modules)
 *     • webSecurity: false (to disable CSP)
 *
 * @returns {BrowserWindow}
 */
function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      // Preload uses CommonJS `require(...)` (electron-message-bridge + local modules).
      // With sandbox enabled, preload require surface is restricted and bridge injection can fail.
      sandbox: false,
    },
  });

  const url = process.env.GODOT_BLAZOR_URL || defaultDevUrl;
  win.loadURL(url).catch((err) => {
    console.error(
      'Failed to load Blazor app. Start Kestrel first: dotnet run --project GodotGenerator.Blazor',
      err,
    );
  });

  return win;
}

// ── App menu ──────────────────────────────────────────────────────────────────
//
// The cross-platform menu structure lives in menu.json as `DeclarativeMenuItem[]`.
// Each clickable item carries an `actionId` that is resolved here in the action
// registry using typed descriptors from `electron-message-bridge/menus`:
//
//   commandAction(fn) — local async logic, main-process only
//   serviceAction(fn) — shared service function also called by IPC handlers
//   emitAction(fn)    — zero-arg closure that fires an ipcEvents.emit(...)
//
// Platform-specific items that cannot be expressed in a static JSON (macOS app
// menu needing `app.name`, macOS Speech submenu, Windows-only Window menu) are
// assembled inline and merged around the JSON-derived template.

/**
 * Loads `menu.json`, builds the typed action registry, and sets the application
 * menu.  Must be awaited inside `app.whenReady()`.
 */
async function buildAndSetMenuAsync() {
  const isMac = process.platform === 'darwin';

  // ── Action registry ─────────────────────────────────────────────────────────
  // Maps each actionId declared in menu.json to a typed ActionDescriptor.
  // Errors thrown by handlers are caught and logged by the bridge resolver.

  const actions = {
    /**
     * File › Open Project Folder…
     * Shows a native open-directory dialog; emits `folderSelected` to the
     * focused renderer so the project-header component auto-populates the path.
     */
    'file.openProjectFolder': commandAction(async () => {
      const w = BrowserWindow.getFocusedWindow();
      if (!w) return;
      const { canceled, filePaths } = await dialog.showOpenDialog(w, {
        properties: ['openDirectory'],
        title: 'Select Project Folder',
        buttonLabel: 'Open Folder',
      });
      if (!canceled && filePaths[0]) {
        ipcEvents.emit(w, 'folderSelected', filePaths[0]);
      }
    }),

    /**
     * Tools › Open Developer Tools
     * Toggles the Chromium DevTools panel for the focused window.
     */
    'tools.devtools': commandAction(() => {
      const w = BrowserWindow.getFocusedWindow();
      if (w) w.webContents.toggleDevTools();
    }),

    /**
     * Tools › Open Blazor in Browser
     * Opens the Kestrel dev URL in the system default browser.
     */
    'tools.openInBrowser': commandAction(async () => {
      await shell.openExternal(process.env.GODOT_BLAZOR_URL || defaultDevUrl);
    }),

    /**
     * Help › Repository
     * Opens the project GitHub page in the system browser.
     */
    'help.repository': commandAction(async () => {
      await shell.openExternal('https://github.com/Ozymandros/Godot-Generator');
    }),

    /**
     * Help › Development Guide
     * Opens docs/DEVELOPMENT.md with the system viewer, or shows an info
     * dialog if the file does not exist (e.g. in production bundles).
     */
    'help.devGuide': commandAction(async () => {
      const docsPath = path.join(__dirname, '..', 'docs', 'DEVELOPMENT.md');
      if (safeExistsSync(docsPath)) {
        await shell.openPath(docsPath);
      } else {
        await dialog.showMessageBox({
          type: 'info',
          title: 'Documentation',
          message: 'Development guide not found.',
          detail: 'Expected docs/DEVELOPMENT.md in the repository root.',
        });
      }
    }),

    /**
     * Help › About Godot Generator
     * Shows a native about dialog with the app version.
     */
    'help.about': commandAction(async () => {
      await dialog.showMessageBox({
        type: 'info',
        title: 'About Godot Generator',
        message: 'Godot Generator',
        detail: `Version: ${app.getVersion()}\n\nAI-powered generator for Godot projects.`,
      });
    }),
  };

  // ── Load declarative spec and build cross-platform template ─────────────────

  const spec = await loadMenuSpecFromFile(path.join(__dirname, 'menu.json'));
  const crossPlatform = buildMenuTemplate(spec.items, { actions });

  // macOS Speech submenu: append to the Edit menu (not in JSON — macOS-only).
  if (isMac) {
    const editMenu = crossPlatform.find((m) => m.label === 'Edit');
    if (editMenu?.submenu && Array.isArray(editMenu.submenu)) {
      editMenu.submenu.push(
        { type: 'separator' },
        { label: 'Speech', submenu: [{ role: 'startSpeaking' }, { role: 'stopSpeaking' }] },
      );
    }
  }

  // ── Assemble final platform-aware template ───────────────────────────────────

  /**
   * Assembles the final platform-aware menu template.  The template is assembled from the following sources:
   *
   *   • The declarative JSON spec (menu.json) is loaded and parsed into a typed action registry.
   *   • The cross-platform menu structure lives in menu.json as `DeclarativeMenuItem[]`.
   *   • Each clickable item carries an `actionId` that is resolved here in the action registry using typed
   *     descriptors from `electron-message-bridge/menus`:
   *
   *     commandAction(fn) — local async logic, main-process only
   *     serviceAction(fn) — shared service function also called by IPC handlers
   *     emitAction(fn)    — zero-arg closure that fires an ipcEvents.emit(...)
   *
   *   • Platform-specific items that cannot be expressed in a static JSON (macOS app menu needing `app.name`,
   *     macOS Speech submenu, Windows-only Window menu) are assembled inline and merged around the JSON-derived
   *     template.
   *
   * @param {object} actions
   * @returns {MenuItemConstructorOptions[]}
   */
  const template = [
    // macOS: prepend the app-name menu (requires runtime `app.name`, not in JSON).
    ...(isMac ? [{
      label: app.name,
      submenu: [
        { role: 'about' },
        { type: 'separator' },
        { role: 'services' },
        { type: 'separator' },
        { role: 'hide' },
        { role: 'hideOthers' },
        { role: 'unhide' },
        { type: 'separator' },
        { role: 'quit' },
      ],
    }] : []),

    // Cross-platform items resolved from menu.json.
    ...crossPlatform,

    // Windows/Linux: append a Window menu (macOS has this built-in via roles).
    ...(!isMac ? [{
      label: 'Window',
      submenu: [
        { role: 'minimize' },
        { role: 'close' },
      ],
    }] : []),
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

// ── Context menu (dynamic — stays inline, not suitable for a static JSON spec) ──

/**
 *  Enables a right-click context menu in the given window.  The menu adapts to the context:
 *
 *   • If right-clicking on an editable element (input, textarea, contenteditable), show cut/copy/paste/selectAll.
 *   • If right-clicking on a selection, show undo/redo.
 *   • If right-clicking anywhere else, show close.
 *
 * @param {*} win
 * @returns
 */
function enableContextMenu(win) {
  if (!win) return;

  win.webContents.on('context-menu', (_event, params) => {
    const { selectionText, isEditable } = params;

    if (isEditable) {
      Menu.buildFromTemplate([
        { role: 'undo' },
        { role: 'redo' },
        { type: 'separator' },
        { role: 'cut' },
        { role: 'copy' },
        { role: 'paste' },
        { type: 'separator' },
        { role: 'selectAll' },
      ]).popup({ window: win });
      return;
    }

    if (selectionText) {
      Menu.buildFromTemplate([
        { role: 'copy' },
        { type: 'separator' },
        { role: 'selectAll' },
      ]).popup({ window: win });
    }
  });
}

// ── App lifecycle ─────────────────────────────────────────────────────────────

app.whenReady().then(async () => {
  installContentSecurityPolicy();
  installMediaPermissionHandlers();
  await ensureMicrophonePermissionOnWindows();

  // Build and apply the application menu from the declarative JSON spec.
  await buildAndSetMenuAsync();

  // Start the .NET backend; create the window immediately so the user sees the
  // loading UI.  The Blazor app becomes fully interactive once the pipe is ready.
  try {
    await backendLifecycle.start();
  } catch (err) {
    console.error('[main] Backend failed to start:', err.message);
    // Window still opens; Blazor will show a degraded-state banner.
  }

  const mainWindow = createWindow();
  enableContextMenu(mainWindow);

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });

  // Notify all renderer windows when the backend becomes (re-)ready.
  backendLifecycle.on('ready', () => {
    if (stt?.manager) {
      _refreshWhisperBinFromPreference('backend-ready').catch(() => {});
    }
    BrowserWindow.getAllWindows().forEach((w) => ipcEvents.emit(w, 'backendReady'));
  });

  // Surface backend crash events so the renderer can show a degraded-state banner.
  backendLifecycle.on('crashed', (detail) => {
    BrowserWindow.getAllWindows().forEach((w) => ipcEvents.emit(w, 'backendCrashed', detail));
  });

  backendLifecycle.on('failed', () => {
    BrowserWindow.getAllWindows().forEach((w) => ipcEvents.emit(w, 'backendFailed'));
  });
});

app.on('before-quit', async (event) => {
  event.preventDefault();
  ipcApi.dispose();
  stt.dispose(); // Clean up STT (stop recorder, Whisper subprocess, temp files)
  await backendLifecycle.stop();
  app.exit(0);
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});


async function fileExists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

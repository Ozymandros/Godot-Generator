'use strict';

const { app, BrowserWindow, Menu, dialog, shell } = require('electron');
const { defineIpcApi, defineIpcEvents }            = require('electron-message-bridge');
const { commandAction, buildMenuTemplate, loadMenuSpecFromFile } =
  require('electron-message-bridge/menus');
const fs   = require('fs');
const path = require('path');
const backendLifecycle = require('./backendLifecycle.cjs');
const pipeBroker       = require('./pipeBroker.cjs');

/** Must match backendLifecycle default (override with GODOT_BLAZOR_URL). */
const defaultDevUrl = 'http://127.0.0.1:5044';

function getBlazorOrigin() {
  try {
    return new URL(process.env.GODOT_BLAZOR_URL || defaultDevUrl).origin;
  } catch {
    return new URL(defaultDevUrl).origin;
  }
}

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
    `connect-src 'self' ${blazorOrigin} ws: wss:`,
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
        success:      false,
        payloadJson:  null,
        errorCode:    'BACKEND_NOT_READY',
        errorMessage: 'The local backend is not ready yet. Please wait and retry.',
      };
    }

    let payload = null;
    if (payloadJson && typeof payloadJson === 'string') {
      try {
        payload = JSON.parse(payloadJson);
      } catch {
        return {
          success:      false,
          payloadJson:  null,
          errorCode:    'INVALID_PAYLOAD',
          errorMessage: 'payloadJson is not valid JSON.',
        };
      }
    }

    try {
      return await pipeBroker.invoke(command, payload);
    } catch (err) {
      return {
        success:      false,
        payloadJson:  null,
        errorCode:    'BROKER_ERROR',
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
  folderSelected: (_path) => {},
  /** Backend pipe became ready (initial start or supervised restart). */
  backendReady:   () => {},
  /** Backend process crashed; payload: `{ code: number|null, signal: string|null }`. */
  backendCrashed: (_detail) => {},
  /** Backend exceeded max restart attempts; no further supervision. */
  backendFailed:  () => {},
});

// ── Window factory ────────────────────────────────────────────────────────────

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      preload:          path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      // Preload uses CommonJS `require(...)` (electron-message-bridge + local modules).
      // With sandbox enabled, preload require surface is restricted and bridge injection can fail.
      sandbox:          false,
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
        title:       'Select Project Folder',
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
      await shell.openExternal('https://github.com/Ozymandros/Godot-Generator-Avalonia');
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
          type:    'info',
          title:   'Documentation',
          message: 'Development guide not found.',
          detail:  'Expected docs/DEVELOPMENT.md in the repository root.',
        });
      }
    }),

    /**
     * Help › About Godot Generator
     * Shows a native about dialog with the app version.
     */
    'help.about': commandAction(async () => {
      await dialog.showMessageBox({
        type:    'info',
        title:   'About Godot Generator',
        message: 'Godot Generator',
        detail:  `Version: ${app.getVersion()}\n\nAI-powered generator for Godot projects.`,
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
  await backendLifecycle.stop();
  app.exit(0);
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

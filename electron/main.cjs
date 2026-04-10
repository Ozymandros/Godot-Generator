'use strict';

const { app, BrowserWindow, Menu, dialog, shell } = require('electron');
const { defineIpcApi, defineIpcEvents }            = require('electron-message-bridge');
const fs               = require('fs');
const path             = require('path');
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
        try {
          return new URL(details.url).origin;
        } catch {
          return '';
        }
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
      sandbox:          true,
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

function buildMenu() {
  const isMac = process.platform === 'darwin';

  /** Opens the native folder picker and pushes the result to the given window. */
  async function pickFolderAndNotify(win) {
    const w = win || BrowserWindow.getFocusedWindow();
    if (!w) return;

    const { canceled, filePaths } = await dialog.showOpenDialog(w, {
      properties: ['openDirectory'],
      title:       'Select Project Folder',
      buttonLabel: 'Open Folder',
    });
    if (!canceled && filePaths[0]) {
      ipcEvents.emit(w, 'folderSelected', filePaths[0]);
    }
  }

  return Menu.buildFromTemplate([
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
    {
      label: 'File',
      submenu: [
        {
          label:       'Open Project Folder…',
          accelerator: 'CmdOrCtrl+O',
          click: (_item, focusedWindow) => pickFolderAndNotify(focusedWindow),
        },
        { type: 'separator' },
        isMac ? { role: 'close' } : { role: 'quit' },
      ],
    },
    {
      label: 'Edit',
      submenu: [
        { role: 'undo' },
        { role: 'redo' },
        { type: 'separator' },
        { role: 'cut' },
        { role: 'copy' },
        { role: 'paste' },
        ...(isMac
          ? [
            { role: 'pasteAndMatchStyle' },
            { role: 'delete' },
            { role: 'selectAll' },
            { type: 'separator' },
            { label: 'Speech', submenu: [{ role: 'startSpeaking' }, { role: 'stopSpeaking' }] },
          ]
          : [
            { role: 'delete' },
            { type: 'separator' },
            { role: 'selectAll' },
          ]),
      ],
    },
    {
      label: 'Tools',
      submenu: [
        {
          label:       'Open Developer Tools',
          accelerator: 'F12',
          click: (_item, focusedWindow) => {
            const w = focusedWindow || BrowserWindow.getFocusedWindow();
            if (w) w.webContents.toggleDevTools();
          },
        },
        { type: 'separator' },
        {
          label: 'Open Blazor in Browser',
          click: async () => shell.openExternal(process.env.GODOT_BLAZOR_URL || defaultDevUrl),
        },
      ],
    },
    {
      label: 'View',
      submenu: [
        { role: 'reload' },
        { role: 'forceReload' },
        { role: 'toggleDevTools' },
        { type: 'separator' },
        { role: 'resetZoom' },
        { role: 'zoomIn' },
        { role: 'zoomOut' },
        { type: 'separator' },
        { role: 'togglefullscreen' },
      ],
    },
    ...(!isMac ? [{
      label: 'Window',
      submenu: [
        { role: 'minimize' },
        { role: 'close' },
      ],
    }] : []),
    {
      role: 'help',
      submenu: [
        {
          label: 'Repository',
          click: async () => shell.openExternal('https://github.com/Ozymandros/Godot-Generator-Avalonia'),
        },
        {
          label: 'Development Guide',
          click: async () => {
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
          },
        },
        { type: 'separator' },
        {
          label: 'About Godot Generator',
          click: async () => dialog.showMessageBox({
            type:    'info',
            title:   'About Godot Generator',
            message: 'Godot Generator',
            detail:  `Version: ${app.getVersion()}\n\nAI-powered generator for Godot projects.`,
          }),
        },
      ],
    },
  ]);
}

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
  Menu.setApplicationMenu(buildMenu());

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

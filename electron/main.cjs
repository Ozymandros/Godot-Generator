'use strict';

const { app, BrowserWindow, Menu, ipcMain, dialog, shell } = require('electron');
const fs               = require('fs');
const path             = require('path');
const backendLifecycle = require('./backendLifecycle.cjs');
const pipeBroker       = require('./pipeBroker.cjs');

/** Must match backendLifecycle default (override with GODOT_BLAZOR_URL). */
const defaultDevUrl = 'http://127.0.0.1:5044';

function safeExistsSync(p) {
  return typeof p === 'string' && p.length > 0 && fs.existsSync(p);
}

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
          label: 'Open Project Folder...',
          accelerator: 'CmdOrCtrl+O',
          click: async (_item, focusedWindow) => {
            const w = focusedWindow || BrowserWindow.getFocusedWindow();
            const { canceled, filePaths } = await dialog.showOpenDialog(w ?? undefined, {
              properties: ['openDirectory'],
              title: 'Select Project Folder',
              buttonLabel: 'Open Folder',
            });
            if (!canceled && filePaths[0] && w) {
              w.webContents.send('godot:folder-selected', filePaths[0]);
            }
          },
        },
        {
          label: 'Open folder…',
          click: async (_item, focusedWindow) => {
            const w = focusedWindow || BrowserWindow.getFocusedWindow();
            const { canceled, filePaths } = await dialog.showOpenDialog(w ?? undefined, {
              properties: ['openDirectory'],
            });
            if (!canceled && filePaths[0] && w) {
              w.webContents.send('godot:folder-selected', filePaths[0]);
            }
          },
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
          label: 'Open Developer Tools',
          accelerator: 'F12',
          click: (_item, focusedWindow) => {
            const w = focusedWindow || BrowserWindow.getFocusedWindow();
            if (w) {
              w.webContents.toggleDevTools();
            }
          },
        },
        { type: 'separator' },
        {
          label: 'Open Blazor in Browser',
          click: async () => {
            const url = process.env.GODOT_BLAZOR_URL || defaultDevUrl;
            await shell.openExternal(url);
          },
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
          click: async () => {
            await shell.openExternal('https://github.com/Ozymandros/Godot-Generator-Avalonia');
          },
        },
        {
          label: 'Development Guide',
          click: async () => {
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
          },
        },
        { type: 'separator' },
        {
          label: 'About Godot Generator',
          click: async () => {
            await dialog.showMessageBox({
              type: 'info',
              title: 'About Godot Generator',
              message: 'Godot Generator',
              detail: `Version: ${app.getVersion()}\n\nAI-powered generator for Godot projects.`,
            });
          },
        },
      ],
    },
  ]);
}

function enableContextMenu(win) {
  if (!win) {
    return;
  }

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

// ── IPC handlers ──────────────────────────────────────────────────────────────

/** Dialog: open-folder (existing). */
ipcMain.handle('godot:show-open-dialog', async (_event, options) => {
  const { canceled, filePaths } = await dialog.showOpenDialog({
    properties: options?.properties ?? ['openDirectory'],
  });
  return canceled ? null : filePaths[0] ?? null;
});

/**
 * IPC command bus: renderer sends a versioned command name + JSON payload,
 * main forwards it to the .NET backend via the named pipe, returns the response.
 *
 * Exposed to the renderer as `window.godotElectron.invokeCommand(command, payload)`.
 */
ipcMain.handle('godot:invoke-command', async (_event, command, payloadJson) => {
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
});

// ── App lifecycle ─────────────────────────────────────────────────────────────

app.whenReady().then(async () => {
  Menu.setApplicationMenu(buildMenu());

  // Start the .NET backend; create the window immediately so the user sees the
  // loading UI, then the Blazor app becomes fully interactive once the pipe is ready.
  try {
    await backendLifecycle.start();
  } catch (err) {
    console.error('[main] Backend failed to start:', err.message);
    // The window still opens; Blazor will show a degraded-state banner.
  }

  const mainWindow = createWindow();
  enableContextMenu(mainWindow);

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });

  // Notify all renderer windows when the backend becomes (re-)ready after a restart.
  backendLifecycle.on('ready', () => {
    BrowserWindow.getAllWindows().forEach((w) =>
      w.webContents.send('godot:backend-ready'));
  });

  // Surface backend crash events so the renderer can show a degraded-state message.
  backendLifecycle.on('crashed', ({ code, signal }) => {
    BrowserWindow.getAllWindows().forEach((w) =>
      w.webContents.send('godot:backend-crashed', { code, signal }));
  });

  backendLifecycle.on('failed', () => {
    BrowserWindow.getAllWindows().forEach((w) =>
      w.webContents.send('godot:backend-failed'));
  });
});

app.on('before-quit', async (event) => {
  event.preventDefault();
  await backendLifecycle.stop();
  app.exit(0);
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

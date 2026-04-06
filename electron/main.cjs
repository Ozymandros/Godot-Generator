'use strict';

const { app, BrowserWindow, Menu, ipcMain, dialog } = require('electron');
const path = require('path');

/** Match Properties/launchSettings.json profile `http` (override with GODOT_BLAZOR_URL). */
const defaultDevUrl = 'http://localhost:5044';

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      sandbox: true,
    },
  });

  const url = process.env.GODOT_BLAZOR_URL || defaultDevUrl;
  win.loadURL(url).catch((err) => {
    console.error('Failed to load Blazor app. Start Kestrel first: dotnet run --project GodotGenerator.Blazor', err);
  });
}

function buildMenu() {
  return Menu.buildFromTemplate([
    {
      label: 'File',
      submenu: [
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
        { role: 'quit' },
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
      ],
    },
  ]);
}

app.whenReady().then(() => {
  Menu.setApplicationMenu(buildMenu());
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

ipcMain.handle('godot:show-open-dialog', async (_event, options) => {
  const { canceled, filePaths } = await dialog.showOpenDialog({
    properties: options?.properties ?? ['openDirectory'],
  });
  return canceled ? null : filePaths[0] ?? null;
});

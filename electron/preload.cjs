'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('godotElectron', {
  platform: process.platform,
  showOpenDialog: (options) => ipcRenderer.invoke('godot:show-open-dialog', options),
  onFolderSelected: (callback) => {
    ipcRenderer.on('godot:folder-selected', (_e, folderPath) => {
      callback(folderPath);
    });
  },
});

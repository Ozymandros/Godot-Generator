// Thin helpers for Blazor JS interop when running inside Electron (see electron/preload.cjs).
window.godotElectronInterop = {
  isElectron: function () {
    return typeof window.godotElectron !== 'undefined' && window.godotElectron !== null;
  },
  pickFolder: async function () {
    if (!window.godotElectronInterop.isElectron()) {
      return null;
    }
    const path = await window.godotElectron.showOpenDialog({
      properties: ['openDirectory'],
    });
    return path ?? null;
  },
};

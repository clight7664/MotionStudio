'use strict';

const { contextBridge } = require('electron');

/**
 * Hardened Electron Preload Bridge (ADR-0001 / NFR-006)
 * contextIsolation: true, nodeIntegration: false
 * Exposes zero raw Node.js or filesystem primitives to the renderer.
 */
contextBridge.exposeInMainWorld('motionStudioDesktop', Object.freeze({
  hostMode: 'electron-loopback',
  platform: process.platform,
  versions: Object.freeze({
    electron: process.versions.electron,
    chrome: process.versions.chrome,
  }),
}));

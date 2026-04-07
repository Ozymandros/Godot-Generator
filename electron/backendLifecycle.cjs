'use strict';

/**
 * @file backendLifecycle.cjs
 * Manages the local .NET backend process lifetime:
 *   - spawn with GODOT_DESKTOP_IPC=1
 *   - verify readiness via named-pipe handshake
 *   - supervise with automatic restart on unexpected exit
 *   - graceful shutdown on app-quit
 *
 * Usage:
 *   const lifecycle = require('./backendLifecycle.cjs');
 *   await lifecycle.start();          // called from app.whenReady()
 *   await lifecycle.stop();           // called from app.before-quit
 *   lifecycle.isReady()               // true once handshake succeeds
 */

const { spawn }    = require('child_process');
const net          = require('net');
const path         = require('path');
const EventEmitter = require('events');

// ── Configuration ────────────────────────────────────────────────────────────

const PIPE_NAME       = '\\\\.\\pipe\\godot-generator-ipc';
const READY_TIMEOUT   = 60_000;   // ms to wait for pipe handshake after spawn
const RESTART_DELAY   = 2_000;    // ms between supervised restart attempts
const MAX_RESTARTS    = 5;        // stop supervising after this many rapid restarts
const RAPID_WINDOW    = 10_000;   // ms window for counting rapid restarts
const DEFAULT_UI_URL  = 'http://127.0.0.1:5044';

// ── Resolve backend executable ───────────────────────────────────────────────

/**
 * Returns the path to the backend executable.
 * In production the bundled exe sits next to the Electron resources; in dev we
 * fall back to `dotnet run` inside the Blazor project directory.
 */
function resolveBackendCommand() {
  const exeName = process.platform === 'win32'
    ? 'GodotGenerator.Blazor.exe'
    : 'GodotGenerator.Blazor';

  // Packaged path (electron-builder copies to resources/backend/)
  const bundled = path.join(process.resourcesPath ?? '', 'backend', exeName);
  const fs = require('fs');
  if (fs.existsSync(bundled)) {
    return { cmd: bundled, args: [], isBundled: true };
  }

  // Development fallback: dotnet run
  const projectDir = path.resolve(__dirname, '..', 'GodotGenerator.Blazor', 'GodotGenerator.Blazor');
  const urls = process.env.GODOT_BLAZOR_URL || DEFAULT_UI_URL;
  return { cmd: 'dotnet', args: ['run', '--project', projectDir, '--no-launch-profile', '--urls', urls], isBundled: false };
}

// ── Pipe readiness probe ─────────────────────────────────────────────────────

/**
 * Resolves when a connection to the IPC pipe succeeds (backend is ready).
 * Retries every 300 ms until `timeoutMs` has elapsed.
 * @param {number} timeoutMs
 * @returns {Promise<void>}
 */
function waitForPipeReady(timeoutMs) {
  return new Promise((resolve, reject) => {
    const deadline = Date.now() + timeoutMs;

    function attempt() {
      if (Date.now() > deadline) {
        return reject(new Error(`Backend pipe not ready within ${timeoutMs}ms.`));
      }
      const sock = net.createConnection(PIPE_NAME);
      sock.on('connect', () => { sock.destroy(); resolve(); });
      sock.on('error', () => { sock.destroy(); setTimeout(attempt, 300); });
    }

    attempt();
  });
}

// ── BackendLifecycle ─────────────────────────────────────────────────────────

class BackendLifecycle extends EventEmitter {
  constructor() {
    super();
    /** @type {import('child_process').ChildProcess | null} */
    this._process     = null;
    this._ready       = false;
    this._stopping    = false;
    this._restartCount = 0;
    this._lastRestartAt = 0;
  }

  /** Whether the backend pipe has completed its readiness handshake. */
  isReady() { return this._ready; }

  /**
   * Spawns the backend process and waits for the pipe handshake.
   * Resolves when the backend is ready to accept IPC commands.
   */
  async start() {
    this._stopping = false;
    await this._spawn();
    await waitForPipeReady(READY_TIMEOUT);
    this._ready = true;
    console.log('[BackendLifecycle] Backend is ready on pipe:', PIPE_NAME);
    this.emit('ready');
  }

  /**
   * Gracefully shuts down the backend process and stops supervision.
   */
  async stop() {
    this._stopping = true;
    this._ready    = false;
    if (this._process) {
      this._process.kill('SIGTERM');
      // Give it a second to exit cleanly before forcing
      await new Promise((res) => setTimeout(res, 1000));
      if (this._process && !this._process.killed) {
        this._process.kill('SIGKILL');
      }
      this._process = null;
    }
    console.log('[BackendLifecycle] Backend stopped.');
  }

  // ── Internal ────────────────────────────────────────────────────────────────

  async _spawn() {
    const { cmd, args, isBundled } = resolveBackendCommand();
    const aspnetEnvironment =
      process.env.ASPNETCORE_ENVIRONMENT || (isBundled ? 'Production' : 'Development');
    console.log(`[BackendLifecycle] Spawning backend: ${cmd} ${args.join(' ')}`);

    this._process = spawn(cmd, args, {
      env: {
        ...process.env,
        GODOT_DESKTOP_IPC: '1',
        ASPNETCORE_ENVIRONMENT: aspnetEnvironment,
      },
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    });

    this._process.stdout?.on('data', (d) =>
      console.log('[backend]', d.toString().trimEnd()));
    this._process.stderr?.on('data', (d) =>
      console.error('[backend:err]', d.toString().trimEnd()));

    this._process.on('exit', (code, signal) => {
      if (this._stopping) return;
      console.warn(`[BackendLifecycle] Backend exited (code=${code}, signal=${signal}); scheduling restart.`);
      this._ready = false;
      this.emit('crashed', { code, signal });
      this._scheduledRestart();
    });
  }

  _scheduledRestart() {
    const now = Date.now();
    if (now - this._lastRestartAt < RAPID_WINDOW) {
      this._restartCount++;
    } else {
      this._restartCount = 1;
    }
    this._lastRestartAt = now;

    if (this._restartCount > MAX_RESTARTS) {
      console.error('[BackendLifecycle] Max restarts exceeded; giving up supervision.');
      this.emit('failed');
      return;
    }

    console.log(`[BackendLifecycle] Restarting in ${RESTART_DELAY}ms (attempt ${this._restartCount}/${MAX_RESTARTS}).`);
    setTimeout(async () => {
      if (this._stopping) return;
      try {
        await this._spawn();
        await waitForPipeReady(READY_TIMEOUT);
        this._ready = true;
        this.emit('ready');
      } catch (err) {
        console.error('[BackendLifecycle] Restart failed:', err.message);
        this._scheduledRestart();
      }
    }, RESTART_DELAY);
  }
}

module.exports = new BackendLifecycle();

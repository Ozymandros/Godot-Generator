'use strict';

/**
 * @file backendLifecycle.cjs
 *
 * MIGRATED to use @ozymandros/electron-message-bridge/lifecycle
 *
 * Manages the local .NET backend process lifetime using ChildProcessLifecycle:
 *   - spawn with GODOT_DESKTOP_IPC=1 via ChildProcessLifecycle
 *   - verify readiness via named-pipe handshake (readyCheck)
 *   - supervise with automatic restart on unexpected exit (restartPolicy)
 *   - graceful shutdown on app-quit via lifecycle.stop()
 *
 * Usage:
 *   const lifecycle = require('./backendLifecycle.cjs');
 *   await lifecycle.start();          // called from app.whenReady()
 *   await lifecycle.stop();           // called from app.before-quit
 *   lifecycle.isReady()               // true once handshake succeeds
 */

const { ChildProcessLifecycle } = require('@ozymandros/electron-message-bridge/lifecycle');
const net = require('net');
const path = require('path');
const EventEmitter = require('events');
const fs = require('fs');

// ── Configuration ────────────────────────────────────────────────────────────

const PIPE_NAME = '\\\\.\\pipe\\godot-generator-ipc';
const READY_TIMEOUT = 300_000;   // ms to wait for pipe handshake after spawn
const RESTART_DELAY = 2_000;    // ms between supervised restart attempts
const MAX_RESTARTS = 5;        // stop supervising after this many rapid restarts
const RAPID_WINDOW = 10_000;   // ms window for counting rapid restarts
const DEFAULT_UI_URL = 'http://127.0.0.1:5044';
const EXISTING_BACKEND_PROBE_TIMEOUT = 1_000;

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
  if (fs.existsSync(bundled)) {
    return { cmd: bundled, args: [], isBundled: true };
  }

  // Development fallback: dotnet run
  const projectDir = path.resolve(__dirname, '..', 'GodotGenerator.Blazor', 'GodotGenerator.Blazor');
  const urls = process.env.GODOT_BLAZOR_URL || DEFAULT_UI_URL;
  return {
    cmd: 'dotnet',
    args: ['run', '-c', 'Release', '--project', projectDir, '--no-launch-profile', '--urls', urls],
    isBundled: false,
  };
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

// ── BackendLifecycle wrapper around ChildProcessLifecycle ────────────────────

class BackendLifecycle extends EventEmitter {
  constructor() {
    super();
    /** @type {ChildProcessLifecycle | null} */
    this._lifecycle = null;
    this._ready = false;
    this._stopping = false;
    this._existingBackendDetected = false;
  }

  /** Whether the backend pipe has completed its readiness handshake. */
  isReady() { return this._ready; }

  /**
   * Initializes and starts the backend process using ChildProcessLifecycle.
   * Resolves when the backend is ready to accept IPC commands.
   */
  async start() {
    this._stopping = false;
    this._existingBackendDetected = false;

    // If another backend instance is already serving the named pipe (e.g. from
    // a previous Electron run), reuse it instead of spawning a duplicate that
    // would fail to bind the same HTTP port.
    try {
      await waitForPipeReady(EXISTING_BACKEND_PROBE_TIMEOUT);
      this._ready = true;
      this._existingBackendDetected = true;
      console.log('[BackendLifecycle] Reusing existing backend already ready on pipe:', PIPE_NAME);
      this.emit('ready');
      return;
    } catch {
      // No existing backend detected; continue with normal spawn path.
    }

    const { cmd, args, isBundled } = resolveBackendCommand();
    const aspnetEnvironment =
      process.env.ASPNETCORE_ENVIRONMENT || (isBundled ? 'Production' : 'Development');

    console.log(`[BackendLifecycle] Initializing ChildProcessLifecycle for: ${cmd} ${args.join(' ')}`);

    // Allow a much longer ready timeout in development (dotnet run needs
    // time to build/publish). Production (bundled) remains the shorter
    // timeout.
    const effectiveReadyTimeoutMs = isBundled ? READY_TIMEOUT : 900_000; // 10 minutes

    this._lifecycle = new ChildProcessLifecycle({
      command: cmd,
      args: args,
      // The lifecycle implementation expects these at the top-level of the
      // options object (not nested under `options`). Map our settings to the
      // names that the packaged lifecycle uses.
      env: {
        ...process.env,
        GODOT_DESKTOP_IPC: '1',
        ASPNETCORE_ENVIRONMENT: aspnetEnvironment,
        ASPNETCORE_Logging__LogLevel__Microsoft_AspNetCore_Server_Kestrel:
          process.env.ASPNETCORE_Logging__LogLevel__Microsoft_AspNetCore_Server_Kestrel || 'Error',
      },
      spawnOptions: {
        stdio: ['ignore', 'pipe', 'pipe'],
        windowsHide: true,
      },

      // Readiness check via named pipe
      readyCheck: async (process) => {
        try {
          await waitForPipeReady(effectiveReadyTimeoutMs);
          return true;
        } catch {
          return false;
        }
      },

      // Lifecycle-specific timeouts and restart policy (names expected by the
      // @ozymandros lifecycle implementation)
      readyTimeoutMs: effectiveReadyTimeoutMs,
      forceKillAfterMs: 10000,
      maxRestarts: MAX_RESTARTS,
      restartDelayMs: RESTART_DELAY,
      rapidRestartWindowMs: RAPID_WINDOW,
    });

    // diagnostic: inspect lifecycle instance safely
    const lifecycleListenerKeys = this._lifecycle && this._lifecycle.listeners
      ? Object.keys(this._lifecycle.listeners)
      : [];
    console.log('ChildProcessLifecycle instance listener keys:', lifecycleListenerKeys);
    console.log('ChildProcessLifecycle proto methods:', Object.getOwnPropertyNames(Object.getPrototypeOf(this._lifecycle || {})));
    if (typeof this._lifecycle.on !== 'function') {
      console.error('ChildProcessLifecycle missing .on()');
    }

    // Register only events the lifecycle implementation actually exposes.
    // The packaged ChildProcessLifecycle initializes a `listeners` map with
    // a limited set of keys (e.g. 'ready','crashed','failed'). Attempting to
    // .on() for unsupported events will throw because the backing Set is
    // undefined.
    const registerIfSupported = (eventName, cb) => {
      if (this._lifecycle && this._lifecycle.listeners && Object.prototype.hasOwnProperty.call(this._lifecycle.listeners, eventName)) {
        this._lifecycle.on(eventName, cb);
      } else {
        console.debug(`[BackendLifecycle] Skipping unsupported lifecycle event registration: ${eventName}`);
      }
    };

    registerIfSupported('ready', () => {
      this._ready = true;
      console.log('[BackendLifecycle] Backend is ready on pipe:', PIPE_NAME);
      this.emit('ready');
    });

    registerIfSupported('crashed', ({ exitCode, signal }) => {
      this._ready = false;
      console.warn(`[BackendLifecycle] Backend exited (code=${exitCode}, signal=${signal}); lifecycle will handle restart.`);
      this.emit('crashed', { code: exitCode, signal });
    });

    registerIfSupported('failed', (reason) => {
      console.error('[BackendLifecycle] Backend failed after max restarts:', reason?.message || reason);
      this.emit('failed', reason);
    });

    // Intercept the lifecycle's `child` assignment so we can attach stdout/
    // stderr handlers immediately when the process is spawned. This ensures
    // we capture early startup logs even if the lifecycle's readyCheck times
    // out.
    try {
      const outer = this;
      let _childBacking = null;
      const desc = Object.getOwnPropertyDescriptor(this._lifecycle, 'child');
      if (!desc || desc.configurable !== false) {
        Object.defineProperty(this._lifecycle, 'child', {
          configurable: true,
          enumerable: true,
          get() {
            return _childBacking;
          },
          set(child) {
            _childBacking = child;
            if (child) {
              child.stdout?.on('data', (d) => {
                const text = d.toString().trimEnd();
                console.log('[backend]', text);
                outer._emitLogLines('stdout', text);
              });
              child.stderr?.on('data', (d) => {
                const text = d.toString().trimEnd();
                console.error('[backend:err]', text);
                outer._emitLogLines('stderr', text);
              });
            }
          },
        });
      }
    } catch (err) {
      console.debug('[BackendLifecycle] Could not intercept lifecycle.child:', err);
    }

    // If child already exists, attach handlers immediately as a fallback.
    if (this._lifecycle.child) {
      this._lifecycle.child.stdout?.on('data', (d) => {
        const text = d.toString().trimEnd();
        console.log('[backend]', text);
        this._emitLogLines('stdout', text);
      });

      this._lifecycle.child.stderr?.on('data', (d) => {
        const text = d.toString().trimEnd();
        console.error('[backend:err]', text);
        this._emitLogLines('stderr', text);
      });
    }

    // Start the lifecycle
    await this._lifecycle.start();
  }

  /**
   * Gracefully shuts down the backend process using lifecycle.stop().
   */
  async stop() {
    this._stopping = true;
    this._ready = false;

    if (this._existingBackendDetected) {
      // We reused an existing backend, don't kill it
      console.log('[BackendLifecycle] Not stopping reused backend.');
      return;
    }

    if (this._lifecycle) {
      try {
        await this._lifecycle.stop();
        console.log('[BackendLifecycle] Backend stopped gracefully via lifecycle.');
      } catch (error) {
        console.error('[BackendLifecycle] Error during lifecycle stop:', error);
      }
      this._lifecycle = null;
    }
  }

  // ── Internal ────────────────────────────────────────────────────────────────

  /**
   * Splits a raw stdout/stderr chunk into individual non-empty lines and emits
   * a `log` event for each.  Handles both Unix (`\n`) and Windows (`\r\n`) line
   * endings; skips blank lines; caps each line at 2 000 characters to bound
   * per-entry payload while still capturing long diagnostics across consecutive
   * events.
   *
   * Consumers (e.g. Electron main) can forward these events to the renderer so
   * backend logs are visible in the in-app log stream without opening a terminal.
   *
   * @param {'stdout'|'stderr'} stream
   * @param {string} chunk  Raw data chunk, potentially multi-line.
   */
  _emitLogLines(stream, chunk) {
    const MAX_LINE_LENGTH = 2_000;
    const timestamp = new Date().toISOString();
    chunk
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line.length > 0)
      .forEach((line) => {
        const message = line.length > MAX_LINE_LENGTH
          ? line.slice(0, MAX_LINE_LENGTH) + ' …[truncated]'
          : line;
        this.emit('log', { stream, message, timestamp });
      });
  }
}

const lifecycle = new BackendLifecycle();

// Test hooks for unit testing
function __setTestDeps(partial) {
  // Note: With ChildProcessLifecycle, test dependency injection is handled
  // differently. This function is kept for backward compatibility but may
  // have limited effect on the lifecycle module itself.
  if (!partial || typeof partial !== 'object') return;
  // For tests that need to mock the lifecycle, they should mock
  // require('@ozymandros/electron-message-bridge/lifecycle') directly
}

function __resetTestDeps() {
  // Reset is a no-op with the migrated implementation
}

module.exports = lifecycle;
module.exports.BackendLifecycle = BackendLifecycle;
module.exports.resolveBackendCommand = resolveBackendCommand;
module.exports.waitForPipeReady = waitForPipeReady;
module.exports.__setTestDeps = __setTestDeps;
module.exports.__resetTestDeps = __resetTestDeps;

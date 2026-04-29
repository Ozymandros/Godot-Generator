'use strict';

/**
 * @file pipeBroker.cjs
 * Translates Electron main-process IPC calls into named-pipe command envelopes,
 * sends them to the local .NET backend, and returns the response envelope.
 *
 * Wire protocol (matches NamedPipeCommandHost.cs):
 *   Request:  [4-byte LE uint32 length][UTF-8 JSON CommandEnvelope]
 *   Response: [4-byte LE uint32 length][UTF-8 JSON ResponseEnvelope]    (legacy)
 *           | [4-byte LE uint32 length][UTF-8 JSON WizardWireFrame] ... (wizard streaming)
 *
 * Wizard streaming frames use a discriminated wrapper:
 *   Progress : { "t": "p", "p": { ...WizardProgressFrame } }
 *   Final    : { "t": "f", "e": { ...ResponseEnvelope      } }
 *
 * Backward compatibility: if the first parsed object lacks the "t" discriminator field
 * it is treated as a legacy single-frame ResponseEnvelope (non-wizard commands).
 *
 * Each call opens a fresh pipe connection, performs one request/response cycle
 * (possibly preceded by N progress frames for wizard commands), then closes the
 * socket — keeping the implementation stateless.
 */

const net = require('net');

const PIPE_NAME       = '\\\\.\\pipe\\godot-generator-ipc';
const DEFAULT_TIMEOUT = 180_000; // 3 minutes per individual command call, plus a bit of leeway for startup/shutdown sequences. Wizard commands that expect multiple progress

let _callCounter = 0;
const deps = {
  netModule:      net,
  setTimeoutFn:   setTimeout,
  clearTimeoutFn: clearTimeout,
  nowFn:          () => Date.now(),
};

/**
 * Generates a unique correlation id for each outgoing command.
 * @returns {string}
 */
function newCorrelationId() {
  _callCounter++;
  return `elec-${deps.nowFn()}-${_callCounter}`;
}

// ── Wire helpers ──────────────────────────────────────────────────────────────

/**
 * Builds the framed request buffer: 4-byte LE length + UTF-8 JSON payload.
 * @param {object} envelope
 * @returns {Buffer}
 */
function buildFrame(envelope) {
  const payload = Buffer.from(JSON.stringify(envelope), 'utf8');
  const frame   = Buffer.allocUnsafe(4 + payload.length);
  frame.writeUInt32LE(payload.length, 0);
  payload.copy(frame, 4);
  return frame;
}

/**
 * Reads a single length-prefixed JSON frame from the socket stream.
 * Buffers incoming chunks until a complete `[uint32 length][payload]` frame
 * has been received, then resolves with the parsed object.
 *
 * Used internally by {@link readFrames} and exported for legacy unit-test
 * compatibility (the existing `readResponse` tests call this directly).
 *
 * @param {import('net').Socket} socket
 * @returns {Promise<object>} Parsed JSON object (caller interprets type).
 */
function readResponse(socket) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let expectedLength = -1;
    let received = 0;

    function onData(chunk) {
      chunks.push(chunk);
      received += chunk.length;

      if (expectedLength === -1 && received >= 4) {
        const header = Buffer.concat(chunks);
        expectedLength = header.readUInt32LE(0);
      }

      if (expectedLength !== -1 && received >= 4 + expectedLength) {
        socket.removeListener('data', onData);
        socket.removeListener('error', onError);
        socket.removeListener('end', onEnd);

        const all     = Buffer.concat(chunks);
        const payload = all.slice(4, 4 + expectedLength);
        try {
          resolve(JSON.parse(payload.toString('utf8')));
        } catch (err) {
          reject(new Error('Failed to parse backend response: ' + err.message));
        }
      }
    }

    function onError(err) { reject(err); }
    function onEnd()      { reject(new Error('Pipe closed before full response was received.')); }

    socket.on('data',  onData);
    socket.on('error', onError);
    socket.on('end',   onEnd);
  });
}

/**
 * Reads all wire frames from the socket for a wizard streaming connection.
 *
 * Repeatedly calls {@link readResponse} (which reads exactly one length-prefixed
 * JSON frame at a time by removing its listeners after each frame) until a frame
 * with `{ "t": "f" }` (final) is received, at which point the loop stops and the
 * inner `ResponseEnvelope` (`frame.e`) is returned.
 *
 * Each intermediate `{ "t": "p" }` (progress) frame causes `onProgress` to be
 * called synchronously with the inner `WizardProgressFrame` payload (`frame.p`).
 *
 * **Backward compatibility**: if the very first frame has no `"t"` property the
 * connection is treated as a legacy single-frame response and that object is
 * returned directly without calling `onProgress`.
 *
 * @param {import('net').Socket} socket
 * @param {(frame: object) => void} [onProgress] Optional progress callback.
 * @returns {Promise<object>} Final ResponseEnvelope.
 */
async function readFrames(socket, onProgress) {
  let firstFrame = true;

  // eslint-disable-next-line no-constant-condition
  while (true) {
    const frame = await readResponse(socket);

    // Backward compat: legacy single-frame response (no discriminator field).
    if (firstFrame && typeof frame.t === 'undefined') {
      return frame;
    }
    firstFrame = false;

    if (frame.t === 'p') {
      // Progress frame — forward payload to caller, continue looping.
      if (typeof onProgress === 'function' && frame.p != null) {
        onProgress(frame.p);
      }
      continue;
    }

    if (frame.t === 'f') {
      // Final frame — return the inner ResponseEnvelope.
      if (frame.e == null) {
        throw new Error('Wizard final frame received but envelope ("e") field is missing.');
      }
      return frame.e;
    }

    // Unknown discriminator — treat as a protocol error rather than silently ignoring.
    throw new Error(`Unknown wizard wire frame type: "${frame.t}"`);
  }
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Sends a versioned IPC command to the .NET backend and returns the response envelope.
 *
 * For wizard commands the server may emit zero or more progress frames before the
 * final `ResponseEnvelope`; these are forwarded to `options.onProgress` as they
 * arrive. Non-wizard commands use the legacy single-frame path automatically.
 *
 * @param {string}   command   Versioned command name, e.g. `Config.GetAll/v1`.
 * @param {object|null} payload  Command-specific payload (will be JSON-stringified).
 * @param {{ timeoutMs?: number, onProgress?: (frame: object) => void }|null} options
 *   Optional invocation options.
 *   - `timeoutMs`  – Override the default per-call timeout (ms).
 *   - `onProgress` – Callback invoked for each `WizardProgressFrame` received
 *                    before the final response. No-op for non-wizard commands.
 * @returns {Promise<{success: boolean, payloadJson: string|null, errorCode: string|null, errorMessage: string|null}>}
 */
async function invoke(command, payload = null, options = null) {
  const correlationId = newCorrelationId();
  const timeoutMs = Number.isFinite(options?.timeoutMs) && options.timeoutMs > 0
    ? options.timeoutMs
    : DEFAULT_TIMEOUT;

  const envelope = {
    correlationId,
    command,
    payloadJson:   payload !== null ? JSON.stringify(payload) : null,
    schemaVersion: '1',
  };

  return new Promise((resolve, reject) => {
    const socket = deps.netModule.createConnection(PIPE_NAME, async () => {
      try {
        socket.write(buildFrame(envelope));
        const response = await readFrames(socket, options?.onProgress);
        deps.clearTimeoutFn(timer);
        socket.destroy();

        if (response.correlationId !== correlationId) {
          reject(new Error(`Correlation id mismatch: expected ${correlationId}, got ${response.correlationId}`));
        } else {
          resolve(response);
        }
      } catch (err) {
        deps.clearTimeoutFn(timer);
        socket.destroy();
        reject(err);
      }
    });

    const timer = deps.setTimeoutFn(
      () => {
        socket.destroy();
        reject(new Error(`IPC call '${command}' timed out after ${timeoutMs}ms.`));
      },
      timeoutMs,
    );

    socket.on('error', (err) => {
      deps.clearTimeoutFn(timer);
      reject(new Error(`Pipe connection error for command '${command}': ${err.message}`));
    });
  });
}

function __setTestDeps(partial) {
  if (!partial || typeof partial !== 'object') return;
  if (partial.netModule)      deps.netModule      = partial.netModule;
  if (partial.setTimeoutFn)   deps.setTimeoutFn   = partial.setTimeoutFn;
  if (partial.clearTimeoutFn) deps.clearTimeoutFn = partial.clearTimeoutFn;
  if (partial.nowFn)          deps.nowFn          = partial.nowFn;
}

function __resetTestDeps() {
  deps.netModule      = net;
  deps.setTimeoutFn   = setTimeout;
  deps.clearTimeoutFn = clearTimeout;
  deps.nowFn          = () => Date.now();
  _callCounter        = 0;
}

module.exports = {
  invoke,
  buildFrame,
  readResponse,
  readFrames,
  newCorrelationId,
  __setTestDeps,
  __resetTestDeps,
};

'use strict';

/**
 * @file pipeBroker.cjs
 * Translates Electron main-process IPC calls into named-pipe command envelopes,
 * sends them to the local .NET backend, and returns the response envelope.
 *
 * Wire protocol (matches NamedPipeCommandHost.cs):
 *   Request:  [4-byte LE uint32 length][UTF-8 JSON CommandEnvelope]
 *   Response: [4-byte LE uint32 length][UTF-8 JSON ResponseEnvelope]
 *
 * Each call opens a fresh pipe connection, performs one request/response cycle,
 * then closes the socket — keeping the implementation stateless.
 */

const net = require('net');

const PIPE_NAME     = '\\\\.\\pipe\\godot-generator-ipc';
const CALL_TIMEOUT  = 600_000; // ms per individual command call

let _callCounter = 0;

/**
 * Generates a unique correlation id for each outgoing command.
 * @returns {string}
 */
function newCorrelationId() {
  _callCounter++;
  return `elec-${Date.now()}-${_callCounter}`;
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
 * Reads a length-prefixed JSON response from the socket stream.
 * Buffers incoming chunks until a complete frame has been received.
 * @param {net.Socket} socket
 * @returns {Promise<object>} Parsed ResponseEnvelope.
 */
function readResponse(socket) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let expectedLength = -1;
    let received = 0;

    socket.on('data', (chunk) => {
      chunks.push(chunk);
      received += chunk.length;

      if (expectedLength === -1 && received >= 4) {
        const header = Buffer.concat(chunks);
        expectedLength = header.readUInt32LE(0);
      }

      if (expectedLength !== -1 && received >= 4 + expectedLength) {
        const all     = Buffer.concat(chunks);
        const payload = all.slice(4, 4 + expectedLength);
        try {
          resolve(JSON.parse(payload.toString('utf8')));
        } catch (err) {
          reject(new Error('Failed to parse backend response: ' + err.message));
        }
      }
    });

    socket.on('error', reject);
    socket.on('end', () => reject(new Error('Pipe closed before full response was received.')));
  });
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Sends a versioned IPC command to the .NET backend and returns the response envelope.
 *
 * @param {string} command    Versioned command name, e.g. `Config.GetAll/v1`.
 * @param {object|null} payload  Command-specific payload (will be JSON-stringified).
 * @returns {Promise<{success: boolean, payloadJson: string|null, errorCode: string|null, errorMessage: string|null}>}
 */
async function invoke(command, payload = null) {
  const correlationId = newCorrelationId();

  const envelope = {
    correlationId,
    command,
    payloadJson:   payload !== null ? JSON.stringify(payload) : null,
    schemaVersion: '1',
  };

  return new Promise((resolve, reject) => {
    const timer  = setTimeout(
      () => { socket.destroy(); reject(new Error(`IPC call '${command}' timed out after ${CALL_TIMEOUT}ms.`)); },
      CALL_TIMEOUT,
    );

    const socket = net.createConnection(PIPE_NAME, async () => {
      try {
        socket.write(buildFrame(envelope));
        const response = await readResponse(socket);
        clearTimeout(timer);
        socket.destroy();

        if (response.correlationId !== correlationId) {
          reject(new Error(`Correlation id mismatch: expected ${correlationId}, got ${response.correlationId}`));
        } else {
          resolve(response);
        }
      } catch (err) {
        clearTimeout(timer);
        socket.destroy();
        reject(err);
      }
    });

    socket.on('error', (err) => {
      clearTimeout(timer);
      reject(new Error(`Pipe connection error for command '${command}': ${err.message}`));
    });
  });
}

module.exports = { invoke };

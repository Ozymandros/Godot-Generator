const test = require('node:test');
const assert = require('node:assert/strict');
const EventEmitter = require('node:events');

const pipeBroker = require('../pipeBroker.cjs');

function failOnRealConnection() {
  throw new Error('Test attempted a real pipe connection.');
}

function frame(payload) {
  const body = Buffer.from(JSON.stringify(payload), 'utf8');
  const full = Buffer.alloc(4 + body.length);
  full.writeUInt32LE(body.length, 0);
  body.copy(full, 4);
  return full;
}

test.afterEach(() => {
  pipeBroker.__resetTestDeps();
});

test.beforeEach(() => {
  pipeBroker.__setTestDeps({
    netModule: { createConnection: failOnRealConnection },
  });
});

// ── Legacy single-frame tests (unchanged) ────────────────────────────────────

test('invoke sends payload and resolves response envelope', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};
  socket.write = (buffer) => {
    const payloadLen = buffer.readUInt32LE(0);
    const req = JSON.parse(buffer.slice(4, 4 + payloadLen).toString('utf8'));
    const response = {
      correlationId: req.correlationId,
      success: true,
      payloadJson: '{"ok":true}',
      errorCode: null,
      errorMessage: null,
    };
    setImmediate(() => socket.emit('data', frame(response)));
  };

  pipeBroker.__setTestDeps({
    netModule: {
      createConnection: (_pipe, onConnect) => {
        setImmediate(onConnect);
        return socket;
      },
    },
  });

  const result = await pipeBroker.invoke('Config.GetAll/v1', { a: 1 });
  assert.equal(result.success, true);
  assert.equal(result.payloadJson, '{"ok":true}');
});

test('invoke rejects when correlation id mismatches', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};
  socket.write = () => {
    const response = {
      correlationId: 'wrong-id',
      success: true,
      payloadJson: '{}',
      errorCode: null,
      errorMessage: null,
    };
    setImmediate(() => socket.emit('data', frame(response)));
  };

  pipeBroker.__setTestDeps({
    netModule: {
      createConnection: (_pipe, onConnect) => {
        setImmediate(onConnect);
        return socket;
      },
    },
  });

  await assert.rejects(
    pipeBroker.invoke('Config.GetAll/v1', null),
    /correlation id mismatch/i,
  );
});

test('invoke wraps connection errors with command context', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  pipeBroker.__setTestDeps({
    netModule: {
      createConnection: () => {
        setImmediate(() => socket.emit('error', new Error('connection refused')));
        return socket;
      },
    },
  });

  await assert.rejects(
    pipeBroker.invoke('PromptAssist.Enhance/v1', null),
    /Pipe connection error.*PromptAssist\.Enhance\/v1.*connection refused/i,
  );
});

test('readResponse rejects on malformed JSON payload', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};
  const invalid = Buffer.from('{"bad"', 'utf8');
  const full = Buffer.alloc(4 + invalid.length);
  full.writeUInt32LE(invalid.length, 0);
  invalid.copy(full, 4);

  const promise = pipeBroker.readResponse(socket);
  socket.emit('data', full);

  await assert.rejects(promise, /Failed to parse backend response/i);
});

// ── readFrames — wizard streaming protocol tests ──────────────────────────────

test('readFrames resolves with final envelope after progress frames', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  const progressPayloads = [];

  // Emit two progress frames then a final frame asynchronously.
  setImmediate(() => {
    socket.emit('data', frame({ t: 'p', p: { phase: 'status', message: 'Starting…' } }));
    setImmediate(() => {
      socket.emit('data', frame({ t: 'p', p: { phase: 'tool', message: 'Invoking Plugin.Fn' } }));
      setImmediate(() => {
        socket.emit('data', frame({
          t: 'f',
          e: { correlationId: 'c1', success: true, payloadJson: '{}', errorCode: null, errorMessage: null },
        }));
      });
    });
  });

  const result = await pipeBroker.readFrames(socket, (frame) => progressPayloads.push(frame));

  assert.equal(result.correlationId, 'c1');
  assert.equal(result.success, true);
  assert.equal(progressPayloads.length, 2);
  assert.equal(progressPayloads[0].phase, 'status');
  assert.equal(progressPayloads[0].message, 'Starting…');
  assert.equal(progressPayloads[1].phase, 'tool');
});

test('readFrames resolves immediately for wizard final frame with no progress frames', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  setImmediate(() => {
    socket.emit('data', frame({
      t: 'f',
      e: { correlationId: 'c2', success: false, payloadJson: null, errorCode: 'ERR', errorMessage: 'fail' },
    }));
  });

  const called = [];
  const result = await pipeBroker.readFrames(socket, (f) => called.push(f));

  assert.equal(result.correlationId, 'c2');
  assert.equal(result.success, false);
  assert.equal(called.length, 0);
});

test('readFrames backward-compat: legacy single-frame (no "t" field) resolves without calling onProgress', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  const legacyResponse = {
    correlationId: 'c3',
    success: true,
    payloadJson: '{"legacy":true}',
    errorCode: null,
    errorMessage: null,
  };

  setImmediate(() => socket.emit('data', frame(legacyResponse)));

  const progressCalls = [];
  const result = await pipeBroker.readFrames(socket, (f) => progressCalls.push(f));

  assert.equal(result.correlationId, 'c3');
  assert.equal(JSON.parse(result.payloadJson).legacy, true);
  assert.equal(progressCalls.length, 0, 'onProgress must not be called for legacy frames');
});

test('readFrames rejects when final frame is missing the "e" envelope field', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  setImmediate(() => {
    socket.emit('data', frame({ t: 'f' /* e field absent */ }));
  });

  await assert.rejects(
    pipeBroker.readFrames(socket),
    /final frame.*envelope.*"e".*missing/i,
  );
});

test('readFrames rejects on unknown discriminator type', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  setImmediate(() => {
    socket.emit('data', frame({ t: 'x', payload: 'unknown' }));
  });

  await assert.rejects(
    pipeBroker.readFrames(socket),
    /unknown wizard wire frame type.*"x"/i,
  );
});

test('invoke with onProgress forwards progress frames to callback', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};
  const captured = [];

  socket.write = (buffer) => {
    const payloadLen = buffer.readUInt32LE(0);
    const req = JSON.parse(buffer.slice(4, 4 + payloadLen).toString('utf8'));

    // Simulate: one progress frame then final.
    setImmediate(() => {
      socket.emit('data', frame({ t: 'p', p: { phase: 'status', message: 'Working…' } }));
      setImmediate(() => {
        socket.emit('data', frame({
          t: 'f',
          e: {
            correlationId: req.correlationId,
            success: true,
            payloadJson: '"done"',
            errorCode: null,
            errorMessage: null,
          },
        }));
      });
    });
  };

  pipeBroker.__setTestDeps({
    netModule: {
      createConnection: (_pipe, onConnect) => {
        setImmediate(onConnect);
        return socket;
      },
    },
  });

  const result = await pipeBroker.invoke(
    'Generate.Wizard/v1',
    { prompt: 'hello' },
    { onProgress: (f) => captured.push(f) },
  );

  assert.equal(result.success, true);
  assert.equal(captured.length, 1);
  assert.equal(captured[0].phase, 'status');
  assert.equal(captured[0].message, 'Working…');
});

test('readFrames works correctly when onProgress is omitted', async () => {
  const socket = new EventEmitter();
  socket.destroy = () => {};

  setImmediate(() => {
    socket.emit('data', frame({ t: 'p', p: { phase: 'status', message: 'Ignored…' } }));
    setImmediate(() => {
      socket.emit('data', frame({
        t: 'f',
        e: { correlationId: 'c5', success: true, payloadJson: null, errorCode: null, errorMessage: null },
      }));
    });
  });

  // No onProgress callback passed — should not throw.
  const result = await pipeBroker.readFrames(socket);

  assert.equal(result.correlationId, 'c5');
  assert.equal(result.success, true);
});

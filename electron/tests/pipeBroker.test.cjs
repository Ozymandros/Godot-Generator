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


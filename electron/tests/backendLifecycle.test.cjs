const test = require('node:test');
const assert = require('node:assert/strict');
const EventEmitter = require('node:events');

const lifecycleModule = require('../backendLifecycle.cjs');

// Note: Tests for resolveBackendCommand and waitForPipeReady remain valid.
// Tests that relied on mocking child_process.spawn are now handled by
// ChildProcessLifecycle internally, so those specific tests have been updated
// to reflect the new architecture.

function createSocketThatConnects() {
  const socket = new EventEmitter();
  socket.destroy = () => {};
  setImmediate(() => socket.emit('connect'));
  return socket;
}

function createSocketThatErrors(message = 'not ready') {
  const socket = new EventEmitter();
  socket.destroy = () => {};
  setImmediate(() => socket.emit('error', new Error(message)));
  return socket;
}

test.afterEach(() => {
  lifecycleModule.__resetTestDeps();
});

test('resolveBackendCommand returns bundled executable when present', () => {
  const oldResourcesPath = process.resourcesPath;
  process.resourcesPath = '/fake-resources';

  // Mock fs.existsSync to return true for bundled path
  const originalFs = require('fs');
  const originalExistsSync = originalFs.existsSync;
  originalFs.existsSync = () => true;

  try {
    const result = lifecycleModule.resolveBackendCommand();
    assert.equal(result.isBundled, true);
    assert.equal(result.args.length, 0);
    assert.match(result.cmd, /backend/i);
  } finally {
    originalFs.existsSync = originalExistsSync;
    process.resourcesPath = oldResourcesPath;
  }
});

test('resolveBackendCommand falls back to dotnet run in dev', () => {
  const originalFs = require('fs');
  const originalExistsSync = originalFs.existsSync;
  originalFs.existsSync = () => false;

  try {
    const result = lifecycleModule.resolveBackendCommand();
    assert.equal(result.isBundled, false);
    assert.equal(result.cmd, 'dotnet');
    assert.deepEqual(result.args.slice(0, 3), ['run', '-c', 'Release']);
  } finally {
    originalFs.existsSync = originalExistsSync;
  }
});

test('waitForPipeReady resolves when socket connects', async () => {
  const originalNet = require('net');
  const originalCreateConnection = originalNet.createConnection;
  
  originalNet.createConnection = () => createSocketThatConnects();

  try {
    await assert.doesNotReject(lifecycleModule.waitForPipeReady(100));
  } finally {
    originalNet.createConnection = originalCreateConnection;
  }
});

test('waitForPipeReady rejects on timeout after repeated errors', async () => {
  const originalNet = require('net');
  const originalCreateConnection = originalNet.createConnection;
  let tick = 0;
  
  originalNet.createConnection = () => createSocketThatErrors();
  const originalSetTimeout = global.setTimeout;
  const originalDateNow = Date.now;
  
  global.Date.now = () => (tick++ === 0 ? 0 : 2);
  global.setTimeout = (fn) => fn();

  try {
    await assert.rejects(
      lifecycleModule.waitForPipeReady(1),
      /not ready within 1ms/i,
    );
  } finally {
    originalNet.createConnection = originalCreateConnection;
    global.setTimeout = originalSetTimeout;
    global.Date.now = originalDateNow;
  }
});

test('BackendLifecycle.start reuses already running backend and skips spawn', async () => {
  const originalNet = require('net');
  const originalCreateConnection = originalNet.createConnection;
  
  originalNet.createConnection = () => createSocketThatConnects();

  try {
    const instance = new lifecycleModule.BackendLifecycle();
    await instance.start();

    assert.equal(instance.isReady(), true);
    // With ChildProcessLifecycle, we don't track spawn calls directly
    // but the reuse path should not create a lifecycle instance
    assert.equal(instance._lifecycle, null, 'Should not create lifecycle when reusing existing backend');
    
    await instance.stop();
  } finally {
    originalNet.createConnection = originalCreateConnection;
  }
});

test('BackendLifecycle._emitLogLines emits one log event per non-empty line', () => {
  const instance = new lifecycleModule.BackendLifecycle();

  const events = [];
  instance.on('log', (payload) => events.push(payload));

  instance._emitLogLines('stdout', 'line one\nline two\n\nline three');

  assert.equal(events.length, 3, 'Should emit 3 events (blank line skipped)');
  assert.equal(events[0].stream, 'stdout');
  assert.equal(events[0].message, 'line one');
  assert.equal(events[1].message, 'line two');
  assert.equal(events[2].message, 'line three');
  assert.ok(typeof events[0].timestamp === 'string', 'timestamp must be a string');
});

test('BackendLifecycle._emitLogLines handles Windows CRLF line endings', () => {
  const instance = new lifecycleModule.BackendLifecycle();

  const events = [];
  instance.on('log', (payload) => events.push(payload));

  instance._emitLogLines('stderr', 'err one\r\nerr two\r\n');

  assert.equal(events.length, 2);
  assert.equal(events[0].stream, 'stderr');
  assert.equal(events[0].message, 'err one');
  assert.equal(events[1].message, 'err two');
});

test('BackendLifecycle._emitLogLines truncates lines exceeding 2000 characters', () => {
  const instance = new lifecycleModule.BackendLifecycle();

  const events = [];
  instance.on('log', (payload) => events.push(payload));

  const longLine = 'x'.repeat(2_500);
  instance._emitLogLines('stdout', longLine);

  assert.equal(events.length, 1);
  assert.ok(events[0].message.endsWith('…[truncated]'));
  assert.ok(events[0].message.length <= 2_015, 'truncated message must not far exceed 2000 chars');
});

test('BackendLifecycle.stop does not kill reused backend', async () => {
  const originalNet = require('net');
  const originalCreateConnection = originalNet.createConnection;
  
  originalNet.createConnection = () => createSocketThatConnects();

  try {
    const instance = new lifecycleModule.BackendLifecycle();
    await instance.start();
    
    assert.equal(instance._existingBackendDetected, true);
    
    // Stop should not attempt to kill the reused backend
    await instance.stop();
    
    assert.equal(instance._ready, false);
  } finally {
    originalNet.createConnection = originalCreateConnection;
  }
});

test('BackendLifecycle emits ready event when started', async () => {
  const originalNet = require('net');
  const originalCreateConnection = originalNet.createConnection;
  
  originalNet.createConnection = () => createSocketThatConnects();

  try {
    const instance = new lifecycleModule.BackendLifecycle();
    let readyEmitted = false;
    
    instance.on('ready', () => {
      readyEmitted = true;
    });
    
    await instance.start();
    
    assert.equal(readyEmitted, true);
    assert.equal(instance.isReady(), true);
    
    await instance.stop();
  } finally {
    originalNet.createConnection = originalCreateConnection;
  }
});


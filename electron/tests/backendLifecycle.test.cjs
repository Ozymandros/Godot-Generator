const test = require('node:test');
const assert = require('node:assert/strict');
const EventEmitter = require('node:events');

const lifecycleModule = require('../backendLifecycle.cjs');

function failOnRealSpawn() {
  throw new Error('Test attempted to spawn a real backend process.');
}

function failOnRealConnection() {
  throw new Error('Test attempted a real pipe connection.');
}

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

test.beforeEach(() => {
  lifecycleModule.__setTestDeps({
    spawnFn: failOnRealSpawn,
    netModule: { createConnection: failOnRealConnection },
  });
});

test('resolveBackendCommand returns bundled executable when present', () => {
  const oldResourcesPath = process.resourcesPath;
  process.resourcesPath = 'C:\\fake-resources';

  lifecycleModule.__setTestDeps({
    fsModule: { existsSync: () => true },
  });

  const result = lifecycleModule.resolveBackendCommand();
  assert.equal(result.isBundled, true);
  assert.equal(result.args.length, 0);
  assert.match(result.cmd, /backend/i);

  process.resourcesPath = oldResourcesPath;
});

test('resolveBackendCommand falls back to dotnet run in dev', () => {
  lifecycleModule.__setTestDeps({
    fsModule: { existsSync: () => false },
  });

  const result = lifecycleModule.resolveBackendCommand();
  assert.equal(result.isBundled, false);
  assert.equal(result.cmd, 'dotnet');
  assert.deepEqual(result.args.slice(0, 3), ['run', '-c', 'Release']);
});

test('waitForPipeReady resolves when socket connects', async () => {
  lifecycleModule.__setTestDeps({
    netModule: { createConnection: () => createSocketThatConnects() },
  });

  await assert.doesNotReject(lifecycleModule.waitForPipeReady(100));
});

test('waitForPipeReady rejects on timeout after repeated errors', async () => {
  let tick = 0;
  lifecycleModule.__setTestDeps({
    netModule: { createConnection: () => createSocketThatErrors() },
    nowFn: () => (tick++ === 0 ? 0 : 2),
    setTimeoutFn: (fn) => fn(),
  });

  await assert.rejects(
    lifecycleModule.waitForPipeReady(1),
    /not ready within 1ms/i,
  );
});

test('BackendLifecycle.start reuses already running backend and skips spawn', async () => {
  let spawnCalls = 0;
  lifecycleModule.__setTestDeps({
    netModule: { createConnection: () => createSocketThatConnects() },
    spawnFn: () => {
      spawnCalls += 1;
      return new EventEmitter();
    },
  });

  const instance = new lifecycleModule.BackendLifecycle();
  await instance.start();

  assert.equal(instance.isReady(), true);
  assert.equal(spawnCalls, 0);
});

test('BackendLifecycle emits failed when restart threshold exceeded', async () => {
  lifecycleModule.__setTestDeps({
    nowFn: () => 1000,
  });

  const instance = new lifecycleModule.BackendLifecycle();
  instance._lastRestartAt = 999;
  instance._restartCount = 5;

  let failed = false;
  instance.on('failed', () => {
    failed = true;
  });

  instance._scheduledRestart();
  assert.equal(failed, true);
});


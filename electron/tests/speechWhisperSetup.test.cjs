const test = require('node:test');
const assert = require('node:assert/strict');

const { resolveWhisperOptions, registerWhisperPlugin } = require('../speechWhisperSetup.cjs');

test('resolveWhisperOptions uses whisper.cmd on Windows', () => {
  const options = resolveWhisperOptions({
    platform: 'win32',
    env: {},
    baseDir: 'C:\\repo\\electron',
  });

  assert.equal(options.whisperBin, 'whisper.cmd');
  assert.match(options.modelPath, /models[\\/]+ggml-base\.bin$/);
});

test('resolveWhisperOptions uses whisper on non-Windows', () => {
  const options = resolveWhisperOptions({
    platform: 'linux',
    env: {},
    baseDir: '/repo/electron',
  });

  assert.equal(options.whisperBin, 'whisper');
  assert.match(options.modelPath, /models[\\/]+ggml-base\.bin$/);
});

test('registerWhisperPlugin passes resolved whisperBin to plugin call', () => {
  let captured = null;
  const fakeRegister = (opts) => {
    captured = opts;
    return { options: opts, dispose() {} };
  };

  const result = registerWhisperPlugin(fakeRegister, {
    platform: 'win32',
    env: {},
    baseDir: 'C:\\repo\\electron',
  });

  assert.ok(captured, 'Expected plugin register function to be called');
  assert.equal(captured.whisperBin, 'whisper.cmd');
  assert.equal(result.options.whisperBin, 'whisper.cmd');
});

test('registerWhisperPlugin honors WHISPER_MODEL env override', () => {
  let captured = null;
  const fakeRegister = (opts) => {
    captured = opts;
    return { options: opts, dispose() {} };
  };

  registerWhisperPlugin(fakeRegister, {
    platform: 'linux',
    env: { WHISPER_MODEL: '/tmp/custom-model.bin' },
    baseDir: '/repo/electron',
  });

  assert.equal(captured.whisperBin, 'whisper');
  assert.equal(captured.modelPath, '/tmp/custom-model.bin');
});


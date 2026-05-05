const test = require('node:test');
const assert = require('node:assert/strict');

const {
  readWhisperConfig,
  resolveWhisperBin,
  resolveWhisperOptions,
  registerWhisperPlugin,
} = require('../speechWhisperSetup.cjs');

test('readWhisperConfig returns empty object when file does not exist', () => {
  const config = readWhisperConfig({
    baseDir: 'C:\\repo\\electron',
    env: {},
    fsModule: { existsSync: () => false, readFileSync: () => '' },
  });

  assert.deepEqual(config, {});
});

test('readWhisperConfig parses json config when present', () => {
  const config = readWhisperConfig({
    baseDir: 'C:\\repo\\electron',
    env: {},
    fsModule: {
      existsSync: (p) => p.endsWith('whisper.config.json'),
      readFileSync: () => '{"whisperBinPath":"C:\\\\Tools\\\\whisper\\\\main.exe"}',
    },
  });

  assert.equal(config.whisperBinPath, 'C:\\Tools\\whisper\\main.exe');
});

test('resolveWhisperBin prefers config path over env and empty fallback', () => {
  const whisperBin = resolveWhisperBin({
    env: { WHISPER_BIN: 'D:\\override\\whisper.exe' },
    config: { whisperBinPath: 'C:\\Tools\\whisper\\main.exe' },
  });

  assert.equal(whisperBin, 'C:\\Tools\\whisper\\main.exe');
});

test('resolveWhisperBin falls back to env then empty string', () => {
  const fromEnv = resolveWhisperBin({
    env: { WHISPER_BIN: 'D:\\override\\whisper.exe' },
    config: {},
  });
  const defaultBin = resolveWhisperBin({ env: {}, config: {} });

  assert.equal(fromEnv, 'D:\\override\\whisper.exe');
  assert.equal(defaultBin, '');
});

test('resolveWhisperOptions uses config whisperBin and modelPath', () => {
  const options = resolveWhisperOptions({
    platform: 'win32',
    env: {},
    baseDir: 'C:\\repo\\electron',
    fsModule: {
      existsSync: (p) => p.endsWith('whisper.config.json'),
      readFileSync: () => '{"whisperBinPath":"C:\\\\Tools\\\\whisper\\\\main.exe","modelPath":"C:\\\\Models\\\\ggml-base.bin"}',
    },
  });

  assert.equal(options.whisperBin, 'C:\\Tools\\whisper\\main.exe');
  assert.equal(options.modelPath, 'C:\\Models\\ggml-base.bin');
});

test('resolveWhisperOptions supports WHISPER_MODEL env override', () => {
  const options = resolveWhisperOptions({
    platform: 'linux',
    env: { WHISPER_MODEL: '/tmp/custom-model.bin' },
    baseDir: '/repo/electron',
    fsModule: { existsSync: () => false, readFileSync: () => '' },
  });

  assert.equal(options.whisperBin, '');
  assert.equal(options.modelPath, '/tmp/custom-model.bin');
});

test('registerWhisperPlugin always passes resolved whisperBin to plugin', () => {
  let captured = null;
  const fakeRegister = (opts) => {
    captured = opts;
    return { options: opts, dispose() {} };
  };

  const result = registerWhisperPlugin(fakeRegister, {
    platform: 'win32',
    env: {},
    baseDir: 'C:\\repo\\electron',
    fsModule: {
      existsSync: (p) => p.endsWith('whisper.config.json'),
      readFileSync: () => '{"whisperBinPath":"C:\\\\Tools\\\\whisper\\\\main.exe"}',
    },
  });

  assert.ok(captured, 'Expected plugin register function to be called');
  assert.equal(captured.whisperBin, 'C:\\Tools\\whisper\\main.exe');
  assert.equal(result.options.whisperBin, 'C:\\Tools\\whisper\\main.exe');
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
    fsModule: { existsSync: () => false, readFileSync: () => '' },
  });

  assert.equal(captured.whisperBin, '');
  assert.equal(captured.modelPath, '/tmp/custom-model.bin');
});


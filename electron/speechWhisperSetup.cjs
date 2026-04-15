'use strict';

const path = require('path');
const fs = require('fs');

const DEFAULT_CONFIG_FILE_NAME = 'whisper.config.json';

function readWhisperConfig({ baseDir, env, fsModule = fs } = {}) {
  const configPath = (typeof env?.WHISPER_CONFIG === 'string' && env.WHISPER_CONFIG.trim().length > 0)
    ? env.WHISPER_CONFIG.trim()
    : path.join(baseDir, DEFAULT_CONFIG_FILE_NAME);

  if (!fsModule.existsSync(configPath)) {
    return {};
  }

  try {
    const raw = fsModule.readFileSync(configPath, 'utf8');
    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    // Invalid JSON should not crash startup; plugin defaults still work.
    return {};
  }
}

function resolveWhisperBin({ env, config }) {
  if (typeof config?.whisperBinPath === 'string' && config.whisperBinPath.trim().length > 0) {
    return config.whisperBinPath.trim();
  }

  if (typeof env?.WHISPER_BIN === 'string' && env.WHISPER_BIN.trim().length > 0) {
    return env.WHISPER_BIN.trim();
  }

  // Default command fallback (lets PATH resolve where available).
  return 'whisper';
}

function resolveWhisperOptions({
  platform = process.platform,
  env = process.env,
  baseDir = __dirname,
  fileExists = fs.existsSync, // kept for test signature compatibility
  fsModule = fs,
} = {}) {
  const config = readWhisperConfig({ baseDir, env, fsModule });
  const whisperBin = resolveWhisperBin({ env, config });
  const modelPath = env.WHISPER_MODEL
    || (typeof config?.modelPath === 'string' && config.modelPath.trim().length > 0
      ? config.modelPath.trim()
      : path.join(baseDir, '..', 'models', 'ggml-base.bin'));
  return { whisperBin, modelPath };
}

function registerWhisperPlugin(registerSpeechWhisperMain, optionsInput) {
  const options = resolveWhisperOptions(optionsInput);
  const stt = registerSpeechWhisperMain(options);
  return { stt, options };
}

module.exports = {
  readWhisperConfig,
  resolveWhisperBin,
  resolveWhisperOptions,
  registerWhisperPlugin,
};


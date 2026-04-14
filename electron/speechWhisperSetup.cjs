'use strict';

const path = require('path');

function resolveWhisperOptions({ platform = process.platform, env = process.env, baseDir = __dirname } = {}) {
  const whisperBin = platform === 'win32' ? 'whisper.cmd' : 'whisper';
  const modelPath = env.WHISPER_MODEL || path.join(baseDir, '..', 'models', 'ggml-base.bin');
  return { whisperBin, modelPath };
}

function registerWhisperPlugin(registerSpeechWhisperMain, optionsInput) {
  const options = resolveWhisperOptions(optionsInput);
  const stt = registerSpeechWhisperMain(options);
  return { stt, options };
}

module.exports = {
  resolveWhisperOptions,
  registerWhisperPlugin,
};


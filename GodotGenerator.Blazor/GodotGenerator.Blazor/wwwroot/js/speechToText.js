/**
 * @file speechToText.js
 * `window.godotSpeech` module used only by prompt inputs.
 *
 * Prompt actions (wand/mic buttons) are rendered by Blazor `SmartFieldType.Prompt`.
 * This script only handles speech capture lifecycle and transcript callbacks.
 *
 * Requires: electronBridge.js loaded first (provides window.godotElectronInterop).
 */

/**
 * Blazor-friendly speech-to-text module consumed by PromptInputSection.razor.
 *
 * API
 * ───
 *   godotSpeech.start(dotNetRef)  — Start mic capture. dotNetRef is a
 *                                   DotNetObjectReference<PromptInputSection>;
 *                                   transcript/stop events are forwarded back via
 *                                   invokeMethodAsync('OnSpeechTranscript', text)
 *                                   and invokeMethodAsync('OnSpeechStopped').
 *   godotSpeech.stop()            — Stop capture (triggers Whisper processing).
 *   godotSpeech.isRecording()     — Returns true while capture is active.
 */
(function () {
  'use strict';

  let _recording = false;
  let _sessionId = 0;
  /** @type {object|null} DotNetObjectReference<PromptInputSection> */
  let _dotNetRef = null;
  /** @type {(() => void)|null} */
  let _transcriptUnsub = null;

  /**
   * Sanitizes transcript text (same rules as the legacy helper above).
   * @param {string} text
   * @returns {string}
   */
  function sanitize(text) {
    if (typeof text !== 'string') return '';
    return text
      .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, '')
      .replace(/\r\n|\r/g, '\n')
      .replace(/ {3,}/g, '  ')
      .trim();
  }

  /**
   * Forwards a transcript string to the Blazor component.
   * @param {string} text Raw transcript from Whisper.
   */
  async function dispatchTranscript(text) {
    const clean = sanitize(text);
    if (!clean || !_dotNetRef) return;
    try {
      await _dotNetRef.invokeMethodAsync('OnSpeechTranscript', clean);
    } catch (err) {
      console.warn('[godotSpeech] Failed to dispatch transcript:', err);
    }
  }

  /**
   * Notifies the Blazor component that recording has stopped and clears the ref.
   */
  async function dispatchStopped() {
    if (!_dotNetRef) return;
    try {
      await _dotNetRef.invokeMethodAsync('OnSpeechStopped');
    } catch (err) {
      console.warn('[godotSpeech] Failed to dispatch stopped:', err);
    }
    _dotNetRef = null;
  }

  window.godotSpeech = {

    /**
     * Start speech capture.
     * @param {object} dotNetRef DotNetObjectReference<PromptInputSection> created by Blazor.
     * @throws {Error} When speech is unavailable (non-Electron or missing Whisper plugin).
     */
    start: async function (dotNetRef) {
      if (_recording) {
        console.warn('[godotSpeech] Already recording — call stop() first.');
        return;
      }
      if (!window.godotElectronInterop || !window.godotElectronInterop.hasSpeech()) {
        throw new Error('Speech-to-text is only available in the Electron desktop app.');
      }

      _dotNetRef = dotNetRef;
      _recording = true;
      _sessionId += 1;
      const sessionId = _sessionId;
      console.info(`[godotSpeech] start() session=${sessionId}`);

      // Subscribe before starting so no transcript events are missed.
      _transcriptUnsub = window.godotElectronInterop.onSpeechTranscript(async (text) => {
        await dispatchTranscript(text);
        // Whisper fires once per session; stop automatically after delivery.
        console.info(`[godotSpeech] transcript received; auto-stop session=${sessionId}`);
        await window.godotSpeech.stop();
      });

      try {
        await window.godotElectronInterop.startSpeech();
        try {
          const postStartStatus = await window.godotElectronInterop.getSpeechStatus();
          void postStartStatus;
        } catch (statusErr) {
          void statusErr;
        }
        console.info(`[godotSpeech] start() completed session=${sessionId}`);
      } catch (err) {
        _recording = false;
        if (_transcriptUnsub) { _transcriptUnsub(); _transcriptUnsub = null; }
        await dispatchStopped();
        console.warn(`[godotSpeech] start() failed session=${sessionId}:`, err);
        throw err;
      }
    },

    /**
     * Stop speech capture and wait for Whisper to process the audio.
     * Safe to call when not recording (no-op).
     */
    stop: async function () {
      try {
        if (window.godotElectronInterop?.getSpeechStatus) {
          const preStopStatus = await window.godotElectronInterop.getSpeechStatus();
          void preStopStatus;
        }
      } catch (statusErr) {
        void statusErr;
      }
      if (!_recording) {
        console.info('[godotSpeech] stop() ignored: no active local recording flag.');
        return;
      }
      _recording = false;

      try {
        if (window.godotElectronInterop && window.godotElectronInterop.hasSpeech()) {
          console.info('[godotSpeech] stop() -> stopSpeech()');
          await window.godotElectronInterop.stopSpeech();
        }
      } catch (err) {
        console.warn('[godotSpeech] stopSpeech error:', err);
      } finally {
        if (_transcriptUnsub) {
          // Keep transcript subscription active until stopSpeech completes,
          // so manual stop does not miss the final Whisper result event.
          _transcriptUnsub();
          _transcriptUnsub = null;
        }
        console.info('[godotSpeech] stop() completed.');
        await dispatchStopped();
      }
    },

    /**
     * Returns whether capture is currently active.
     * @returns {boolean}
     */
    isRecording: function () {
      return _recording;
    },
  };

})();

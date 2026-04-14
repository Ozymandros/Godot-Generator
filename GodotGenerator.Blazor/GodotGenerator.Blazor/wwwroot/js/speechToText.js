/**
 * @file speechToText.js
 * Exposes `window.godotSpeech` for use by Blazor components via IJSRuntime.
 *
 * API
 * ───
 *   godotSpeech.start(dotNetRef)  — Start mic capture. dotNetRef must be a
 *                                   DotNetObjectReference<PromptInputSection>;
 *                                   transcript/stop events are forwarded via
 *                                   invokeMethodAsync.
 *   godotSpeech.stop()            — Stop capture and trigger Whisper processing.
 *   godotSpeech.isRecording()     — Returns true while capture is active.
 *
 * Note: the old DOM-injection approach (attaching a wand/mic button to every
 * textarea imperatively) has been removed. Buttons are now Blazor Fluent UI
 * components rendered inside PromptInputSection.razor.
 *
 * Requires: electronBridge.js loaded first (provides window.godotElectronInterop).
 */

(function () {
  'use strict';

  let _recording = false;
  /** @type {import('@microsoft/dotnet-runtime').DotNetObject|null} */
  let _dotNetRef = null;
  /** @type {(() => void)|null} */
  let _transcriptUnsub = null;

  /**
   * Sanitizes transcript text for safe insertion.
   * Removes C0/C1 control characters (except newline/tab), normalises CRLF,
   * and collapses runs of 3+ spaces.
   * @param {string} text
   * @returns {string}
   */
  function sanitize(text) {
    if (typeof text \!== 'string') return '';
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
    if (\!clean || \!_dotNetRef) return;
    try {
      await _dotNetRef.invokeMethodAsync('OnSpeechTranscript', clean);
    } catch (err) {
      console.warn('[godotSpeech] Failed to dispatch transcript:', err);
    }
  }

  /**
   * Notifies the Blazor component that recording has stopped.
   */
  async function dispatchStopped() {
    if (\!_dotNetRef) return;
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
     * @param {object} dotNetRef DotNetObjectReference<PromptInputSection> from Blazor.
     * @throws {Error} When speech is unavailable (non-Electron or missing plugin).
     */
    start: async function (dotNetRef) {
      if (_recording) {
        console.warn('[godotSpeech] Already recording — stop first.');
        return;
      }
      if (\!window.godotElectronInterop || \!window.godotElectronInterop.hasSpeech()) {
        throw new Error('Speech-to-text is only available in the Electron desktop app.');
      }

      _dotNetRef = dotNetRef;
      _recording = true;

      // Subscribe before starting so no transcripts are missed.
      _transcriptUnsub = window.godotElectronInterop.onSpeechTranscript(async (text) => {
        await dispatchTranscript(text);
        // Whisper fires once per session; clean up after delivery.
        await window.godotSpeech.stop();
      });

      try {
        await window.godotElectronInterop.startSpeech();
      } catch (err) {
        _recording = false;
        if (_transcriptUnsub) { _transcriptUnsub(); _transcriptUnsub = null; }
        await dispatchStopped();
        throw err;
      }
    },

    /**
     * Stop speech capture. Safe to call when not recording (no-op).
     */
    stop: async function () {
      if (\!_recording) return;
      _recording = false;

      if (_transcriptUnsub) {
        _transcriptUnsub();
        _transcriptUnsub = null;
      }

      try {
        if (window.godotElectronInterop && window.godotElectronInterop.hasSpeech()) {
          await window.godotElectronInterop.stopSpeech();
        }
      } catch (err) {
        console.warn('[godotSpeech] stopSpeech error:', err);
      } finally {
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

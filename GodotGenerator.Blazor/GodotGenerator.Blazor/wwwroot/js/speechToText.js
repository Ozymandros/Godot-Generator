/**
 * @file speechToText.js
 * Two responsibilities:
 *
 * 1. DOM injection (legacy, kept for non-PromptInputSection textareas such as the log
 *    viewer and settings prompts panels): attaches a 🪄 wand button that starts/stops
 *    Electron Whisper speech capture and inserts transcribed text at cursor.
 *    Skips textareas already inside `.prompt-input-wrap` — those are handled by
 *    the Blazor PromptInputSection component's own mic button.
 *
 * 2. `window.godotSpeech` module: a Blazor-friendly API consumed by
 *    PromptInputSection.razor via IJSRuntime. Forwards transcript and stop
 *    events back to the component via DotNetObjectReference invokeMethodAsync.
 *
 * Requires: electronBridge.js loaded first (provides window.godotElectronInterop).
 */

/* ── Part 1: Legacy DOM-injection wand button ─────────────────────────────── */

(function () {
  'use strict';

  const WAND_ICON = '🪄';
  const WAND_ACTIVE_ICON = '🎙️';

  let isRecording = false;
  let transcriptUnsub = null;
  let activeTextarea = null;

  /**
   * Sanitizes transcript text for safe insertion into a textarea.
   * Removes control characters except newlines/tabs, trims excessive whitespace.
   * @param {string} text
   * @returns {string}
   */
  function sanitizeText(text) {
    if (typeof text !== 'string') return '';
    // Remove control chars except \n, \r, \t
    let sanitized = text.replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, '');
    // Normalize line endings
    sanitized = sanitized.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    // Trim excessive whitespace (3+ spaces → 2 spaces)
    sanitized = sanitized.replace(/ {3,}/g, '  ');
    return sanitized.trim();
  }

  /**
   * Inserts text at the current cursor position in a textarea.
   * @param {HTMLTextAreaElement} textarea
   * @param {string} text
   */
  function insertAtCursor(textarea, text) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const value = textarea.value;
    textarea.value = value.substring(0, start) + text + value.substring(end);
    textarea.selectionStart = textarea.selectionEnd = start + text.length;
    // Trigger input event for Blazor binding
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    textarea.dispatchEvent(new Event('change', { bubbles: true }));
  }

  /**
   * Initializes wand buttons on all matching textareas.
   */
  function init() {
  }

  init();
})();


/* ── Part 2: window.godotSpeech — Blazor IJSRuntime API ──────────────────── */

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

      // Subscribe before starting so no transcript events are missed.
      _transcriptUnsub = window.godotElectronInterop.onSpeechTranscript(async (text) => {
        await dispatchTranscript(text);
        // Whisper fires once per session; stop automatically after delivery.
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
     * Stop speech capture and wait for Whisper to process the audio.
     * Safe to call when not recording (no-op).
     */
    stop: async function () {
      if (!_recording) return;
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

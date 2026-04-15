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
  const BUTTON_CLASS = 'stt-wand-btn';

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
   * Creates or updates the wand button for a textarea.
   * Skips textareas that live inside `.prompt-input-wrap` — those already
   * have the Blazor-rendered wand + mic buttons from PromptInputSection.
   * @param {Element} textarea
   */
  function attachWandButton(textarea) {
    // Skip if this textarea is already managed by the Blazor PromptInputSection component.
    if (textarea.closest('.prompt-input-wrap')) return;

    // Skip if already has a button
    if (textarea.parentElement?.querySelector(`.${BUTTON_CLASS}`)) return;

    const container = document.createElement('div');
    container.style.position = 'relative';
    container.style.display = 'inline-block';
    container.style.width = '100%';

    // Wrap textarea in container
    textarea.parentNode.insertBefore(container, textarea);
    container.appendChild(textarea);

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = BUTTON_CLASS;
    btn.textContent = WAND_ICON;
    btn.title = 'Speech to Text (click to start/stop recording)';
    btn.style.position = 'absolute';
    btn.style.right = '8px';
    btn.style.bottom = '8px';
    btn.style.zIndex = '10';
    btn.style.padding = '4px 8px';
    btn.style.border = '1px solid #ccc';
    btn.style.borderRadius = '4px';
    btn.style.background = '#fff';
    btn.style.cursor = 'pointer';
    btn.style.fontSize = '14px';
    btn.style.opacity = '0.8';
    btn.style.transition = 'opacity 0.2s';

    btn.addEventListener('mouseenter', () => btn.style.opacity = '1');
    btn.addEventListener('mouseleave', () => btn.style.opacity = '0.8');

    btn.addEventListener('click', async () => {
      if (!window.godotElectronInterop || !window.godotElectronInterop.hasSpeech()) {
        alert('Speech-to-text is only available in the Electron desktop app.');
        return;
      }

      try {
        if (isRecording) {
          // Stop recording
          btn.textContent = WAND_ICON;
          btn.title = 'Speech to Text (click to start/stop recording)';
          isRecording = false;
          await window.godotElectronInterop.stopSpeech();
          if (transcriptUnsub) {
            transcriptUnsub();
            transcriptUnsub = null;
          }
          activeTextarea = null;
        } else {
          // Start recording
          activeTextarea = textarea;
          btn.textContent = WAND_ACTIVE_ICON;
          btn.title = 'Recording... Click to stop';
          isRecording = true;

          // Subscribe to transcript results
          transcriptUnsub = window.godotElectronInterop.onSpeechTranscript((text) => {
            const sanitized = sanitizeText(text);
            if (sanitized && activeTextarea) {
              insertAtCursor(activeTextarea, sanitized);
            }
          });

          await window.godotElectronInterop.startSpeech();
        }
      } catch (err) {
        console.error('[speechToText] Error:', err);
        btn.textContent = WAND_ICON;
        btn.title = 'Speech to Text (error occurred)';
        isRecording = false;
        if (transcriptUnsub) {
          transcriptUnsub();
          transcriptUnsub = null;
        }
        activeTextarea = null;
        alert(`Speech-to-text error: ${err.message}`);
      }
    });

    container.appendChild(btn);
  }

  /**
   * Initializes wand buttons on all matching textareas.
   */
  function init() {
    // 1. Attach to any fluent-text-area elements already in the DOM.
    document.querySelectorAll('fluent-text-area').forEach(attachWandButton);

    // 2. Observe future additions from Blazor re-renders.
    const observer = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        for (const node of mutation.addedNodes) {
          if (node.nodeType === Node.ELEMENT_NODE) {
            const fluents = node.tagName?.toLowerCase() === 'fluent-text-area'
              ? [node]
              : node.querySelectorAll('fluent-text-area');

            fluents.forEach(el => {
              // Allow time for the Fluent web component shadow root to mount.
              setTimeout(() => attachWandButton(el), 100);
            });
          }
        }
      }
    });

    observer.observe(document.body, { childList: true, subtree: true });
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

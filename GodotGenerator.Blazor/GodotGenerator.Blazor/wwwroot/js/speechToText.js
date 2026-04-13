/**
 * @file speechToText.js
 * Attaches a speech-to-text button (🪄 wand icon) to all prompt textareas.
 * When clicked, starts/stops microphone capture and inserts the transcribed
 * text (sanitized) into the textarea at the current cursor position.
 *
 * Requires: electronBridge.js loaded first (provides window.godotElectronInterop).
 */

(function () {
  'use strict';

  const WAND_ICON = '🪄';
  const WAND_ACTIVE_ICON = '🎙️';
  const BUTTON_CLASS = 'stt-wand-btn';
  const TEXTAREA_SELECTOR = 'textarea.app-input-like--textarea, textarea.generation-workspace__result-textarea, textarea.app-textarea-log, textarea.settings-prompts__textarea';

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
   * @param {HTMLTextAreaElement} textarea
   */
  function attachWandButton(textarea) {
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
    // Wait for DOM ready
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', init);
      return;
    }

    // Attach to existing textareas
    document.querySelectorAll(TEXTAREA_SELECTOR).forEach(attachWandButton);

    // Observe DOM for dynamically added textareas (Blazor navigation)
    const observer = new MutationObserver((mutations) => {
      mutations.forEach((mutation) => {
        mutation.addedNodes.forEach((node) => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            if (node.matches && node.matches(TEXTAREA_SELECTOR)) {
              attachWandButton(node);
            }
            node.querySelectorAll(TEXTAREA_SELECTOR).forEach(attachWandButton);
          }
        });
      });
    });

    observer.observe(document.body, { childList: true, subtree: true });
  }

  init();
})();

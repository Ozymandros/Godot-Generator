// Theme management module - handles Light/Dark/System theme selection
// Must be loaded early to prevent flash of incorrect theme

(function () {
  'use strict';

  const STORAGE_KEY = 'godot-generator-theme-preference';
  const THEME_ATTRIBUTE = 'data-theme';
  const HTML_ELEMENT = document.documentElement;

  /**
   * Get the system's preferred color scheme
   * @returns {'light' | 'dark'}
   */
  function getSystemPreference() {
    if (typeof window.matchMedia === 'function') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return 'light';
  }

  /**
   * Apply the effective theme based on user preference
   * @param {'light' | 'dark' | 'system'} preference - User's stored preference
   */
  function applyTheme(preference) {
    let effectiveTheme;

    if (preference === 'system') {
      effectiveTheme = getSystemPreference();
    } else {
      effectiveTheme = preference;
    }

    HTML_ELEMENT.setAttribute(THEME_ATTRIBUTE, effectiveTheme);

    // Also sync with Fluent UI design theme if available
    if (window.fluentDesignTheme) {
      try {
        window.fluentDesignTheme.setMode(effectiveTheme === 'dark' ? 'dark' : 'light');
      } catch {
        // Fluent theme API may not be available yet
      }
    }
  }

  /**
   * Initialize theme on page load
   * Called immediately to prevent flash of incorrect theme
   */
  function initializeTheme() {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      const preference = stored || 'system';
      applyTheme(preference);
      
      // Listen for system theme changes when in system mode
      if (typeof window.matchMedia === 'function') {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
          const currentPreference = localStorage.getItem(STORAGE_KEY) || 'system';
          if (currentPreference === 'system') {
            applyTheme('system');
          }
        });
      }
    } catch {
      // localStorage unavailable or disabled; use system default
      applyTheme(getSystemPreference());
    }
  }

  // Expose public API
  window.godotTheme = {
    /**
     * Set the user's theme preference
     * @param {'light' | 'dark' | 'system'} preference
     */
    setPreference: function (preference) {
      try {
        localStorage.setItem(STORAGE_KEY, preference);
        applyTheme(preference);
      } catch {
        // localStorage unavailable
      }
    },

    /**
     * Get the current user preference
     * @returns {'light' | 'dark' | 'system'}
     */
    getPreference: function () {
      try {
        return localStorage.getItem(STORAGE_KEY) || 'system';
      } catch {
        return 'system';
      }
    },

    /**
     * Get the currently active (effective) theme
     * @returns {'light' | 'dark'}
     */
    getActiveTheme: function () {
      return HTML_ELEMENT.getAttribute(THEME_ATTRIBUTE) || getSystemPreference();
    },

    /**
     * Legacy method for backwards compatibility
     * @deprecated Use setPreference instead
     */
    setDark: function (isDark) {
      this.setPreference(isDark ? 'dark' : 'light');
    },

    /**
     * Initialize theme - call this on page load
     */
    init: initializeTheme
  };

  // Auto-initialize if script is loaded synchronously in head
  // For Blazor, we typically call init() explicitly after DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeTheme);
  } else {
    initializeTheme();
  }
})();

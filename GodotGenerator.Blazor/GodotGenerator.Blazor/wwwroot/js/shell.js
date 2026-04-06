/* Shell: mobile nav drawer + backdrop. Toggle class app-shell--nav-open on documentElement. */
(function () {
  var NAV_OPEN = 'app-shell--nav-open';

  function syncToggleButton(open) {
    var btn = document.querySelector('[data-app-shell-nav-toggle]');
    if (!btn) {
      return;
    }
    btn.setAttribute('aria-expanded', open ? 'true' : 'false');
    btn.setAttribute('aria-label', open ? 'Close navigation menu' : 'Open navigation menu');
  }

  function setNavOpen(open) {
    var root = document.documentElement;
    if (open) {
      root.classList.add(NAV_OPEN);
    } else {
      root.classList.remove(NAV_OPEN);
    }
    syncToggleButton(open);
  }

  window.godotShell = {
    toggleNav: function () {
      setNavOpen(!document.documentElement.classList.contains(NAV_OPEN));
    },
    setNavOpen: setNavOpen,
    closeNav: function () {
      setNavOpen(false);
    },
  };

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
      window.godotShell.closeNav();
    }
  });

  document.addEventListener('DOMContentLoaded', function () {
    var backdrop = document.querySelector('[data-app-shell-backdrop]');
    if (backdrop) {
      backdrop.addEventListener('click', function () {
        window.godotShell.closeNav();
      });
    }
    syncToggleButton(document.documentElement.classList.contains(NAV_OPEN));
  });
})();

/** Clipboard helper for Blazor (system prompts copy buttons). */
window.godotClipboard = {
  copy: function (text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      return navigator.clipboard.writeText(text);
    }
    return Promise.reject(new Error('Clipboard API unavailable'));
  },
};

window.godotTheme = {
  setDark: function (dark) {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }
};

window.godotGeneratorStorage = {
  getItem: function (key) {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  setItem: function (key, value) {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* ignore */
    }
  }
};

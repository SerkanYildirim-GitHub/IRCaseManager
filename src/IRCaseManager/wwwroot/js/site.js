// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(function () {
  const storageKey = "ir-case-manager-theme";
  const root = document.documentElement;
  const toggle = document.querySelector("[data-theme-toggle]");
  const label = document.querySelector("[data-theme-label]");

  function applyTheme(theme) {
    root.dataset.theme = theme;
    if (toggle) {
      const isDark = theme === "dark";
      toggle.setAttribute("aria-pressed", String(isDark));
      toggle.setAttribute("aria-label", isDark ? "Switch to light mode" : "Switch to dark mode");
      if (label) {
        label.textContent = isDark ? "Dark" : "Light";
      }
    }
  }

  const savedTheme = localStorage.getItem(storageKey);
  const preferredTheme = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  applyTheme(savedTheme || preferredTheme);

  if (toggle) {
    toggle.addEventListener("click", function () {
      const nextTheme = root.dataset.theme === "dark" ? "light" : "dark";
      localStorage.setItem(storageKey, nextTheme);
      applyTheme(nextTheme);
    });
  }
})();

(function () {
  const table = document.querySelector("[data-case-filter-table]");
  if (!table) {
    return;
  }

  const filters = Array.from(table.querySelectorAll("[data-case-filter]"));
  const rows = Array.from(table.querySelectorAll("[data-case-row]"));
  const shells = Array.from(table.querySelectorAll("[data-case-filter-shell]"));
  const emptyState = document.querySelector("[data-case-filter-empty]");

  function normalize(value) {
    return (value || "").trim().toLowerCase();
  }

  function toDatasetKey(key) {
    return "filter" + key.replace(/(^|-)([a-z])/g, function (_, _separator, letter) {
      return letter.toUpperCase();
    });
  }

  function closeMenus(exceptShell) {
    shells.forEach(function (shell) {
      if (shell === exceptShell) {
        return;
      }

      const button = shell.querySelector("[data-case-filter-toggle]");
      const menu = shell.querySelector("[data-case-filter-menu]");
      shell.classList.remove("is-open");
      if (button) {
        button.setAttribute("aria-expanded", "false");
      }
      if (menu) {
        menu.hidden = true;
      }
    });
  }

  function updateActiveHeaders() {
    shells.forEach(function (shell) {
      const filter = shell.querySelector("[data-case-filter]");
      const button = shell.querySelector("[data-case-filter-toggle]");
      const isActive = filter && normalize(filter.value).length > 0;
      shell.classList.toggle("is-active", Boolean(isActive));
      if (button) {
        button.classList.toggle("is-active", Boolean(isActive));
      }
    });
  }

  function applyFilters() {
    const activeFilters = filters
      .map(function (filter) {
        return {
          key: filter.dataset.caseFilter,
          value: normalize(filter.value)
        };
      })
      .filter(function (filter) {
        return filter.value.length > 0;
      });

    let visibleCount = 0;

    rows.forEach(function (row) {
      const isVisible = activeFilters.every(function (filter) {
        return normalize(row.dataset[toDatasetKey(filter.key)]).includes(filter.value);
      });

      row.hidden = !isVisible;
      if (isVisible) {
        visibleCount += 1;
      }
    });

    if (emptyState) {
      emptyState.hidden = visibleCount > 0;
    }

    updateActiveHeaders();
  }

  shells.forEach(function (shell) {
    const button = shell.querySelector("[data-case-filter-toggle]");
    const menu = shell.querySelector("[data-case-filter-menu]");
    const field = shell.querySelector("[data-case-filter]");

    if (!button || !menu) {
      return;
    }

    button.addEventListener("click", function () {
      const isOpening = menu.hidden;
      closeMenus(shell);
      shell.classList.toggle("is-open", isOpening);
      menu.hidden = !isOpening;
      button.setAttribute("aria-expanded", String(isOpening));

      if (isOpening && field) {
        field.focus();
      }
    });
  });

  filters.forEach(function (filter) {
    filter.addEventListener("input", applyFilters);
    filter.addEventListener("change", applyFilters);
  });

  document.addEventListener("click", function (event) {
    if (!table.contains(event.target)) {
      closeMenus();
      return;
    }

    if (!event.target.closest("[data-case-filter-shell]")) {
      closeMenus();
    }
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
      closeMenus();
    }
  });
})();

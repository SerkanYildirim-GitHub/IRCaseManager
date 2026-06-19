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

  if (table.dataset.caseFiltersReady === "true") {
    return;
  }
  table.dataset.caseFiltersReady = "true";

  const emptyState = document.querySelector("[data-case-filter-empty]");
  const filterKeys = ["date", "case-id", "title", "type", "severity", "assigned-to", "team", "status"];

  function normalize(value) {
    return (value || "").trim().toLowerCase();
  }

  function toDatasetKey(key) {
    return "filter" + key.replace(/(^|-)([a-z])/g, function (_, _separator, letter) {
      return letter.toUpperCase();
    });
  }

  const filterControls = Array.from(table.querySelectorAll("[data-case-filter]")).map(function (field) {
    return {
      field: field,
      key: field.dataset.caseFilter
    };
  });

  const shellControls = Array.from(table.querySelectorAll("[data-case-filter-shell]")).map(function (shell) {
    return {
      shell: shell,
      button: shell.querySelector("[data-case-filter-toggle]"),
      menu: shell.querySelector("[data-case-filter-menu]"),
      field: shell.querySelector("[data-case-filter]")
    };
  });

  const rowCache = Array.from(table.querySelectorAll("[data-case-row]")).map(function (row) {
    const values = {};
    filterKeys.forEach(function (key) {
      values[key] = normalize(row.dataset[toDatasetKey(key)]);
    });

    return {
      row: row,
      values: values
    };
  });

  let openShell = null;

  function setShellOpen(control, isOpen) {
    control.shell.classList.toggle("is-open", isOpen);
    control.menu.hidden = !isOpen;
    control.button.setAttribute("aria-expanded", String(isOpen));
    openShell = isOpen ? control : openShell === control ? null : openShell;
  }

  function closeOpenMenu() {
    if (openShell) {
      setShellOpen(openShell, false);
    }
  }

  function updateActiveHeaders() {
    shellControls.forEach(function (control) {
      if (!control.button || !control.field) {
        return;
      }

      const isActive = normalize(control.field.value).length > 0;
      control.shell.classList.toggle("is-active", isActive);
      control.button.classList.toggle("is-active", isActive);
    });
  }

  function applyFilters() {
    const activeFilters = filterControls
      .map(function (control) {
        return {
          key: control.key,
          value: normalize(control.field.value)
        };
      })
      .filter(function (filter) {
        return filter.value.length > 0;
      });

    let visibleCount = 0;

    // Compute visibility first and only write DOM when visibility actually changes.
    rowCache.forEach(function (record) {
      const isVisible = activeFilters.every(function (filter) {
        return record.values[filter.key].includes(filter.value);
      });

      const shouldBeHidden = !isVisible;
      if (record.row.hidden !== shouldBeHidden) {
        record.row.hidden = shouldBeHidden;
      }

      if (isVisible) {
        visibleCount += 1;
      }
    });

    if (emptyState) {
      emptyState.hidden = visibleCount > 0;
    }

    updateActiveHeaders();
  }

  shellControls.forEach(function (control) {
    if (!control.button || !control.menu) {
      return;
    }

    control.button.addEventListener("click", function (event) {
      event.stopPropagation();
      const isOpening = control.menu.hidden;

      if (openShell && openShell !== control) {
        closeOpenMenu();
      }

      setShellOpen(control, isOpening);

      if (isOpening && control.field) {
        // Delay focus slightly to avoid blocking the click path on some browsers/devices.
        setTimeout(function () {
          try {
            control.field.focus({ preventScroll: true });
          } catch (e) {
            control.field.focus();
          }
        }, 0);
      }
    });

    control.menu.addEventListener("click", function (event) {
      event.stopPropagation();
    });
  });

  filterControls.forEach(function (control) {
    // Debounce free-text inputs to avoid running expensive filtering on every keystroke.
    var tag = (control.field && control.field.tagName) ? control.field.tagName.toUpperCase() : "";
    if (tag === "SELECT") {
      control.field.addEventListener("change", applyFilters);
    } else {
      var debounceTimer = null;
      control.field.addEventListener("input", function () {
        if (debounceTimer) {
          clearTimeout(debounceTimer);
        }
        debounceTimer = setTimeout(function () {
          applyFilters();
        }, 150);
      });
      // Keep change immediate for inputs that may not fire input events (accessibility devices)
      control.field.addEventListener("change", applyFilters);
    }
  });

  document.addEventListener("pointerdown", function (event) {
    if (!table.contains(event.target)) {
      closeOpenMenu();
      return;
    }

    if (!event.target.closest("[data-case-filter-shell]")) {
      closeOpenMenu();
    }
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
      closeOpenMenu();
    }
  });
})();

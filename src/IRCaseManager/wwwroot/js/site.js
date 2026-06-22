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
  const dropdowns = document.querySelectorAll(".nav-dropdown");
  dropdowns.forEach(function (dropdown) {
    dropdown.addEventListener("mouseenter", function () {
      dropdown.open = true;
    });

    dropdown.addEventListener("mouseleave", function () {
      dropdown.open = false;
    });
  });
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

  const filterRoot = table.closest(".table-wrap") || document;
  const emptyState = document.querySelector("[data-case-filter-empty]");
  const filterKeys = ["date", "type", "severity", "assigned-to", "queue", "status"];

  function normalize(value) {
    return (value || "").trim().toLowerCase();
  }

  function toDatasetKey(key) {
    return "filter" + key.replace(/(^|-)([a-z])/g, function (_, _separator, letter) {
      return letter.toUpperCase();
    });
  }

  const filterControls = Array.from(filterRoot.querySelectorAll("[data-case-filter]")).map(function (field) {
    return {
      field: field,
      key: field.dataset.caseFilter
    };
  });

  const clearAllButton = filterRoot.querySelector("[data-case-filter-clear-all]");
  const filterCount = filterRoot.querySelector("[data-case-filter-count]");

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
        return record.values[filter.key] === filter.value;
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

    if (filterCount) {
      filterCount.textContent = "Showing " + visibleCount + " of " + rowCache.length + " cases";
    }

  }

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

  if (clearAllButton) {
    clearAllButton.addEventListener("click", function () {
      filterControls.forEach(function (control) {
        control.field.value = "";
      });
      applyFilters();
    });
  }

})();

(function () {
  const form = document.querySelector("[data-analytics-range-form]");
  if (!form) {
    return;
  }

  const rangeSelect = form.querySelector("[data-analytics-range-select]");
  const customFields = Array.from(form.querySelectorAll("[data-analytics-custom-date-field]"));
  if (!rangeSelect || customFields.length === 0) {
    return;
  }

  function syncCustomDateFields() {
    const isCustomRange = rangeSelect.value === "custom";
    customFields.forEach(function (field) {
      field.hidden = !isCustomRange;
      field.querySelectorAll("input").forEach(function (input) {
        input.disabled = !isCustomRange;
      });
    });
  }

  rangeSelect.addEventListener("change", syncCustomDateFields);
  syncCustomDateFields();
})();

(function () {
  const modal = document.querySelector("[data-analytics-chart-modal]");
  if (!modal) {
    return;
  }

  const modalDialog = modal.querySelector(".analytics-modal-dialog");
  const modalTitle = modal.querySelector("[data-analytics-modal-title]");
  const modalDescription = modal.querySelector("[data-analytics-modal-description]");
  const modalContent = modal.querySelector("[data-analytics-modal-content]");
  let lastFocusedElement = null;

  function getSegmentElements(scope, key) {
    return Array.from(scope.querySelectorAll("[data-segment-key]")).filter(function (element) {
      return element.dataset.segmentKey === key;
    });
  }

  function setSegmentState(element, className, enabled) {
    const scope = element.closest("[data-analytics-chart-scope]");
    const key = element.dataset.segmentKey;
    if (!scope || !key) {
      return;
    }

    getSegmentElements(scope, key).forEach(function (match) {
      match.classList.toggle(className, enabled);
    });
  }

  function toggleSegmentSelection(element) {
    const scope = element.closest("[data-analytics-chart-scope]");
    const key = element.dataset.segmentKey;
    if (!scope || !key) {
      return;
    }

    const wasSelected = element.classList.contains("is-selected");
    Array.from(scope.querySelectorAll("[data-segment-key].is-selected")).forEach(function (match) {
      match.classList.remove("is-selected");
    });

    if (!wasSelected) {
      getSegmentElements(scope, key).forEach(function (match) {
        match.classList.add("is-selected");
      });
    }
  }

  document.addEventListener("mouseover", function (event) {
    const segment = event.target.closest("[data-segment-key]");
    if (segment) {
      setSegmentState(segment, "is-hovered", true);
    }
  });

  document.addEventListener("mouseout", function (event) {
    const segment = event.target.closest("[data-segment-key]");
    if (segment) {
      setSegmentState(segment, "is-hovered", false);
    }
  });

  document.addEventListener("focusin", function (event) {
    const segment = event.target.closest("[data-segment-key]");
    if (segment) {
      setSegmentState(segment, "is-hovered", true);
    }
  });

  document.addEventListener("focusout", function (event) {
    const segment = event.target.closest("[data-segment-key]");
    if (segment) {
      setSegmentState(segment, "is-hovered", false);
    }
  });

  document.addEventListener("click", function (event) {
    const segment = event.target.closest("[data-segment-key]");
    if (segment) {
      event.preventDefault();
      toggleSegmentSelection(segment);
    }
  });

  document.addEventListener("keydown", function (event) {
    const segment = event.target.closest("[data-segment-key]");
    if (segment && (event.key === "Enter" || event.key === " ")) {
      event.preventDefault();
      toggleSegmentSelection(segment);
      return;
    }

    if (event.key === "Escape" && !modal.hidden) {
      closeModal();
    }
  });

  function openModal(card) {
    const title = card.querySelector(".analytics-panel-header h2")?.textContent || "Chart";
    const description = card.querySelector(".analytics-panel-header .muted")?.textContent || "";
    const source = card.querySelector("[data-analytics-card-content]");
    if (!source || !modalContent || !modalTitle || !modalDescription) {
      return;
    }

    lastFocusedElement = document.activeElement;
    modalTitle.textContent = title;
    modalDescription.textContent = description;
    modalContent.replaceChildren(source.cloneNode(true));
    modal.hidden = false;
    document.body.classList.add("has-analytics-modal");

    const closeButton = modal.querySelector("[data-analytics-modal-close]");
    if (closeButton) {
      closeButton.focus();
    } else if (modalDialog) {
      modalDialog.focus();
    }
  }

  function closeModal() {
    if (modal.hidden) {
      return;
    }

    modal.hidden = true;
    document.body.classList.remove("has-analytics-modal");
    if (modalContent) {
      modalContent.replaceChildren();
    }

    if (lastFocusedElement && typeof lastFocusedElement.focus === "function") {
      lastFocusedElement.focus();
    }
  }

  document.querySelectorAll("[data-analytics-expand-chart]").forEach(function (button) {
    button.addEventListener("click", function () {
      const card = button.closest("[data-analytics-chart-card]");
      if (card) {
        openModal(card);
      }
    });
  });

  modal.querySelectorAll("[data-analytics-modal-close]").forEach(function (element) {
    element.addEventListener("click", closeModal);
  });
})();

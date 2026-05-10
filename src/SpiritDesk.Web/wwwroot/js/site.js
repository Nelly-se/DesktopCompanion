(() => {
  const clock = document.getElementById("liveClock");
  if (!clock) {
    return;
  }

  const updateClock = () => {
    const now = new Date();
    const hours = String(now.getHours()).padStart(2, "0");
    const minutes = String(now.getMinutes()).padStart(2, "0");
    clock.textContent = `${hours}:${minutes}`;
  };

  updateClock();
  window.setInterval(updateClock, 1000);
})();

(() => {
  const tabs = Array.from(document.querySelectorAll("[data-task-tab]"));
  const panels = Array.from(document.querySelectorAll("[data-task-panel]"));

  if (tabs.length === 0 || panels.length === 0) {
    return;
  }

  const activateTab = (target) => {
    tabs.forEach((tab) => {
      tab.classList.toggle("active", tab.dataset.taskTab === target);
    });

    panels.forEach((panel) => {
      panel.classList.toggle("active", panel.dataset.taskPanel === target);
    });
  };

  tabs.forEach((tab) => {
    tab.addEventListener("click", () => {
      activateTab(tab.dataset.taskTab);
    });
  });
})();

(() => {
  const modal = document.getElementById("gameModal");
  if (!modal) {
    return;
  }

  const setOpen = (open) => {
    modal.classList.toggle("is-open", open);
    modal.setAttribute("aria-hidden", open ? "false" : "true");
  };

  modal.querySelectorAll("[data-close-game-modal]").forEach((el) => {
    el.addEventListener("click", () => setOpen(false));
  });

  document.querySelectorAll(".game-panel-open").forEach((btn) => {
    btn.addEventListener("click", () => setOpen(true));
  });

  window.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && modal.classList.contains("is-open")) {
      setOpen(false);
    }
  });
})();

(() => {
  const storageKey = "spiritdesk.sidebarCollapsed";
  document.querySelectorAll("[data-workbench-root]").forEach((root) => {
    const toggle = root.querySelector("[data-sidebar-toggle]");
    if (!toggle) {
      return;
    }

    const apply = (collapsed) => {
      root.classList.toggle("sidebar-collapsed", collapsed);
      toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
      toggle.title = collapsed ? "展开侧栏" : "收起侧栏";
    };

    apply(window.localStorage.getItem(storageKey) === "1");

    toggle.addEventListener("click", () => {
      const next = !root.classList.contains("sidebar-collapsed");
      apply(next);
      window.localStorage.setItem(storageKey, next ? "1" : "0");
    });
  });
})();

(() => {
  const root = document.querySelector("[data-floating-companion]");
  if (!root) {
    return;
  }

  const panel = root.querySelector("[data-floating-panel]");
  const toggle = root.querySelector("[data-floating-toggle]");
  const close = root.querySelector("[data-floating-close]");
  const storageKey = "spiritdesk.floatingCompanionOpen";

  if (!panel || !toggle) {
    return;
  }

  const setOpen = (open) => {
    root.classList.toggle("is-open", open);
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    panel.setAttribute("aria-hidden", open ? "false" : "true");
    window.localStorage.setItem(storageKey, open ? "1" : "0");
  };

  setOpen(window.localStorage.getItem(storageKey) === "1");

  toggle.addEventListener("click", () => {
    setOpen(!root.classList.contains("is-open"));
  });

  close?.addEventListener("click", () => setOpen(false));

  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && root.classList.contains("is-open")) {
      setOpen(false);
    }
  });
})();

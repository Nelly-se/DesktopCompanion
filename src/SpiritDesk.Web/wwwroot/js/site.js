(() => {
  const modal = document.getElementById("gameModal");
  if (!modal) return;
  const setOpen = (open) => {
    modal.classList.toggle("is-open", open);
    modal.setAttribute("aria-hidden", open ? "false" : "true");
  };
  modal.querySelectorAll("[data-close-game-modal]").forEach((el) => el.addEventListener("click", () => setOpen(false)));
  document.querySelectorAll(".game-panel-open").forEach((btn) => btn.addEventListener("click", () => setOpen(true)));
  window.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && modal.classList.contains("is-open")) setOpen(false);
  });
})();

(() => {
  const storageKey = "spiritdesk.sidebarCollapsed";
  document.querySelectorAll("[data-workbench-root]").forEach((root) => {
    const toggle = root.querySelector("[data-sidebar-toggle]");
    if (!toggle) return;
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


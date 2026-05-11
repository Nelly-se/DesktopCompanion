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

(() => {
  const roots = Array.from(document.querySelectorAll("[data-floating-companion]"));
  if (roots.length === 0) return;

  const root = roots[0];
  roots.slice(1).forEach((node) => node.remove());

  const panel = root.querySelector("[data-floating-panel]");
  const toggle = root.querySelector("[data-floating-toggle]");
  const close = root.querySelector("[data-floating-close]");
  const bubble = root.querySelector("[data-floating-bubble]");
  console.log("floating companion init", {
    companion: root,
    toggle,
    panel
  });
  if (!panel || !toggle) {
    console.warn("[SpiritDesk] floating companion: missing panel or toggle handle.");
    return;
  }

  const keyExpanded = "spiritdesk.floating.expanded";
  const keyLeft = "spiritdesk.floating.left";
  const keyTop = "spiritdesk.floating.top";
  const margin = 8;

  const bubbles = {
    default: ["我一直在这里陪你。", "今天也可以慢慢来。", "要不要先完成一个小任务？", "记得休息一下眼睛哦。", "有我在，任务也没那么难啦。"],
    "卷卷晴": ["先完成最重要的一件事吧。", "目标明确，行动就会轻松。"],
    "嘻嘻滴": ["今天也要开心一点！", "来点快乐能量！"],
    "贴贴朵": ["要不要关心一下朋友？", "你不是一个人在努力哦。"],
    "慢慢壤": ["累了可以先停一下。", "照顾好自己也很重要。"],
    "新新星": ["换个角度试试看？", "今天可以尝试一点新东西！"]
  };

  const name = root.querySelector(".floating-companion-head h3")?.textContent?.trim() || "";
  const pool = bubbles[name] || bubbles.default;
  if (bubble && pool.length > 0) {
    bubble.textContent = pool[Math.floor(Math.random() * pool.length)];
  }

  const setOpen = (open) => {
    root.classList.toggle("is-open", open);
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    panel.setAttribute("aria-hidden", open ? "false" : "true");
    window.localStorage.setItem(keyExpanded, open ? "1" : "0");
  };

  setOpen(window.localStorage.getItem(keyExpanded) === "1");

  close?.addEventListener("click", () => setOpen(false));
  close?.addEventListener("pointerdown", (e) => e.stopPropagation());

  document.querySelectorAll("#floating-companion img").forEach((img) => {
    img.setAttribute("draggable", "false");
    img.addEventListener("dragstart", (e) => e.preventDefault());
  });

  let isDragging = false;
  let hasMoved = false;
  let pointerId = null;
  let startX = 0;
  let startY = 0;
  let startLeft = 0;
  let startTop = 0;
  let suppressClick = false;

  const dragHandle = toggle || root;

  const getHandleSize = () => {
    const rect = dragHandle.getBoundingClientRect();
    const width = rect.width || 92;
    const height = rect.height || 92;
    return { width, height };
  };

  const getBounds = () => {
    const { width, height } = getHandleSize();
    return {
      minLeft: margin,
      minTop: margin,
      maxLeft: Math.max(margin, window.innerWidth - width - margin),
      maxTop: Math.max(margin, window.innerHeight - height - margin)
    };
  };

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  const clampPosition = (left, top) => {
    const bounds = getBounds();
    return {
      left: clamp(left, bounds.minLeft, bounds.maxLeft),
      top: clamp(top, bounds.minTop, bounds.maxTop)
    };
  };

  const applyPosition = (left, top) => {
    const pos = clampPosition(left, top);
    root.style.right = "auto";
    root.style.bottom = "auto";
    root.style.left = `${Math.round(pos.left)}px`;
    root.style.top = `${Math.round(pos.top)}px`;
  };

  const restorePosition = () => {
    const { width, height } = getHandleSize();
    const defaultLeft = Math.max(margin, window.innerWidth - width - 24);
    const defaultTop = Math.max(margin, window.innerHeight - height - 24);

    const parsedLeft = Number(window.localStorage.getItem(keyLeft));
    const parsedTop = Number(window.localStorage.getItem(keyTop));
    const left = Number.isFinite(parsedLeft) ? parsedLeft : defaultLeft;
    const top = Number.isFinite(parsedTop) ? parsedTop : defaultTop;
    applyPosition(left, top);
  };

  restorePosition();

  const stopDragging = () => {
    if (!isDragging) return;
    isDragging = false;
    pointerId = null;
    root.classList.remove("is-dragging");
    const currentLeft = Number.parseFloat(root.style.left);
    const currentTop = Number.parseFloat(root.style.top);
    const safePos = clampPosition(
      Number.isFinite(currentLeft) ? currentLeft : root.getBoundingClientRect().left,
      Number.isFinite(currentTop) ? currentTop : root.getBoundingClientRect().top
    );
    applyPosition(safePos.left, safePos.top);
    window.localStorage.setItem(keyLeft, String(Math.round(safePos.left)));
    window.localStorage.setItem(keyTop, String(Math.round(safePos.top)));
  };

  toggle.addEventListener("pointerdown", (e) => {
    if (e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation();

    isDragging = true;
    hasMoved = false;
    pointerId = e.pointerId;

    const rect = root.getBoundingClientRect();
    startX = e.clientX;
    startY = e.clientY;
    startLeft = rect.left;
    startTop = rect.top;
    root.classList.add("is-dragging");
    toggle.setPointerCapture?.(e.pointerId);
  });

  const onPointerMove = (e) => {
    if (!isDragging) return;
    if (pointerId !== null && e.pointerId !== pointerId) return;

    const dx = e.clientX - startX;
    const dy = e.clientY - startY;
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) hasMoved = true;

    applyPosition(startLeft + dx, startTop + dy);
  };

  const onPointerUp = (e) => {
    if (!isDragging) return;
    if (pointerId !== null && e.pointerId !== pointerId) return;
    const moved = hasMoved;
    suppressClick = true;
    stopDragging();
    if (!moved) {
      setOpen(!root.classList.contains("is-open"));
    }
    window.setTimeout(() => {
      suppressClick = false;
    }, 80);
  };

  toggle.addEventListener("click", (e) => {
    console.log("floating companion click", e.target);
    e.preventDefault();
    if (suppressClick || isDragging) return;
    setOpen(!root.classList.contains("is-open"));
  });

  document.addEventListener("pointermove", onPointerMove);
  document.addEventListener("pointerup", onPointerUp);
  document.addEventListener("mouseup", stopDragging);
  document.addEventListener("mouseleave", stopDragging);
  window.addEventListener("blur", stopDragging);
  window.addEventListener("dragstart", (e) => {
    if (root.contains(e.target)) e.preventDefault();
  });

  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && root.classList.contains("is-open")) setOpen(false);
  });

  window.addEventListener("resize", () => {
    const left = Number.parseFloat(root.style.left);
    const top = Number.parseFloat(root.style.top);
    if (Number.isFinite(left) && Number.isFinite(top)) {
      applyPosition(left, top);
    } else {
      restorePosition();
    }
  });
})();

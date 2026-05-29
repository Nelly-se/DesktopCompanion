// site.js — 全站前端小脚本：猜拳弹窗、侧栏折叠 localStorage（无构建步骤，页面直接引用）

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
  const form = document.querySelector("[data-stream-chat-form]");
  const stream = document.querySelector("[data-stream-chat-list]");
  if (!form || !stream) return;

  const input = form.querySelector("textarea[name='MessageText']");
  const submit = form.querySelector("button[type='submit']");
  const userName = stream.dataset.userName || "你";
  const spiritName = stream.dataset.spiritName || "精灵";

  const scrollToBottom = () => {
    stream.scrollTop = stream.scrollHeight;
  };

  const removePlaceholder = () => {
    stream.querySelector(".desk-chat-placeholder")?.remove();
  };

  const appendLine = (sender, name, text = "") => {
    const line = document.createElement("div");
    line.className = `desk-chat-line ${sender}`;

    const who = document.createElement("span");
    who.className = "desk-chat-who";
    who.textContent = name;

    const msg = document.createElement("span");
    msg.className = "desk-chat-msg";
    msg.textContent = text;

    line.append(who, msg);
    stream.append(line);
    scrollToBottom();
    return { line, msg };
  };

  const handleStreamLine = (line, spiritMessage) => {
    if (!line.trim()) return;
    const event = JSON.parse(line);
    if (event.type === "delta") {
      spiritMessage.textContent += event.content || "";
      scrollToBottom();
      return;
    }

    if (event.type === "error") {
      spiritMessage.textContent = event.content || "发送失败，请稍后再试。";
    }
  };

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!input) return;

    const message = input.value.trim();
    if (!message) {
      input.focus();
      return;
    }

    removePlaceholder();
    appendLine("user", userName, message);
    const spirit = appendLine("spirit is-streaming", spiritName);
    input.value = "";
    input.disabled = true;
    if (submit) submit.disabled = true;

    try {
      const body = new FormData(form);
      body.set("MessageText", message);
      const response = await fetch(form.dataset.streamUrl || form.action, {
        method: "POST",
        body,
        headers: { "X-Requested-With": "fetch" }
      });

      if (!response.ok || !response.body) {
        throw new Error(`stream request failed: ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() || "";
        lines.forEach((line) => handleStreamLine(line, spirit.msg));
      }

      if (buffer.trim()) {
        handleStreamLine(buffer, spirit.msg);
      }
    } catch {
      spirit.msg.textContent = "消息发送失败，请稍后再试。";
    } finally {
      spirit.line.classList.remove("is-streaming");
      input.disabled = false;
      if (submit) submit.disabled = false;
      input.focus();
      scrollToBottom();
    }
  });
})();


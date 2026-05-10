(() => {
  const root = document.getElementById("deskCalendarRoot");
  const dataEl = document.getElementById("desk-calendar-data");
  if (!root || !dataEl) {
    return;
  }

  /** @type {{ id:number; title:string; description:string|null; dueAt:string|null; createdAt:string; isCompleted:boolean; completedAt:string|null }[]} */
  let tasks = [];
  try {
    tasks = JSON.parse(dataEl.textContent || "[]");
  } catch {
    tasks = [];
  }

  const titleEl = root.closest(".desk-cal-shell")?.querySelector("[data-cal-title]");
  const prevBtn = root.closest(".desk-cal-shell")?.querySelector("[data-cal-prev]");
  const nextBtn = root.closest(".desk-cal-shell")?.querySelector("[data-cal-next]");
  const todayBtn = root.closest(".desk-cal-shell")?.querySelector("[data-cal-today]");
  const viewButtons = Array.from(root.closest(".desk-cal-shell")?.querySelectorAll("[data-cal-view]") ?? []);
  const filterButtons = Array.from(root.closest(".desk-cal-shell")?.querySelectorAll("[data-cal-filter]") ?? []);

  /** @type {'day'|'week'|'month'} */
  let view = "week";
  /** @type {'pending'|'all'} */
  let filter = "pending";

  let anchor = startOfDay(new Date());

  const activeViewBtn = viewButtons.find((b) => b.classList.contains("active"));
  const vv = activeViewBtn?.getAttribute("data-cal-view");
  if (vv === "day" || vv === "week" || vv === "month") {
    view = vv;
  }

  const activeFilterBtn = filterButtons.find((b) => b.classList.contains("active"));
  const ff = activeFilterBtn?.getAttribute("data-cal-filter");
  if (ff === "pending" || ff === "all") {
    filter = ff;
  }

  function pad(n) {
    return String(n).padStart(2, "0");
  }

  function toYmd(d) {
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  function parseIsoDate(s) {
    if (!s) {
      return null;
    }
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
    if (!m) {
      return null;
    }
    return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  }

  function startOfDay(d) {
    return new Date(d.getFullYear(), d.getMonth(), d.getDate());
  }

  function addDays(d, n) {
    const x = new Date(d);
    x.setDate(x.getDate() + n);
    return startOfDay(x);
  }

  function startOfWeekMonday(d) {
    const x = startOfDay(d);
    const dow = x.getDay();
    const offset = dow === 0 ? -6 : 1 - dow;
    return addDays(x, offset);
  }

  function taskBucketDay(t) {
    const due = t.dueAt ? parseIsoDate(t.dueAt) : null;
    if (due) {
      return toYmd(due);
    }
    const c = parseIsoDate(t.createdAt);
    return c ? toYmd(c) : toYmd(new Date());
  }

  function visibleTasks() {
    if (filter === "all") {
      return tasks;
    }
    return tasks.filter((x) => !x.isCompleted);
  }

  function tasksForDay(ymd) {
    return visibleTasks().filter((t) => taskBucketDay(t) === ymd);
  }

  function submitCompleteTask(taskId) {
    const src = document.getElementById("desk-antiforgery-source");
    if (!src) {
      return;
    }
    const tokenInput = src.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) {
      return;
    }
    const form = document.createElement("form");
    form.method = "POST";
    form.action = `${window.location.pathname}?handler=CompleteTask`;

    const tokenClone = document.createElement("input");
    tokenClone.type = "hidden";
    tokenClone.name = "__RequestVerificationToken";
    tokenClone.value = tokenInput.value;
    form.appendChild(tokenClone);

    const tid = document.createElement("input");
    tid.type = "hidden";
    tid.name = "TaskId";
    tid.value = String(taskId);
    form.appendChild(tid);

    document.body.appendChild(form);
    form.submit();
  }

  root.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-complete-task]");
    if (!btn) {
      return;
    }
    e.preventDefault();
    const id = btn.getAttribute("data-complete-task");
    if (id) {
      submitCompleteTask(id);
    }
  });

  root.addEventListener("click", (e) => {
    const cell = e.target.closest("[data-cal-day]");
    if (!cell) {
      return;
    }
    const ymd = cell.getAttribute("data-cal-day");
    if (!ymd) {
      return;
    }
    anchor = parseIsoDate(ymd + "T00:00:00") || anchor;
    view = "day";
    syncViewButtons();
    render();
  });

  function weekdayLabels() {
    return ["日", "一", "二", "三", "四", "五", "六"];
  }

  function renderTitle() {
    if (!titleEl) {
      return;
    }
    if (view === "day") {
      titleEl.textContent = `${anchor.getFullYear()}年${anchor.getMonth() + 1}月${anchor.getDate()}日`;
      return;
    }
    if (view === "week") {
      const ws = startOfWeekMonday(anchor);
      const we = addDays(ws, 6);
      titleEl.textContent = `${ws.getMonth() + 1}月${ws.getDate()}日 — ${we.getMonth() + 1}月${we.getDate()}日，${ws.getFullYear()}`;
      return;
    }
    titleEl.textContent = `${anchor.getFullYear()}年${anchor.getMonth() + 1}月`;
  }

  function renderDay() {
    const ymd = toYmd(anchor);
    const dayTasks = tasksForDay(ymd).sort((a, b) => {
      const ta = a.dueAt || a.createdAt;
      const tb = b.dueAt || b.createdAt;
      return ta.localeCompare(tb);
    });

    const rows = dayTasks
      .map((t) => {
        const time = t.dueAt ? t.dueAt.slice(11, 16) || "" : "";
        const done = t.isCompleted ? " cal-task-done-row" : "";
        const actions = !t.isCompleted
          ? `<button type="button" class="cal-task-complete" data-complete-task="${t.id}">完成</button>`
          : `<span class="cal-task-status">已完成</span>`;
        const desc = t.description ? `<p class="cal-task-desc">${escapeHtml(t.description)}</p>` : "";
        return `
          <article class="cal-day-task${done}">
            <div class="cal-day-task-main">
              <span class="cal-task-time">${escapeHtml(time || "全天")}</span>
              <div>
                <strong>${escapeHtml(t.title)}</strong>
                ${desc}
              </div>
            </div>
            ${actions}
          </article>`;
      })
      .join("");

    root.innerHTML = `
      <div class="cal-day-view">
        <div class="cal-weekday-band">${weekdayLabels()[anchor.getDay()]} · ${anchor.getMonth() + 1}/${anchor.getDate()}</div>
        ${
          dayTasks.length === 0
            ? `<div class="cal-empty">这一天没有任务</div>`
            : `<div class="cal-day-list">${rows}</div>`
        }
      </div>`;
  }

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function renderWeek() {
    const ws = startOfWeekMonday(anchor);
    const labels = weekdayLabels();
    const cols = [];
    for (let i = 0; i < 7; i += 1) {
      const d = addDays(ws, i);
      const ymd = toYmd(d);
      const list = tasksForDay(ymd);
      const isToday = toYmd(new Date()) === ymd;
      const inner = list
        .map((t) => {
          const bar = t.isCompleted ? "done" : "pending";
          const short = escapeHtml(t.title.length > 18 ? `${t.title.slice(0, 18)}…` : t.title);
          const time = t.dueAt && t.dueAt.length > 11 ? t.dueAt.slice(11, 16) : "";
          return `<div class="cal-week-chip cal-week-chip--${bar}" title="${escapeHtml(t.title)}">${time ? `<span class="cal-week-chip-time">${escapeHtml(time)}</span>` : ""}<span class="cal-week-chip-title">${short}</span></div>`;
        })
        .join("");
      cols.push(`
        <div class="cal-week-col ${isToday ? "is-today" : ""}" data-cal-day="${ymd}">
          <div class="cal-week-col-head">
            <span class="cal-week-dow">${labels[d.getDay()]}</span>
            <span class="cal-week-date ${isToday ? "is-today-num" : ""}">${d.getDate()}</span>
          </div>
          <div class="cal-week-col-body">${inner || `<span class="cal-week-empty">—</span>`}</div>
        </div>`);
    }
    root.innerHTML = `<div class="cal-week-grid">${cols.join("")}</div>`;
  }

  function renderMonth() {
    const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
    const startGrid = startOfWeekMonday(first);
    const labels = weekdayLabels();
    const header = `<div class="cal-month-weekdays">${labels.map((l) => `<span>${l}</span>`).join("")}</div>`;
    const cells = [];
    let cursor = startGrid;
    for (let w = 0; w < 6; w += 1) {
      for (let d = 0; d < 7; d += 1) {
        const ymd = toYmd(cursor);
        const inMonth = cursor.getMonth() === anchor.getMonth();
        const isToday = toYmd(new Date()) === ymd;
        const list = tasksForDay(ymd).slice(0, 3);
        const more = tasksForDay(ymd).length - list.length;
        const chips = list
          .map((t) => {
            const bar = t.isCompleted ? "done" : "pending";
            const short = escapeHtml(t.title.length > 10 ? `${t.title.slice(0, 10)}…` : t.title);
            return `<span class="cal-month-chip cal-month-chip--${bar}">${short}</span>`;
          })
          .join("");
        cells.push(`
          <div class="cal-month-cell ${inMonth ? "" : "is-muted"} ${isToday ? "is-today" : ""}" data-cal-day="${ymd}">
            <div class="cal-month-date"><span class="${isToday ? "is-today-num" : ""}">${cursor.getDate()}</span></div>
            <div class="cal-month-chips">${chips}${more > 0 ? `<span class="cal-month-more">+${more}</span>` : ""}</div>
          </div>`);
        cursor = addDays(cursor, 1);
      }
    }
    root.innerHTML = `<div class="cal-month">${header}<div class="cal-month-grid">${cells.join("")}</div></div>`;
  }

  function render() {
    renderTitle();
    if (view === "day") {
      renderDay();
    } else if (view === "week") {
      renderWeek();
    } else {
      renderMonth();
    }
  }

  function syncViewButtons() {
    viewButtons.forEach((b) => {
      b.classList.toggle("active", b.getAttribute("data-cal-view") === view);
    });
    filterButtons.forEach((b) => {
      b.classList.toggle("active", b.getAttribute("data-cal-filter") === filter);
    });
  }

  prevBtn?.addEventListener("click", () => {
    if (view === "day") {
      anchor = addDays(anchor, -1);
    } else if (view === "week") {
      anchor = addDays(anchor, -7);
    } else {
      anchor = new Date(anchor.getFullYear(), anchor.getMonth() - 1, 1);
    }
    render();
  });

  nextBtn?.addEventListener("click", () => {
    if (view === "day") {
      anchor = addDays(anchor, 1);
    } else if (view === "week") {
      anchor = addDays(anchor, 7);
    } else {
      anchor = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1);
    }
    render();
  });

  todayBtn?.addEventListener("click", () => {
    anchor = startOfDay(new Date());
    render();
  });

  viewButtons.forEach((b) => {
    b.addEventListener("click", () => {
      const v = b.getAttribute("data-cal-view");
      if (v === "day" || v === "week" || v === "month") {
        view = v;
        syncViewButtons();
        render();
      }
    });
  });

  filterButtons.forEach((b) => {
    b.addEventListener("click", () => {
      const f = b.getAttribute("data-cal-filter");
      if (f === "pending" || f === "all") {
        filter = f;
        syncViewButtons();
        render();
      }
    });
  });

  syncViewButtons();
  render();
})();

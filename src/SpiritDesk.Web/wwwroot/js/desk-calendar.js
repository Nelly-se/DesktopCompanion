(() => {
  const root = document.getElementById("deskCalendarRoot");
  const dataEl = document.getElementById("desk-calendar-data");
  if (!root || !dataEl) return;

  let tasks = [];
  try { tasks = JSON.parse(dataEl.textContent || "[]"); } catch { tasks = []; }

  const shell = root.closest(".desk-cal-shell");
  const titleEl = shell?.querySelector("[data-cal-title]");
  const prevBtn = shell?.querySelector("[data-cal-prev]");
  const nextBtn = shell?.querySelector("[data-cal-next]");
  const todayBtn = shell?.querySelector("[data-cal-today]");
  const viewButtons = Array.from(shell?.querySelectorAll("[data-cal-view]") ?? []);
  const filterButtons = Array.from(shell?.querySelectorAll("[data-cal-filter]") ?? []);

  let view = "week";
  let filter = "pending";
  let anchor = startOfDay(new Date());

  function pad(n) { return String(n).padStart(2, "0"); }
  function toYmd(d) { return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`; }
  function startOfDay(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
  function addDays(d, n) { const x = new Date(d); x.setDate(x.getDate() + n); return startOfDay(x); }
  function startOfWeekMonday(d) { const x = startOfDay(d); const dow = x.getDay(); return addDays(x, dow === 0 ? -6 : 1 - dow); }
  function parseIsoDate(s) {
    if (!s) return null;
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
    return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : null;
  }
  function escapeHtml(s) {
    return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/\"/g, "&quot;");
  }
  function weekdayLabels() { return ["日", "一", "二", "三", "四", "五", "六"]; }
  function taskBucketDay(t) {
    const due = t.dueAt ? parseIsoDate(t.dueAt) : null;
    if (due) return toYmd(due);
    const c = parseIsoDate(t.createdAt);
    return c ? toYmd(c) : toYmd(new Date());
  }
  function visibleTasks() { return filter === "all" ? tasks : tasks.filter((t) => !t.isCompleted); }
  function tasksForDay(ymd) { return visibleTasks().filter((t) => taskBucketDay(t) === ymd); }

  function submitCompleteTask(taskId) {
    const src = document.getElementById("desk-antiforgery-source");
    const tokenInput = src?.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;
    const form = document.createElement("form");
    form.method = "POST";
    form.action = `${window.location.pathname}?handler=CompleteTask`;
    form.innerHTML = `<input type="hidden" name="__RequestVerificationToken" value="${tokenInput.value}"><input type="hidden" name="TaskId" value="${taskId}">`;
    document.body.appendChild(form);
    form.submit();
  }

  root.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-complete-task]");
    if (!btn) return;
    e.preventDefault();
    submitCompleteTask(btn.getAttribute("data-complete-task"));
  });

  root.addEventListener("click", (e) => {
    const cell = e.target.closest("[data-cal-day]");
    if (!cell) return;
    const ymd = cell.getAttribute("data-cal-day");
    if (!ymd) return;
    anchor = parseIsoDate(`${ymd}T00:00:00`) || anchor;
    view = "day";
    syncButtons();
    render();
  });

  function renderTitle() {
    if (!titleEl) return;
    if (view === "day") {
      titleEl.textContent = `${anchor.getFullYear()}年${anchor.getMonth() + 1}月${anchor.getDate()}日`;
      return;
    }
    if (view === "week") {
      const ws = startOfWeekMonday(anchor);
      const we = addDays(ws, 6);
      titleEl.textContent = `${ws.getMonth() + 1}月${ws.getDate()}日 - ${we.getMonth() + 1}月${we.getDate()}日，${ws.getFullYear()}年`;
      return;
    }
    titleEl.textContent = `${anchor.getFullYear()}年${anchor.getMonth() + 1}月`;
  }

  function renderDay() {
    const ymd = toYmd(anchor);
    const dayTasks = tasksForDay(ymd).sort((a, b) => (a.dueAt || a.createdAt).localeCompare(b.dueAt || b.createdAt));
    const rows = dayTasks.map((t) => {
      const time = t.dueAt ? t.dueAt.slice(11, 16) || "" : "";
      const done = t.isCompleted ? " cal-task-done-row" : "";
      const actions = t.isCompleted ? "<span class=\"cal-task-status\">已完成</span>" : `<button type=\"button\" class=\"cal-task-complete\" data-complete-task=\"${t.id}\">完成</button>`;
      const desc = t.description ? `<p class=\"cal-task-desc\">${escapeHtml(t.description)}</p>` : "";
      return `<article class=\"cal-day-task${done}\"><div class=\"cal-day-task-main\"><span class=\"cal-task-time\">${escapeHtml(time || "全天")}</span><div><strong>${escapeHtml(t.title)}</strong>${desc}</div></div>${actions}</article>`;
    }).join("");
    root.innerHTML = `<div class=\"cal-day-view\"><div class=\"cal-weekday-band\">${weekdayLabels()[anchor.getDay()]} · ${anchor.getMonth() + 1}/${anchor.getDate()}</div>${dayTasks.length === 0 ? "<div class=\"cal-empty\">这一天没有任务。</div>" : `<div class=\"cal-day-list\">${rows}</div>`}</div>`;
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
      const inner = list.map((t) => {
        const bar = t.isCompleted ? "done" : "pending";
        const short = escapeHtml(t.title.length > 18 ? `${t.title.slice(0, 18)}…` : t.title);
        const time = t.dueAt && t.dueAt.length > 11 ? t.dueAt.slice(11, 16) : "";
        return `<div class=\"cal-week-chip cal-week-chip--${bar}\" title=\"${escapeHtml(t.title)}\">${time ? `<span class=\"cal-week-chip-time\">${escapeHtml(time)}</span>` : ""}<span class=\"cal-week-chip-title\">${short}</span></div>`;
      }).join("");
      cols.push(`<div class=\"cal-week-col ${isToday ? "is-today" : ""}\" data-cal-day=\"${ymd}\"><div class=\"cal-week-col-head\"><span class=\"cal-week-dow\">${labels[d.getDay()]}</span><span class=\"cal-week-date ${isToday ? "is-today-num" : ""}\">${d.getDate()}</span></div><div class=\"cal-week-col-body\">${inner || "<span class=\"cal-week-empty\">暂无</span>"}</div></div>`);
    }
    root.innerHTML = `<div class=\"cal-week-grid\">${cols.join("")}</div>`;
  }

  function renderMonth() {
    const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
    const startGrid = startOfWeekMonday(first);
    const labels = weekdayLabels();
    const header = `<div class=\"cal-month-weekdays\">${labels.map((l) => `<span>${l}</span>`).join("")}</div>`;
    const cells = [];
    let cursor = startGrid;
    for (let w = 0; w < 6; w += 1) {
      for (let d = 0; d < 7; d += 1) {
        const ymd = toYmd(cursor);
        const inMonth = cursor.getMonth() === anchor.getMonth();
        const isToday = toYmd(new Date()) === ymd;
        const dayTasks = tasksForDay(ymd);
        const list = dayTasks.slice(0, 3);
        const chips = list.map((t) => {
          const bar = t.isCompleted ? "done" : "pending";
          const short = escapeHtml(t.title.length > 10 ? `${t.title.slice(0, 10)}…` : t.title);
          return `<span class=\"cal-month-chip cal-month-chip--${bar}\">${short}</span>`;
        }).join("");
        const more = dayTasks.length - list.length;
        cells.push(`<div class=\"cal-month-cell ${inMonth ? "" : "is-muted"} ${isToday ? "is-today" : ""}\" data-cal-day=\"${ymd}\"><div class=\"cal-month-date\"><span class=\"${isToday ? "is-today-num" : ""}\">${cursor.getDate()}</span></div><div class=\"cal-month-chips\">${chips}${more > 0 ? `<span class=\"cal-month-more\">+${more}</span>` : ""}</div></div>`);
        cursor = addDays(cursor, 1);
      }
    }
    root.innerHTML = `<div class=\"cal-month\">${header}<div class=\"cal-month-grid\">${cells.join("")}</div></div>`;
  }

  function syncButtons() {
    viewButtons.forEach((b) => b.classList.toggle("active", b.getAttribute("data-cal-view") === view));
    filterButtons.forEach((b) => b.classList.toggle("active", b.getAttribute("data-cal-filter") === filter));
  }

  function render() {
    renderTitle();
    if (view === "day") renderDay();
    else if (view === "week") renderWeek();
    else renderMonth();
  }

  prevBtn?.addEventListener("click", () => {
    anchor = view === "day" ? addDays(anchor, -1) : view === "week" ? addDays(anchor, -7) : new Date(anchor.getFullYear(), anchor.getMonth() - 1, 1);
    render();
  });
  nextBtn?.addEventListener("click", () => {
    anchor = view === "day" ? addDays(anchor, 1) : view === "week" ? addDays(anchor, 7) : new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1);
    render();
  });
  todayBtn?.addEventListener("click", () => { anchor = startOfDay(new Date()); render(); });
  viewButtons.forEach((b) => b.addEventListener("click", () => {
    const v = b.getAttribute("data-cal-view");
    if (v === "day" || v === "week" || v === "month") { view = v; syncButtons(); render(); }
  }));
  filterButtons.forEach((b) => b.addEventListener("click", () => {
    const f = b.getAttribute("data-cal-filter");
    if (f === "pending" || f === "all") { filter = f; syncButtons(); render(); }
  }));

  syncButtons();
  render();
})();

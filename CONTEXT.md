# Mentor-Overseer — Project Context

## What this app is
A desktop personal mentor and accountability companion that tracks the user's progress
across up to 2 active life/career plans simultaneously. It monitors his activity,
keeps him on-plan, logs his full day (06:00–20:00), and generates weekly reports.

## The user
- Name: the user
- Location: Milan, Italy (moving to Utrecht/Eindhoven, NL)
- Goal: Land a Dutch Digital Transformation Manager role → HSM visa → EU citizenship
- Key tools: Power BI, Power Platform, SharePoint, MBA from Bologna
- Current active plan: Netherlands Relocation (plans/active/netherlands.json)

---

## App architecture

```
mentor-overseer/
├── CONTEXT.md              ← you are here. Read this first every session.
├── config.json             ← user settings, idle threshold, scoring rules (no secrets —
│                              TickTick client_secret/access_token live in Windows
│                              Credential Manager via `keyring`, not on disk)
├── main.py                 ← single-file app entry point (MentorApp class, ~2100 lines)
├── plans/
│   ├── active/             ← up to 2 active plan JSONs (e.g. netherlands.json)
│   └── archive/            ← completed plans moved here; frees a slot for a new plan
├── plan/
│   └── roadmap.json        ← legacy source (auto-migrated to plans/active/ on first run)
├── tracker/
│   ├── __init__.py
│   └── activity.py         ← window monitor, diary session tracking, idle detection
├── ticktick/
│   ├── __init__.py
│   └── sync.py             ← TickTick OAuth2 REST client
├── data/
│   └── progress.db         ← SQLite: task_completions, activity_log, time_diary, ticktick_sync
├── MentorOverseer.exe      ← PyInstaller single-file build
├── build.bat               ← PyInstaller build script
└── run.bat                 ← dev launcher
```

---

## Tech stack
- **Language:** Python 3.11 — `C:\Users\devba\AppData\Local\Programs\Python\Python311\python.exe`
- **UI framework:** Tkinter (3-column layout: sidebar + task list + detail panel)
- **Database:** SQLite via sqlite3
- **Notifications:** pystray (tray icon + notify) with plyer fallback
- **Activity tracking:** ctypes `GetForegroundWindow` + `GetLastInputInfo` (Windows)
- **TickTick:** REST API (OAuth2) — redirect URI: `http://localhost:8765/callback`
- **Packaging:** PyInstaller `--onefile --noconsole --icon`
- **App name constant:** `APP_NAME = "Mentor-Overseer"`, `MAX_PLANS = 2`

---

## Build phases — ALL COMPLETE

### Phase 1 — Plan engine + daily view ✅
- Load roadmap, calculate plan day, show today's tasks, mark complete, save to DB
- End-of-day summary at 20:00

### Phase 2 — TickTick sync ✅
- OAuth2 flow, pull personal tasks, push plan tasks, sync completions bidirectionally
- "My Tasks (TickTick)" section shown below plan tasks in the main list

### Phase 3 — Activity tracker ✅
- Polls active window every 60s; classifies on_plan / off_plan / neutral via keyword rules
- Focus alerts: off-plan >15 min → notification; escalates every 5 min
- Full time diary 06:00–20:00: logs every session to `time_diary` table
- Idle detection: `GetLastInputInfo` + wall-clock gap for sleep detection
- After 10 min idle/sleep: blocking modal dialog (no X button, must type) asks "What were you doing?"
  Answer saved to `time_diary` with category="idle"

### Phase 4 — Reports ✅
- Weekly report view (sidebar "Weekly Report" nav)
- Today's productivity score card (tasks × 10, overdue −5, on-plan +3/hr, off-plan −2/hr)
- Weekly table: 7 days of tasks / on-plan min / off-plan min / score
- Top distractions with mini bar chart
- Rule-based insights (>2h off-plan, <50% task completion, etc.)
- Time Diary section: stacked breakdown bar + chronological timeline; idle entries in amber italic
- Export HTML report button → opens `report.html` in browser

### Multi-plan architecture ✅
- Up to 2 active plans loaded from `plans/active/*.json`
- Auto-migrates legacy `plan/roadmap.json` → `plans/active/netherlands.json` on first run
- Sidebar shows "ACTIVE PLANS" section with colored dot, day counter, today's progress
- "+ Add Plan" button (file picker → validate → ask start date → copy to active/)
- "Archive ✓" button appears when all tasks done → moves to `plans/archive/`; frees slot
- Daily task list: per-plan colored section header + Overdue/Today sub-sections, then TickTick section
- Activity from any active plan counts as on-plan

---

## Plan JSON format
```json
{
  "id": "netherlands",
  "name": "Netherlands Relocation",
  "color": "#0a84ff",
  "start_date": "2026-06-29",
  "total_days": 160,
  "phases": [
    {
      "phase": 1,
      "name": "Phase Name",
      "tasks": [
        { "day": 1, "task": "Task title", "detail": "...", "category": "profile", "duration_min": 60 }
      ]
    }
  ]
}
```
Required fields when adding a plan: `id`, `name`, `phases`. `start_date` asked interactively if missing.

---

## Database schema (progress.db)

```sql
task_completions (id, plan_id TEXT DEFAULT 'netherlands', plan_day, task_text, completed,
                  completed_at, last_updated)
  UNIQUE INDEX tc_plan_idx ON (plan_id, plan_day, task_text)

activity_log (id, logged_at, window, class)          -- one row per 60s poll

time_diary (id, date, start_time, end_time,
            duration_min, category, window, description)
  -- category: on_plan | off_plan | neutral | idle
  -- description: user's answer to idle dialog (idle rows only)

ticktick_sync (id, plan_day, task_text, ticktick_task_id,
               ticktick_proj_id, pushed_at, synced_at)
  UNIQUE(plan_day, task_text)
```

---

## Key business rules
1. Working hours: 08:00–20:00 (configurable in config.json)
2. Diary tracking hours: 06:00–20:00 (hardcoded in ActivityTracker)
3. Reminder grace: 15 min off-plan before first alert
4. Reminder escalation: every 5 min after first
5. Idle threshold: 10 min (config `idle_threshold_minutes`)
6. Sleep detection: if wall-clock gap between polls >> POLL_SECONDS → treat as sleep/idle
7. Idle dialog: modal Toplevel, X button disabled, submit disabled until text entered
8. Plan day: `(today - start_date).days + 1`
9. Overdue: any incomplete task from a past day surfaces again with [OVERDUE] style
10. Archive: plan moves to `plans/archive/` when ALL tasks done; slot freed for new plan
11. Completions key: `(plan_id, plan_day, task_text)` — both in-memory dict and DB

---

## config.json key fields
```json
{
  "user_name": "the user",
  "plan_start_date": "2026-06-29",
  "working_hours": { "start": "08:00", "end": "20:00" },
  "reminder_grace_minutes": 15,
  "reminder_interval_minutes": 5,
  "idle_threshold_minutes": 10,
  "end_of_day_summary_time": "20:00",
  "ticktick": { "client_id": "..." },
  "activity_rules": {
    "on_plan":  ["Power BI", "PowerPoint", "LinkedIn", "Excel", "Claude", ...],
    "off_plan": ["Netflix", "YouTube", "Steam", "Tanki Online", "Besiege", ...],
    "neutral":  ["Chrome", "Firefox", "Slack", ...]
  },
  "scoring": { "task_completed": 10, "task_overdue_penalty": -5,
               "on_plan_hour": 3, "off_plan_hour": -2, "streak_bonus_per_day": 5 }
}
```
`ticktick.client_secret` / `access_token` / `refresh_token` are never written to this file —
they live in Windows Credential Manager via `keyring`, migrated out of config.json on first
launch after the 2026-06-29 audit fix.

---

## Session handoff notes
_Update this section at the end of each Claude Code session:_

- Last session: 2026-07-01
- What was built:
  - Security/reliability audit (2026-06-29) — all High/Medium findings fixed on branch
    `audit-remediation` (keyring credentials, autostart toggle, SQLite thread isolation,
    HTML escaping, OAuth CSRF state, parametrized SQL). See git log for detail.
  - Feature work (committed 2026-07-01): idle/sleep dialog now fires on return (reports
    actual elapsed duration) instead of the moment idle starts; messenger window-title
    badge stripping; Schedule dialog to pull a future task to today (shifts intervening
    tasks forward a day); startup gap check; diary entry edit/delete UI.
  - Bug fix (2026-07-01): Today/Report task list canvas could render blank — it never
    reset scroll position when switching views, so returning to Today from a taller
    view (Weekly Report, or after the Schedule dialog) left the view scrolled past the
    short amount of real content. Fixed by resetting `canvas.yview_moveto(0)` on view
    switch. Also fixed the Schedule dialog's mousewheel binding, which used `bind_all`/
    `unbind_all` and was clobbering the main window's global scroll binding.
  - Reliability: added `root.report_callback_exception` so exceptions raised inside
    Tkinter callbacks (button commands, binds, `after()`) are logged to
    `data/mentor.log` — previously only `sys.excepthook` was wired up, which does NOT
    catch Tkinter callback exceptions, so UI crashes were silently swallowed with zero
    trace. `render_tasks()` also now has a try/except safety net that shows a "Retry"
    button instead of leaving the pane blank.
  - Schema fix: `task_completions` carried a legacy inline `UNIQUE(plan_day, task_text)`
    constraint from before multi-plan support (global across all plans, not caught by
    the `ON CONFLICT` clause in `save_completion`). `ensure_data_store()` now detects
    and rebuilds the table without it, preserving data.
  - Telegram grouping fix (2026-07-01): "Time By App" reports were splitting the same
    Telegram chat between a standalone top-level entry and a nested Telegram sub-item,
    and some Telegram sub-names retained leftover unread-badge digits (e.g.
    "Telegram (1027)"). Root causes, confirmed against a week of real `time_diary` data:
    - `_active_window_title()` re-resolved the owning exe via `OpenProcess` on every
      poll; when that WinAPI call failed (happened often for Telegram), the title was
      stored with no "– Telegram" suffix and got misfiled as a standalone app. Fixed
      with a sticky per-PID cache (`_pid_app_cache` in tracker/activity.py) so one
      successful resolution covers the rest of that process's session.
    - `_strip_unread_badge()` required pure ASCII digits inside the parens, so a
      locale-formatted thousands separator (`1,027`) or a second trailing paren group
      (`(525) (2)`) left the badge stuck. Now strips iteratively and tolerates common
      separator characters.
    - `_poll_once()` was re-stripping the Unicode LTR mark *after*
      `_active_window_title()` already returned, without also stripping the badge that
      went with it — this silently broke the report's older LTR-mark-based Telegram
      fallback whenever exe resolution failed. Removed the redundant re-normalisation.
    - `main.py`'s `_get_app_sub()` now re-applies badge stripping at report-render time
      too, so already-logged rows with a stuck badge display correctly with no DB
      migration needed. Chats logged with zero identifying signal at all (bare name,
      no marker — e.g. old data predating any of this session's fixes) can't be
      retroactively regrouped; only newly-logged sessions benefit going forward.
  - Security: found `ticktick.client_secret` / `access_token` committed in plaintext in
    git history (baseline commit, predating the keyring migration). No remote was
    configured, so exposure was local-only. Rewrote git history on both `master` and
    `audit-remediation` with `git filter-branch` to redact the values, then purged all
    backup refs/reflog and ran `git gc --prune=now --aggressive` — verified via a full
    object scan that the secret no longer exists anywhere in the repo. Commit hashes on
    both branches changed as a result. the user still needs to rotate the TickTick OAuth
    credential at developer.ticktick.com as a precaution (old values are unrecoverable
    now, but treat them as burned).
- What to build next: nothing specified — await the user's direction
- Known issues / notes:
  - TickTick redirect URI must be registered at developer.ticktick.com: `http://localhost:8765/callback`
  - `tracker/__init__.py` / `ticktick/__init__.py` were missing from git for a while
    (package worked locally without them tracked) — now added.
  - `run.bat` mentioned in the architecture diagram above does not actually exist on
    disk — use `python main.py` directly for dev runs, or `build.bat` for a full
    PyInstaller rebuild (kills any running `MentorOverseer.exe` first).
  - Historical Telegram activity data from before 2026-07-01 may still show some chats
    as standalone top-level apps instead of nested under Telegram (see Telegram
    grouping fix above) — this is expected and won't self-correct for old rows.
- Python: `C:\Users\devba\AppData\Local\Programs\Python\Python311\python.exe`
- Exe: `MentorOverseer.exe` (next to config.json / plans/ / data/) — rebuild with
  `build.bat` after any source change; the compiled exe does NOT auto-update.
- Desktop shortcut: `C:\Users\devba\Desktop\Mentor-Overseer.lnk`

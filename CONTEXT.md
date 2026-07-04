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

### Settings + Score economy ✅ (2026-07-02)
- ⚙ button in sidebar header → in-app Settings dialog (working hours, reminders,
  scoring rates, score economy rates, activity keywords, idle-answer library).
  Saving updates `config.json` and restarts the activity tracker.
- Score is now a spendable, persistent balance (`score_ledger` table), credited once
  per day from the existing score formula (now actually reads `config["scoring"]`
  instead of hardcoded numbers) plus a streak bonus for consecutive fully-completed
  days.
- Sidebar SCORE section: balance, "Buy entertainment time" (spend points for a timed
  window — off-plan activity during it logs as `paid`, doesn't cost score, doesn't
  trigger focus-alert nagging), "Log spend (no regrets)" (manual expenditure,
  deducts points at a configurable rate).
- Idle-answer library: typed idle-dialog answers get matched against
  `config["idle_activity_rules"]` and reclassified from generic `idle` to
  on_plan/off_plan/neutral when they match — seeded empty, the user populates via
  Settings.
- TickTick sidebar simplified to a single "Connect TickTick" → "TickTick connected"
  control; push + completion sync now run automatically every 5 min once connected,
  no manual buttons.

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

- Last session: 2026-07-04
- What was built:
  - **Task editing**: detail panel now has an "Edit" button (`_edit_task_dialog`)
    for title/detail/duration/category. Saving (`_apply_task_edit`) rewrites the
    task in-place in `plans/active/<id>.json` and, if the title changed, migrates
    `task_completions`/`task_overrides`/`ticktick_sync` rows from the old title
    to the new one (these tables key on task text) so completion history and the
    TickTick mapping stay attached.
  - **Non-working days**: Schedule dialog now has a "Day off" button on every day
    header (`_mark_day_off`) — pushes that day's tasks, and everything assigned
    on/after it, forward by one day via the existing `task_overrides` mechanism
    (same mechanism as "Do today", just applied to the whole tail instead of one
    task). Dialog refreshes in place after each click instead of closing, so
    several consecutive days (e.g. a week off) can be marked in one sitting.
  - **Bug found + fixed: TickTick tasks not showing up.** "My Tasks (TickTick)"
    was pulling from `self.tt_project_id` — the "Netherlands Plan" project the
    app itself creates to push plan tasks into — never the user's actual personal
    TickTick lists. Confirmed live against his account: real tasks live in 7
    other projects (STUDY, NOTES, HOME, "My 2026 Goals", etc.), none of which
    were ever queried. Added `TickTickClient.get_all_tasks(exclude_project_id)`
    in `ticktick/sync.py` (loops `get_projects()`, pulls each project's tasks,
    tags each with `projectId`/`_projectName`); `_tt_autosync_cycle` now uses it
    (excluding the plan's own mirror project) to populate `self.tt_tasks`.
    Completing a "My Tasks" row now routes to the task's own `projectId`
    instead of always assuming the plan project (`_on_tt_toggle` signature
    updated to carry it through).
  - All three verified by driving the actual widgets (not reimplemented mocks):
    instantiated `MentorApp` against an isolated copy of config/plans/db, with
    autostart/tray/tracker/TickTick-autosync side effects monkeypatched out,
    then typed into the real Edit-Task Entry/Text widgets and invoked the real
    Save button, invoked the real "Day off" flow and checked the resulting
    `plan_day` shift, and called the real `get_all_tasks` live (read-only) against
    the user's actual TickTick account.
- Previous session: 2026-07-02
- What was built:
  - Bug: single-instance mutex guard (`_acquire_instance_mutex`) existed but silently
    let duplicate `MentorOverseer.exe` instances run — `windll.kernel32.GetLastError()`
    is unreliable in ctypes without `use_last_error=True`, so the "already running"
    check could read a stale/zero error code. Fixed by using `WinDLL(...,
    use_last_error=True)` + `ctypes.get_last_error()`. Verified with 20 truly-parallel
    launches (raw ctypes) and repeated exe-launch stress tests — exactly one instance
    survives every time now.
  - Bug: `self.today` was computed once at `__init__` and never refreshed — since this
    app runs continuously in the tray, staying open past midnight silently used
    yesterday's date for all "today" calculations. Converted to a `@property` that
    always reads `date.today()`.
  - TickTick reconnect flow fixes: `on_connect_ticktick` skipped the credentials dialog
    whenever *any* client_id/secret were already saved, even if stale/rotated — no way
    back in to fix bad credentials. `_tt_on_error` now reopens the setup dialog (and
    actually retries the connection after saving — the dialog's "Save & Connect" button
    previously only saved). `_exchange_code` in `ticktick/sync.py` now surfaces
    TickTick's real OAuth error text instead of a bare HTTP status line. OAuth wait
    timeout cut from 120s to 45s with a clearer status message, since a hard rejection
    at TickTick's `/authorize` (bad client_id, unregistered redirect_uri) never calls
    back to the local server at all — previously that looked like the app was hung.
  - TickTick sidebar simplified: removed the separate status line and the "Push
    Today"/"Sync Completions" buttons. Now just "Connect TickTick" → "TickTick
    connected". Once connected, `_tt_autosync_cycle()` pushes today's plan tasks and
    syncs completions both ways automatically every 5 min (`TT_AUTOSYNC_INTERVAL_MS`),
    no manual action needed.
  - Bug: `_tt_autosync_cycle`'s error handler referenced `exc` (the `except ... as exc`
    variable) inside a `lambda` passed to `root.after()` — Python unbinds `exc` when
    the except block exits, so by the time the deferred lambda ran it raised
    `NameError: cannot access free variable 'exc'` on every failed sync. Same bug fixed
    in `_on_tt_toggle`. Both now capture the message as a plain string before the
    closure.
  - **Settings moved into the app**: new ⚙ button in the sidebar header opens a
    scrollable Settings dialog (working hours, reminders, scoring rates, score economy
    rates, activity keyword lists, idle-answer library — see below). Saving writes
    `config.json` and restarts `ActivityTracker` so changes take effect immediately
    (it only reads config at construction, doesn't poll it live).
  - Found `config.json`'s `scoring` block was dead — `_week_stats()` had the formula
    hardcoded (10/-5/3/-2, no streak bonus despite `streak_bonus_per_day` existing in
    config). Extracted `_day_score()`/`_day_task_counts()`/`_day_diary_minutes()`
    helpers that actually read `config["scoring"]`, and added a real streak bonus
    (`_current_streak()`) applied to today's score only.
  - **Score monetization**: new `score_ledger` table (persistent running balance,
    never resets). Credited once/day via `fire_eod_summary()` (`_credit_day_score_if_
    missing`), with a 7-day startup catch-up (`_ensure_score_caught_up`) for days the
    app wasn't open at EOD — bounded window, doesn't retroactively credit the whole
    plan history. New sidebar SCORE section: balance + "Buy entertainment time" (spend
    points for a timed window; off-plan activity during it is tracked under a new
    `paid` diary category — excluded from the score penalty and from the off-plan
    nagging alerts — via `ActivityTracker.set_paid_until`/`_effective_class`) + "Log
    spend (no regrets)" (manual expenditure logging, deducts points). Both dialogs
    block purchases exceeding the current balance. Rates (`points_per_minute`,
    `points_per_currency_unit`, `currency_symbol`) live in `config["score"]`, editable
    via Settings.
  - Bug found + fixed while building the above: the sidebar had no scrollbar, and
    adding the SCORE section pushed content below the visible window at the default
    1060x720 size with no way to reach it. Wrapped the whole sidebar in a scrollable
    canvas (mousewheel + drag), matching the pattern already used for the Schedule/
    Settings dialogs. Re-binds mousewheel after the two sections that rebuild their
    own children (`_rebuild_plans_sidebar`, `_refresh_score_sidebar`).
  - **Idle-time activity library**: new `config["idle_activity_rules"]` block
    (on_plan/off_plan/neutral phrase lists, seeded empty — the user fills it in via the
    new Settings section). `ActivityTracker.classify_idle_text()` substring-matches a
    typed idle-dialog answer against it (same mechanism as the existing window-title
    `classify()`); `log_idle_answer()` now stores the matched category instead of
    always hardcoding `'idle'`. Diary rendering (live view + HTML export) updated to
    show the typed description for *any* category, not just `idle` — needed because a
    reclassified row still has `window='idle'` literally, which would otherwise render
    as the unhelpful literal text "idle" instead of what was typed.
  - All changes verified via `python main.py` dev-mode screenshots (sidebar layout,
    Settings dialog, live purchase flow end-to-end against the real DB, reverted after)
    plus a rebuilt/relaunched exe with a clean `mentor.log` after each fix.
- Previous session: 2026-07-01
- What was built (2026-07-01):
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
- What to build next: nothing specified — await the user's direction. Idle-answer
  library (`config["idle_activity_rules"]`) is seeded empty; the user needs to populate
  it via Settings for idle-time reclassification to do anything.
- Known issues / notes:
  - TickTick redirect URI must be registered at developer.ticktick.com: `http://localhost:8765/callback`
    — separate from the "App Service URL" field on that page, which is a different
    setting; the redirect URI specifically goes in the "OAuth redirect URL" field.
  - the user's TickTick client_id/secret were visible in a screenshot shared this session
    (2026-07-02) — same "treat as burned, rotate when convenient" situation as the
    git-history leak from 2026-06-29.
  - `tracker/__init__.py` / `ticktick/__init__.py` were missing from git for a while
    (package worked locally without them tracked) — now added.
  - `run.bat` mentioned in the architecture diagram above does not actually exist on
    disk — use `python main.py` directly for dev runs, or `build.bat` for a full
    PyInstaller rebuild (kills any running `MentorOverseer.exe` first).
  - Historical Telegram activity data from before 2026-07-01 may still show some chats
    as standalone top-level apps instead of nested under Telegram (see Telegram
    grouping fix above) — this is expected and won't self-correct for old rows.
  - `config["notifications"]`, `config["user_name"]`, and `config["plan_start_date"]`
    are dead — not read anywhere in main.py. Deliberately left out of the new Settings
    dialog rather than exposing controls that do nothing; flagged here in case the user
    wants them wired up or removed later.
  - Restarting `ActivityTracker` (Settings save, or anywhere else that calls
    `self.tracker.stop()` + `init_tracker()`) leaves the old poll thread running for
    up to ~60s until its current sleep cycle ends, briefly overlapping with the new
    one. Both hold independent SQLite connections so this is harmless, just noting it
    exists rather than treating an overlap as new gremlin behavior if it comes up.
- Python: `C:\Users\devba\AppData\Local\Programs\Python\Python311\python.exe`
- Exe: `MentorOverseer.exe` (next to config.json / plans/ / data/) — rebuild with
  `build.bat` after any source change; the compiled exe does NOT auto-update.
- Desktop shortcut: `C:\Users\devba\Desktop\Mentor-Overseer.lnk`

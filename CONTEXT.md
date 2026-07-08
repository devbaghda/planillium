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
- Active plans: Netherlands Relocation, The Complete 10-Level Claude Code Mastery Guide
  (both under `plans/active/`)

---

## App architecture

**The WinUI app (`winui/MentorOverseer.App`) is THE app.** It started as a Python/
Tkinter app (main.py), was fully rebuilt in WinUI 3/.NET 8 over 2026-07-06/07, and the
Python source was removed from the repo on 2026-07-08 once the WinUI app had shipped
everything it did (still recoverable from git history/tags if ever needed).

```
mentor-overseer/
├── CONTEXT.md              ← you are here. Read this first every session.
├── config.json             ← shared user settings, idle threshold, scoring rules (no
│                              secrets — TickTick client_secret/access_token live in
│                              Windows Credential Manager via CredentialStore, not on disk)
├── plans/
│   ├── active/             ← up to 2 active plan JSONs (e.g. netherlands.json)
│   └── archive/            ← completed plans moved here; frees a slot for a new plan
├── data/
│   ├── progress.db         ← SQLite — see Database schema below
│   ├── winui_state.json    ← window size/theme/kickoff-review state
│   └── mentor-winui.log    ← app log
├── release/                ← Inno Setup installer pipeline (release.ps1, app.iss) — see
│                              the windows-app-releaser skill for how to cut a build
└── winui/MentorOverseer.App/  ← WinUI 3 / .NET 8 source — see Tech stack below
```

---

## Tech stack
- **Framework:** WinUI 3 / .NET 8 (`net8.0-windows10.0.19041.0`), Windows App SDK 1.5.240627000
- **Build:** `dotnet build -p:Platform=x64 -c Release` from `winui/MentorOverseer.App/` —
  no Visual Studio required. Unpackaged, self-contained (`WindowsPackageType=None`,
  `WindowsAppSDKSelfContained=true`), no admin rights needed.
- **Database:** SQLite via `Microsoft.Data.Sqlite`
- **Tray:** H.NotifyIcon.WinUI — close hides to tray, tracking keeps running
- **Activity tracking:** P/Invoke `GetForegroundWindow` + `GetLastInputInfo` (Windows)
- **TickTick:** REST API (OAuth2), own `TickTickAuth`/loopback listener on 8765
- **Credentials:** Windows Credential Manager (`CredentialStore`, python-keyring-compatible
  byte format)
- **Packaging:** Inno Setup via `release/release.ps1` (see `release/README.md`)
- **App name constant:** `AppNames`, `MAX_PLANS = 2`

---

## Plan JSON format
```json
{
  "id": "kebab-case-slug",
  "name": "Plan title",
  "color": "#0a84ff",
  "start_date": "2026-06-29",
  "total_days": 160,
  "briefing": { "high_leverage": [...], "ignore_completely": "...",
                "common_time_wasters": "...", "realistic_timeline": "..." },
  "phases": [
    {
      "phase": 1,
      "name": "Phase Name",
      "tasks": [
        { "day": 1, "task": "Task title", "detail": "...", "mentor_note": "...",
          "category": "profile", "duration_min": 60 }
      ]
    }
  ]
}
```
Required fields: `id`, `name`, `phases`. `mentor_note` (per task) and `briefing`
(plan-level) are what drives the app's "mentor" UI (💡 note under a task, MENTOR'S
NOTE in Details, 📋 Briefing button) — a plan imported without them just won't show
mentor commentary; that's a content gap in the source JSON, not a bug. The "Add Plan"
wizard's 3 prompt templates (`Services/PlanTemplates.cs`) ask Claude to include both.

**Known bug (found 2026-07-08, not yet fixed):** `PlanTemplates.cs`'s "Format my own
plan" (reformat) template tells Claude to key each phase's name as `"title"`, but
`PlanModels.cs`'s `Phase` class deserializes `"name"` — any plan generated via that
specific wizard mode gets phases with a silently-empty name. Low impact today since
the Schedule page doesn't render phase names, only `Day N`, so it's not visibly broken
yet.

---

## Database schema (data/progress.db)
```sql
task_completions (id, plan_id, plan_day, task_text, completed, completed_at, last_updated)
  UNIQUE (plan_id, plan_day, task_text)
task_overrides   (plan_id, task_text, original_day, assigned_day)  PK (plan_id, task_text)
  -- reschedule / move-to-today / day-off all write here; "insert don't overlap" shift semantics
plan_days_off    (plan_id, day, marked_at)  PK (plan_id, day)
task_notes       (plan_id, task_text, note, updated_at)  PK (plan_id, task_text)
  -- personal scratchpad per task, inline-editable on Today/Schedule; empty note deletes the row
score_ledger     (id, ts, date, delta, reason, detail)
  -- UNIQUE(reason, date) for reason IN (daily_score, overdue_accrual, weekly_comeback_bonus)
reflections      (id, date UNIQUE, text)          -- evening review answers
ticktick_sync    (id, plan_day, task_text, ticktick_task_id, ticktick_proj_id, pushed_at, synced_at)
  UNIQUE (plan_day, task_text)
time_diary       (id, date, start_time, end_time, duration_min, category, window, description)
  -- category: on_plan | off_plan | neutral | idle | paid
activity_log     (id, logged_at, window, class)   -- one row per 60s poll; pruned to 90 days
diary_daily_rollup (date PK, on_min, off_min, neutral_min, paid_min, ...)
  -- written just before a day's raw time_diary rows age out (365-day retention), keeps
  -- Year-view totals accurate forever after per-entry detail is gone
```

---

## Key business rules
1. Working hours: 08:00–20:00 (configurable in config.json)
2. Diary tracking hours: 06:00–20:00
3. Reminder grace: 15 min off-plan before first alert; escalates every 5 min after
4. Idle threshold: 10 min; a wall-clock gap much larger than the poll interval is
   treated as sleep, not idle
5. Plan day: `(today − start_date).days + 1`, exclusion-aware (`excluded_weekdays`,
   `plan_days_off`)
6. Overdue: any incomplete task from a past day resurfaces; `task_overdue_penalty`
   applies once for the day-of miss (folded into that day's `daily_score`) and again
   *every subsequent day* it stays outstanding (`overdue_accrual`, capped at 3 days).
   Rescheduling doesn't refund penalties already taken.
7. Reschedule / Move-to-today / Day-off all use the same "insert, don't overlap" shift:
   whatever's already on the target day (and everything after it) shifts forward one
   day first, rather than doubling up.
8. Archive: plan moves to `plans/archive/` when ALL tasks done; frees a slot (max 2
   active plans)
9. Score floor: daily score floors at −10; a `weekly_comeback_bonus` (20 pts) rewards
   a full week back on track after a bad stretch.

---

## config.json key fields
Working hours, reminder/idle timing, `ticktick.client_id` (secret/tokens are in
Credential Manager, never here), `activity_rules` (on_plan/off_plan/neutral keyword
lists), `scoring` (task_completed/task_overdue_penalty/on_plan_hour/off_plan_hour/
streak_bonus_per_day/weekly_comeback_bonus), `score` (points_per_minute/
points_per_currency_unit/currency_symbol — the "buy entertainment time" economy),
`idle_activity_rules` (idle-dialog answer → category reclassification, the user
populates via Settings), `start_with_windows`, `appearance.theme_mode`. Edited via
the in-app Settings dialog, which writes this file and restarts the activity tracker.

---

## Project history (pre-WinUI, Python/Tkinter era — retired 2026-07-07/08)
Built 2026-06-28 through 2026-07-06 as a single-file Tkinter app (`main.py` +
`tracker/`, `ticktick/`). Delivered, in order: plan engine + daily view, TickTick
sync, activity tracker + idle detection, weekly reports, multi-plan support, a
spendable score economy, light/dark theming, a UX/accessibility remediation pass,
and a Claude-assisted plan-generation wizard. A 2026-06-29 security audit fixed
credential storage (moved to keyring/Credential Manager), SQLite thread isolation,
OAuth CSRF, and a plaintext-secret git-history leak (rewritten out via
`filter-branch`). Full blow-by-blow detail for all of this lives in git log — every
commit from that era is still there, the source itself is not. Nothing from this
period needs re-reading to work on the app today; the WinUI rebuild below ported
every feature that mattered.

## Session handoff notes
_Update this section at the end of each Claude Code session:_

- Last session: 2026-07-08, branch `winui-rebuild` (now == `master`)
- **Repo consolidation**: `master` fast-forwarded to match `winui-rebuild` (they'd
  diverged since the WinUI work started on a feature branch); stale branches fully
  contained in it (`audit-remediation`, `ux-audit-fixes`, `ui-theme-light-dark`)
  deleted locally + on origin; GitHub's default branch switched from
  `audit-remediation` → `master`. `code-refinement` kept (checked out in the
  `mentor-overseer-test/` worktree). Removed ~430MB of local build cruft (caches,
  old PyInstaller output, an unused Debug config build, stray test logs) and retired
  the Python app source entirely (see Project history above).
- **New features**: per-plan-independent scrollable schedule boxes (each plan on the
  Schedule page now scrolls in its own bounded box, auto-scrolled to its own today,
  instead of one shared list where reaching plan 2 meant scrolling past all of plan
  1); "Get a head start on tomorrow?" prompt on Today once the day's tasks are fully
  cleared (lists tomorrow's tasks with one-click "Start now", reusing
  `MoveTaskToToday`); manual "+ Add step" on Plans (`AddTaskDialog` →
  `PlanStore.AddTask`, surgical JSON patch); personal per-task notes, inline-editable
  on both Today and Schedule; a "Details" link on Schedule (was Today-only).
- **Fixes**: dark-mode live theme switching (root cause — `ThemeSync`'s
  `ThemeDictionaries`-walk silently returned `null` for every key on every call;
  replaced with hardcoded Light/Dark hex values pulled from the pinned WindowsAppSDK
  package's `generic.xaml`); Reports page bar-column alignment between "Time by App"
  and "Top Distractions" (three layered bugs, fixed in sequence); reschedule no
  longer double-books a day (now shifts, same semantics as Move-to-today/Day-off).
- Also folded in same-day work from 2026-07-07 that hadn't made it into this log:
  **v1.0.0 release** (Inno Setup installer pipeline, version surfacing in Settings +
  log header) plus overdue-reschedule dialog, editable diary entries, app icon,
  score-chip styling, latest-first diary ordering, dropped category tags from Today.
- Everything built clean (`dotnet build -p:Platform=x64 -c Release`, 0 warnings/0
  errors) and verified via `PrintWindow` screenshots against a running instance
  (`MENTOR_PAGE` env hook). Shift/reschedule logic verified by code-pattern match
  against the already-proven pattern, not click-tested — standing rule against
  simulating mouse/keyboard input on the user's live app/data.
- Previous session: 2026-07-07 — **full audit of the WinUI app, all 18 findings
  applied**: file logger (`Services/Log.cs`) wired into every previously-silent
  `catch{}`; global exception handlers + single-instance mutex in `App.xaml.cs`;
  `ActivityTracker` poll timer reentrancy fix + pauses when the Python app is
  running; `DialogGate` semaphore around every `ContentDialog` (no more
  "already an open dialog" crash); EOD-check `>=` fix + close confirmation; plan-id
  filename validation; a11y names on checkboxes; `InvariantCulture` on all DB/UI
  dates (OS locale is Russian, app language is English). Also: **one-app
  consolidation** — WinUI became the sole app (OAuth/Credential-Manager port, tray
  via H.NotifyIcon, plan import from .docx/.txt/.md/.json, full Settings editing,
  HTML report export) — the user's call: "all functionality in one place." Full detail
  for both in git log around commits `~9e0..194beec` on `winui-rebuild`.
- Open action items (carried from the Python-era audits, still not done as of
  2026-07-08): **rotate the TickTick OAuth client secret** at
  developer.ticktick.com — the old value was committed in plaintext early on and,
  separately, shown in a screenshot; both instances are burned and rotation is the
  only real fix (git-history rewrite already happened for the first leak, but
  rotation itself is still outstanding).
- Known issue: TickTick redirect URI must be registered at developer.ticktick.com
  as `http://localhost:8765/callback` in the **OAuth redirect URL** field
  specifically (not the separate "App Service URL" field on that page).

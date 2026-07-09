# Mentor-Overseer — Project Context

**Display name is "Planillium"** (renamed 2026-07-08, prepping to open-source as a
portfolio piece — detail in Session handoff notes). Repo folder, C# namespace, and
this doc's title stay `MentorOverseer` on purpose — internal, invisible to users.
Compiled exe: `Planillium.App.exe`.

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
├── CLAUDE.md               ← operating instructions (how to work here — build/verify/
│                              publish workflow, safety rules). CONTEXT.md is facts,
│                              CLAUDE.md is process; read both, keep them separate.
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
7. Reschedule / Day-off still use the "insert, don't overlap" forward shift: whatever's
   already on the target day (and everything after it) shifts forward one day first,
   rather than doubling up. **Move-to-today no longer works this way** (changed
   2026-07-09): pulling a future task to today just adds it alongside whatever's
   already there (multiple tasks per day is normal); if that empties out the task's old
   day, everything after it shifts *back* one day to close the gap instead — finishing
   something ahead of schedule compresses the remaining plan rather than leaving a dead
   day in the middle of it. **Already-completed tasks are never shifted** by any of
   these four operations (fixed 2026-07-09; see Session handoff notes — shifting a
   completed task orphaned its `task_completions` row, keyed by assigned day, silently
   unmarking it and moving it to tomorrow).
8. Archive: plan moves to `plans/archive/` when ALL tasks done; frees a slot (max 2
   active plans)
9. Score floor: daily score floors at −10; a `weekly_comeback_bonus` (20 pts) rewards
   a full week back on track after a bad stretch; a `multi_task_bonus_per_extra_task`
   (3 pts, added 2026-07-09) rewards each task completed beyond the first one on the
   same day, on top of the flat per-task rate.

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
_Update this section at the end of each Claude Code session. This is an index, not an
archive — full blow-by-blow detail for any entry lives in git log/commit messages.
Compress aggressively rather than letting this grow forever (compressed 852→224 lines on
2026-07-06; compressed again, ~230→~50 lines, on 2026-07-09)._

- **2026-07-09** (three rounds, `winui-rebuild` kept in sync with `master`, both pushed):
  Reports diary column width is now computed deterministically in code-behind from
  `RootScroller`'s `ActualWidth` (three XAML-only attempts didn't hold — see `1e7dc07`).
  Window size/position now clamped to `DisplayArea.WorkArea` at launch (was opening
  off-screen with a stale saved size). Added a zero-padded (`dd.MM.yyyy`)
  `CalendarDatePicker` to jump to a diary date. Fixed a real data-loss bug: shifting an
  already-completed task's assigned day (move-to-today/reschedule/day-off) orphaned its
  `task_completions` row, silently un-marking it — completed tasks are now excluded from
  every such shift (business rule 7). Replaced move-to-today's forward-shift with backward
  compaction — pulling a future task to today now closes the gap it leaves instead of
  abandoning a dead day. "Get a head start on tomorrow?" now offers after *any* task today
  is done, not only after all of them. Added a `multi_task_bonus_per_extra_task` scoring
  rate. One live-data repair applied directly (`task_overrides` for
  `claude-code-10-level-mastery`, 20 rows), only after the user's explicit reviewed
  confirmation — see Standing lessons below. Deleted two stale `CLAUDE_HANDOFF.md` files
  another tool left behind (already-merged rename instructions). Pulled the
  testing/architecture lessons from this session into the `windows-app-tester` and
  `windows-app-auditor` global skills (UIA-over-screenshots, `PrintWindow` flag 2,
  reorder-shift-vs-completion-keying).
- **2026-07-08**: Full 5-category audit + remediation (`2a7b31f`). Critical: TickTick
  secret was in git history twice, rewritten out via `filter-branch` (the user still needs to
  **rotate the actual secret** — history rewrite doesn't invalidate the value, see Open
  TODOs). High: first-run activity-tracking disclosure added. 8 Medium/4 Low fixed
  (activity_log retention, `ReportsPage.Render` decomposed, SQLite lock-error surfacing,
  shared `HttpClient`, in-app data export/clear-history, dead Python-detection code
  removed). Renamed app to "Planillium" (display/build identity only — repo folder/C#
  namespace stay `MentorOverseer`); caught a `CredentialStore` target-name bug before it
  could silently orphan the stored TickTick token. Repo consolidated: `master`
  fast-forwarded to `winui-rebuild`, stale branches removed, ~430MB build cruft removed,
  Python source retired.
- **2026-07-07**: Full audit of the freshly-rebuilt WinUI app, all 18 findings applied
  (global exception handlers, single-instance mutex, `DialogGate` semaphore,
  `InvariantCulture` everywhere — OS locale is Russian, app language English). WinUI became
  the sole app (Python retired the next day). v1.0.0 shipped (Inno Setup installer).
- **Standing lessons** (apply every session, not just the one that taught them):
  - Check `end_of_day_summary_time` before killing a live instance near EOD — killing it
    early can skip the evening-review popup entirely for that day.
  - The live instance usually runs from `bin\x64\Release\...`, not Debug — confirm via
    `wmic process where "ProcessId=X" get ExecutablePath` before trusting a Debug rebuild
    as verification.
  - Never simulate input (clicks/keystrokes) that would mutate the user's real plan/score
    data — verify data-mutating logic by code inspection + a clean build, not by clicking
    it live. Any direct write to `data/progress.db` outside the app's own code path needs
    the user's explicit confirmation naming the specific table/change first — the harness's
    auto-mode classifier enforces this and will block an unnamed attempt.
  - `CopyFromScreen`/GDI `BitBlt` doesn't capture WinUI3 Mica/DirectComposition content —
    use `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`). For exact layout comparisons,
    UI Automation `BoundingRectangle` beats pixel-diffing screenshots.
- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **Rotate the TickTick OAuth client secret** at developer.ticktick.com — committed in
    plaintext twice historically; the repo rewrite removed it from git, not from validity.
  - `PlanTemplates.cs`'s "Format my own plan" wizard mode still tells Claude to key each
    phase as `"title"`, but `PlanModels.cs`'s `Phase` deserializes `"name"` — phases from
    that specific wizard mode get a silently-empty name (low impact: Schedule only renders
    `Day N`, not phase names).
  - **Scrub personal data before making the repo public**: `plans/active/netherlands.json`,
    `config.json` keywords, and this file's own "The user" section all name the user directly.
    The GitHub repo is currently **Private** (confirmed 2026-07-09), so there's no live
    exposure today, but this is still an unresolved pre-publish step, not a completed one.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field specifically (not
    "App Service URL").

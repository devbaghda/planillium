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
- **Framework:** WinUI 3 / .NET 8 (`net8.0-windows10.0.19041.0`), Windows App SDK 1.8.260529003
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
diary_daily_rollup (date PK, on_min, off_min, neutral_min, paid_min, ...)
  -- written just before a day's raw time_diary rows age out (90-day retention, user-
  -- configurable in Settings), keeps
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
7. **the user's steady-state rule: one task per day.** A day holding two tasks is only ever
   a transient fact ("I did two things today"), never a permanent state a scheduling
   action should create. This is *why* Reschedule/Day-off and Move-to-today deliberately
   behave differently (clarified with the user 2026-07-09, after an audit flagged the
   difference as a possible inconsistency — it isn't one):
   - **Reschedule / Day-off** use the "insert, don't overlap" forward shift: whatever's
     already on the target day (and everything after it) shifts forward one day first,
     rather than doubling up — because these are "place this specific task on this
     specific day" actions, and the one-task-per-day rule must hold going forward.
   - **Move-to-today** does *not* shift forward (changed 2026-07-09): pulling a future
     task to today just adds it alongside today's own task (a deliberate, transient
     exception to the rule — you really did finish two things today). If that empties
     out the task's old day, everything after it shifts *back* one day to close the gap
     — finishing something ahead of schedule compresses the remaining plan back down to
     one-task-per-day, rather than leaving a dead day in the middle of it.
   - **Already-completed tasks are never shifted** by any of these four operations
     (fixed 2026-07-09; see Session handoff notes — shifting a completed task orphaned
     its `task_completions` row, keyed by assigned day, silently
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

- **2026-07-07 to 2026-07-13**: Five audit rounds (1-4 numbered; round 1 folded into round 2's
  count) plus feature work, all on the freshly-rebuilt WinUI app. **07-07**: initial audit, all
  18 findings applied (exception handlers, single-instance mutex, `DialogGate`, `InvariantCulture`
  throughout); v1.0.0 shipped. **07-08**: Critical — TickTick secret was in git history twice,
  purged via `filter-branch` (rotated 07-09); 8M/4L fixed; app renamed "Planillium" (display only).
  **07-09** (3 rounds): diary column width fix, window-clamp-to-monitor, data-loss fix for shifting
  a completed task's day (business rule 7), move-to-today → backward compaction,
  `multi_task_bonus_per_extra_task` added. **07-13**: calendar-period reports, day-off pause,
  "where have you been" any-hour, tray-toast routing → round-1 audit (4H/15M/18L/4I) → merged with
  a long-unmerged branch → fixed (`4014b00`) → round-2 audit (1 new High: `StartEodWatcher` could
  wedge `DialogGate`, fixed via `ReviewDialog.Trigger`; 14M/13L,
  https://claude.ai/code/artifact/1f5b15bb-c6d5-4ea0-a1db-a46b984db19e). `config.json`/
  `plans/active/*.json` gitignored + untracked (commit `9be8515`, history NOT purged — Open TODOs).
- **2026-07-14**: All 27 round-2 findings fixed (`1724e14`); added `ScoreServiceScoringTests.cs`
  and `Dialogs/PromptRouter.cs`. **Round 3**: 1 new High (`ReviewDialog` missing `KickoffDialog`'s
  reentrancy guard, fixed; report https://claude.ai/code/artifact/8bcbfc6d-c998-4084-840c-23e8641483c2).
  Added per-task "Reschedule…" to every open task, `Dialogs/ReplanOverdueDialog` (per-task
  date-pickers replacing automatic spread), Plans page original-due-date + drift. **Same day**:
  WindowsAppSDK bumped `1.5.240627000`→`1.8.260529003`; God-Object split (`MainWindow.xaml.cs`
  633→238, `ReportsPage.xaml.cs` 1069→255, plain partial-class file splits, zero logic change);
  sidebar plan-drift note added (`PlanDriftPanel`).
- **2026-07-14 (round 4)**: 0 Critical/High, 8M/14L/9I
  (https://claude.ai/code/artifact/edcb4afc-aea5-429d-bb30-4889c3b04ba6). **All 22 fixed same
  session**: `Plan.DriftDays(tasks)` unified (sidebar+Plans were 2 copies); `RefreshScore()` now
  called after every schedule-mutating action; `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 pinned
  (CVE-2025-6965); Reports' "SCHEDULE DRIFT"→"EXCLUSION IMPACT" rename; `ActivityTracker.PollOnce`
  split into 5 named methods; `DateExtensions.ToIsoDate()` added (replaced 26 hand-typed copies);
  `PlanScoreAction`/`TaskDetailsLink` dedup helpers; TickTick Connect dialog stopped
  pre-filling the saved secret.
- **2026-07-14 (round 5)**: 0 Critical, 3H/17M/13L/8I
  (https://claude.ai/code/artifact/e54a2317-7406-49ab-bc77-7607b9920860). All rounds 1-4 fixes
  held. **All 39 fixable findings applied same session** (2 deferred: TickTick client_id in
  unpurged git history — already tracked; `PlanDayForDate`'s O(days) walk — auditor said profile
  first, not acted on). The 3 Highs: (1) `SplitDiaryEntryDialog` deleted the original diary entry
  before reinserting replacements with no SQL transaction — real data loss on a mid-loop failure;
  (2) `KickoffDialog.ShowAsync` never actually checked its own `_showing` guard despite the doc
  comment saying that's its job — mirror of round-3's `ReviewDialog` gap, could double-queue the
  morning dialog; (3) `ReportData.cs:91` was missing `InvariantCulture` on a date parse, 164 lines
  from its own already-correct sibling — the exact locale-bug class this app already shipped once.
  Fix highlights: `Database.RunInTransaction`/`CreateCommand` (transaction-aware wrapper shared
  with `ScoreService`) now wraps every multi-step schedule write; `DateExtensions.ToIsoTimestamp()`
  added alongside `ToIsoDate()`; `TickTickSection.LoadAsync` gained a generation-token guard;
  `PlanScoreAction.Run`'s callback now carries a success bool so Day-off/Move-to-today/Start-now
  can surface failures like task checkboxes already do; `ReportsPage.Styling.cs` gained one shared
  `CategoryBrushKey()` (Diary and Time-by-App had silently opposite Paid/Neutral colors) and
  `AppUsage` gained an `Idle` bucket; `ReviewDialog.ShowCore` (187 lines) split into
  `ReconcilePendingGap`/`ComputeReviewStats`/`BuildReviewPanel`/`PersistReview`; `TodayPage`'s
  get-ahead rule extracted to `GetAheadEligibility`; shared `Views/EmptyPlansState.cs` replaces
  Today/Schedule's independently-drifted empty states; `AssignedTask.OverdueCaption()` replaces a
  duplicated string; `ActivityTracker.ExeAppNames` gained Teams (was missing vs.
  `AppNames.Messengers`); `MENTOR_PAGE`/`MENTOR_INSTANCE_SUFFIX` now `#if DEBUG`-gated
  (`MENTOR_ROOT` deliberately left alone — real user-facing recovery mechanism, not a debug hook);
  `Log.cs` capped at 5MB; `TickTickAuth` logs only `error`/`error_description`, never the raw
  response body. Test project needed 2 more pins beyond SQLite (`System.Net.Http`/
  `System.Text.RegularExpressions` — only its plain `net8.0` TFM pulled these, not the main app's
  `net8.0-windows` one). Verified: full Debug+Release build, all 10 tests, `dotnet list package
  --vulnerable` clean on both projects, live UIA pass across all 5 pages.
- **Standing lesson**: any "show a prompt on a timer" trigger needs BOTH the
  `IsOnScreen()`-check-then-toast-fallback pattern AND an "already showing, don't reopen" guard —
  `StartEodWatcher` was missing the first in round 2, `ReviewDialog` was missing the second in
  round 3, `KickoffDialog` was missing it too (opposite direction) in round 5: three rounds
  running on the same lesson — when one dialog has a safety pattern a sibling doesn't, grep the
  *whole* pattern across every sibling before calling it done, and re-check on the *next* audit
  too, since a fix on one sibling doesn't inoculate the other from ever drifting back out of sync.
  Also: subscribe to `AppNotificationManager` events *before* `.Register()` (reverse order throws
  at the WinRT layer). Also (round 5): a dedup helper (e.g. `ToIsoDate()`) needs a second pass to
  check *adjacent* formats (e.g. timestamps) didn't get left uncovered by the same helper. Also
  (round 5): a "fix one sibling, miss the other" gap can hide across *projects* too, not just
  files — the test project's `.csproj` needed the identical CVE pin the main app's already had.
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
  - `Plan.PlanDayForDate`/`DateForPlanDay`'s day-by-day walk for plans with excluded weekdays is
    O(days-elapsed), called on nearly every render/click (round-5 audit finding #28, deferred —
    profile before prioritizing per the auditor's own note; no evidence yet it's actually slow).
  - ~~Rotate the TickTick OAuth client secret~~ — **the user confirmed done 2026-07-09**
    (rotated at developer.ticktick.com). One follow-up remains, not yet done: the app's
    Windows Credential Manager entry still holds the *old* secret until the user reconnects
    TickTick from Settings (disconnect → "Connect TickTick" → re-auth writes the new value
    via `TickTickAuth.SaveClientSecret`) — until then, TickTick sync will fail with an
    auth error using the now-invalid old secret.
  - `PlanTemplates.cs`'s "Format my own plan" wizard mode still tells Claude to key each
    phase as `"title"`, but `PlanModels.cs`'s `Phase` deserializes `"name"` — phases from
    that specific wizard mode get a silently-empty name (low impact: Schedule only renders
    `Day N`, not phase names).
  - **Scrub personal data before making the repo public**: `config.json` and
    `plans/active/*.json` were gitignored + untracked going forward on 2026-07-13 (commit
    `9be8515`), and this file's own "The user" section still names the user directly. The GitHub
    repo is currently **Private** (confirmed 2026-07-09), so there's no live exposure today,
    but **history still holds the real files in every past commit** — untracking only stops
    new copies, it doesn't purge old ones. Full purge (if wanted before any public push) needs
    `git filter-repo` across every branch + a force-push of each, a deliberately separate,
    bigger step from the untracking done tonight.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field specifically (not
    "App Service URL").

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
     its `task_completions` row, keyed by assigned day, silently unmarking it and
     moving it to tomorrow).
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
2026-07-06; ~230→~50 lines on 2026-07-09; rounds 1-5 condensed 2026-07-15; three 07-15 turns
condensed same evening; rounds 1-6 + all 07-15/07-16 entries condensed into one paragraph each
on 2026-07-17 after the round-7 audit)._

- **2026-07-07 to 07-09, WinUI rebuild + hardening**: shipped v1.0.0 (07-07, 18 findings fixed).
  TickTick secret purged from git history via `filter-branch` + rotated (07-08/09 — Credential
  Manager entry still needs the user to reconnect TickTick from Settings to pick up the new secret,
  see Open TODOs). App renamed "Planillium" (display only).
- **2026-07-09 to 07-15, audit rounds 1-6** (full detail in git log; artifact links kept): Rounds
  1-2 (https://claude.ai/code/artifact/1f5b15bb-c6d5-4ea0-a1db-a46b984db19e): diary column width,
  window-clamp-to-monitor, completed-task-shift data loss fixed (business rule 7), move-to-today
  backward compaction, `multi_task_bonus_per_extra_task`, `config.json`/`plans/active/*.json`
  gitignored (history NOT purged — Open TODOs). Round 3
  (https://claude.ai/code/artifact/8bcbfc6d-c998-4084-840c-23e8641483c2): `ReviewDialog`
  reentrancy guard, per-task Reschedule, God-Object file splits. Round 4
  (https://claude.ai/code/artifact/edcb4afc-aea5-429d-bb30-4889c3b04ba6): `Plan.DriftDays`
  unified, SQLite CVE-2025-6965 pin. Round 5
  (https://claude.ai/code/artifact/e54a2317-7406-49ab-bc77-7607b9920860): introduced
  `Database.RunInTransaction`/`CreateCommand` and `DateExtensions.ToIsoTimestamp()`, the two
  mechanisms every later round keys off. Round 6
  (https://claude.ai/code/artifact/4575736f-87f3-4926-b233-6135aaac0530, 07-14): 4H/10M/11L/8I,
  remediated 07-15 — `PlanStore.IsValidPlanId` unified; several dialogs/pages made transactional
  or return real success/failure (`RescheduleTaskDialog`/`AddTaskDialog`/`ReplanOverdueDialog`/
  `EditDiaryEntryDialog` → `bool?`, surfaced via each page's new `SaveErrorBar`); new
  `DateExtensions.ToIsoTimeOfDay()`/`ToDisplayDate()` and `Services/JsonFileIO.cs` (atomic
  writes); Reports' period selector unified to one `RadioButtons` group. All rounds verified with
  full Debug+Release build + tests + `dotnet list package --vulnerable` + live UIA pass.
- **2026-07-15, three same-day turns** (full detail in git log): (1) `ReviewDialog._offeredOn`
  was set by *any* `ShowCore` call (not just the automatic path), so a manual "Evening review"
  click burned the day's one automatic EOD offer — fixed by moving the flag-set into `Trigger()`.
  Added a 30s live-refresh timer to the Reports diary section. Investigated (not conclusively
  root-caused) a missing "where were you" prompt after an overnight gap — added `Log.Info`
  diagnostics at the sleep-gap/idle-return decision points (**open TODO: watch the log next
  occurrence**). (2) Skills housekeeping (reconciled canonical `Desktop/CLAUDE/skills` vs
  deployed `~/.claude/skills`, added global skill `knowledge-upkeep`) + completed round-6
  remediation. **Found live** (a the user screenshot of overlapping diary rows):
  `ActivityTracker.HandleActiveSession`'s idle-detected branch closed the outgoing session
  through "now" instead of the real idle-start instant, double-counting up to 10 min of
  on/off-plan time against idle time for as long as idle detection has existed — fixed by
  computing idle-start once, shared with `_idleSince`. **Open TODO**: the already-written
  overlapping rows this caused in `data/progress.db` (e.g. on-plan 11:41→11:51 inside idle
  "Break" 11:40→11:55) are still uncorrected, pending the user's explicit go-ahead naming the
  specific rows. (3) Fixed all three "Add Plan" wizard templates keying phases as `"title"`
  instead of `"name"` (every wizard mode silently dropped phase names on import); idle-return
  dialog now shows the actual clock time range; sidebar gained a "Finishes dd.MM.yyyy" line
  (`Plan.CurrentEndDate`). All three turns verified with full build+test+live relaunch+UIA.
- **2026-07-16, reschedule/day-off shifting bugs** (full detail in git log): Fixed three related
  bugs. `RescheduleTaskDialog`/`ReplanOverdueDialog` allowed only tomorrow-or-later as a
  reschedule target, so an overdue task could never be moved to *today* (fixed: `MinDate` now
  uses `plan.PlanDay`). Root cause shared by two more reports: `MarkDayOff`/`UnmarkDayOff`/
  `RescheduleTask`/`MoveTaskToToday` shifted tasks by a blind `±1` with no awareness that another
  day might already be marked off, so a shift could land a task directly on a day-off day while
  leaving a plain working day looking empty — fixed with `NextWorkingDay`/`PrevWorkingDay`
  helpers (skip over `plan_days_off` entries) used at all four shift sites. 4 new regression
  tests. Verified with full build+test+live relaunch.
- **2026-07-17, full 5-category audit + remediation** (full detail in git log/commit messages):
  Ran all 5 `windows-app-auditor` passes in parallel (architecture/security/UX/code-quality/
  privacy). Fixed 1 High (an unobserved-`Task` risk on the toast-notification click-through,
  mirroring a bug class already fixed elsewhere — `MainWindow.xaml.cs`), 9 Medium, and ~15
  Low/Info. Highlights: `StateService.Save` now uses the atomic-write helper; the tray pill and
  Reports page now read activity-category colors from one shared `Services/CategoryStyle.cs`
  table instead of two independently-drifted copies (the "fix one sibling, miss the other"
  pattern recurring a 4th time — see Standing lessons); `TreatWarningsAsErrors` turned on so a
  plain build now enforces the zero-warnings bar a manual `/warnaserror` flag used to;
  TickTick's connect dialog now discloses what data syncs before you connect it; `MANUAL.md`'s
  privacy section rewritten to actually describe window-title tracking/retention/export; a new
  **"Clear all my data" Settings action** wipes every remaining table (task_completions/
  overrides/day-offs/notes/score_ledger/ticktick_sync) plus the debug log — previously only
  activity-history and reflections had any delete path at all; Settings' hours/reminders/
  keyword-list fields now autosave like the rest of the page instead of needing an explicit
  "Save settings" click; `DiaryCategory`/`MessengerApps` shared-constant classes replaced
  scattered string literals across 11+2 files; `ReportData.Buckets` split into
  `MonthBuckets`/`YearBuckets`; a few functions had their pure data-decisions pulled out of
  UI-building code (`TodayPage.BuildPlanView`, `ReportExport.GatherWeekReportData`,
  `TickTickAuth.TryParseCallbackRequest`, `IdleReturnDialog.BuildSegments`) — `ReportsPage.
  Diary.BuildDiarySection` and a few other verbose-but-already-data-separated UI builders
  (`PlansPage.PlanCard`, `ReviewDialog.BuildReviewPanel`, `ReportsPage.TimeByApp.
  AppBreakdownPanel`) were deliberately left unsplit after inspection showed no real hidden
  business-logic-in-UI risk, only inherent WinUI UI-construction verbosity, or (BuildDiarySection,
  IdleReturnDialog) genuine shared-closure-state across UI modes. Both projects confirmed clean
  via `dotnet list package --vulnerable`. Verified with full Debug+Release build + all 14 tests +
  live Release relaunch + log check. Deferred (see Open TODOs): git-history purge, keyboard/
  dark-mode/timing items that need a live human check, not a code change.
- **Standing lessons** (apply every session, not just the one that taught them):
  - The "fix one sibling, miss the other" class hit a 4th shape 2026-07-17: a shared color table
    (`ReportsPage.Styling.CategoryBrushKey`) was introduced round-5 specifically to stop two
    Reports views from disagreeing on activity-category colors — but the tray status pill
    (`MainWindow.Tracker.UpdatePill`) kept its own third, independent copy that was never migrated,
    and the doc comment claiming "both now read from this one table" went uncorrected for two
    rounds. When centralizing a duplicated value/table, grep for *every* call site across the
    whole app (not just the two the original bug report named) before writing a doc comment that
    claims it's now unified.
  - Any "show a prompt on a timer" trigger needs BOTH the `IsOnScreen()`-check-then-toast-fallback
    pattern AND an "already showing, don't reopen" guard — confirmed missing on three different
    dialogs across three different rounds (`StartEodWatcher` round 2, `ReviewDialog` round 3,
    `KickoffDialog` round 5). When one dialog has a safety pattern a sibling doesn't, grep the
    *whole* pattern across every sibling before calling it done, and re-check on the *next* audit
    too — a fix on one sibling doesn't inoculate the other from drifting back out of sync. This
    "fix one sibling, miss the other" class has also shown up across *projects* (a CVE pin present
    in the main app's `.csproj` but missing from the test project's), and — a new shape found
    2026-07-15 — between a prompt template and its own deserialization model (see
    `windows-app-auditor`'s UX reference). A once-per-day "don't re-offer" flag must only be set
    by the *automatic* trigger path — if a manual preview button shares the same show-and-persist
    function as the automatic watcher, the manual path silently burns the automatic offer with no
    error to catch it on.
  - Subscribe to `AppNotificationManager` events *before* `.Register()` (reverse order throws at
    the WinRT layer). A dedup helper (e.g. `ToIsoDate()`) needs a second pass to check *adjacent*
    formats (e.g. timestamps) didn't get left uncovered by the same helper.
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
  - When a background poll first *notices* a state change (idle crossing a threshold, a timer
    tick), the poll's own timestamp is not the same as when that state change actually happened —
    it can lag by up to the full poll/threshold interval. Log/close out events at the
    back-computed real moment, not at "now," or two records meant to be back-to-back end up
    overlapping instead (see the 2026-07-15 idle-transition overlap bug below).
- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **The two overlapping diary rows the user spotted 2026-07-15 (on-plan 11:41→11:51 inside idle
    "Break" 11:40→11:55) are still sitting in `data/progress.db` uncorrected** — the bug that
    caused them is fixed, but fixing already-written rows is a direct DB edit and needs the user's
    explicit confirmation naming the specific rows first (this file's Direct-database-access
    rule). There may be older instances of the same overlap further back in the diary too, not
    audited.
  - `Plan.PlanDayForDate`/`DateForPlanDay`'s day-by-day walk for plans with excluded weekdays is
    O(days-elapsed), called on nearly every render/click (round-5 audit finding #28, deferred —
    profile before prioritizing per the auditor's own note; no evidence yet it's actually slow).
  - ~~Rotate the TickTick OAuth client secret~~ — **the user confirmed done 2026-07-09**
    (rotated at developer.ticktick.com). One follow-up remains, not yet done: the app's
    Windows Credential Manager entry still holds the *old* secret until the user reconnects
    TickTick from Settings (disconnect → "Connect TickTick" → re-auth writes the new value
    via `TickTickAuth.SaveClientSecret`) — until then, TickTick sync will fail with an
    auth error using the now-invalid old secret.
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

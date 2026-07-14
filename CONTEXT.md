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

- **2026-07-13**: Six feature changes (calendar-period reports; day-off pause, confirmed
  *recurring excluded weekdays only*; "where have you been" any hour + evening-review gap
  sweep; tray-toast routing via new `Services/ToastNotifier.cs`) → round-1 audit (4H/15M/18L/4I,
  https://claude.ai/code/artifact/7cb9538b-4859-4eea-ac1a-396b6f722282) → merged with the
  long-unmerged `audit-fixes-2026-07-09` branch → remediation commit `4014b00` (both round-1
  Highs + 9M/9L) → round-2 audit (1 new High: `StartEodWatcher` could wedge `DialogGate` by
  opening `ReviewDialog` while hidden to tray, fixed same session via `ReviewDialog.Trigger`
  mirroring `KickoffDialog.Trigger`; 14M/13L, https://claude.ai/code/artifact/1f5b15bb-c6d5-4ea0-a1db-a46b984db19e).
  Also: `config.json`/`plans/active/*.json` gitignored + untracked going forward (commit
  `9be8515`, history NOT purged — see Open TODOs); finished the incomplete 2026-07-09 secret
  cleanup (`git gc`, 0 dangling objects).
- **2026-07-14**: Applied all 27 round-2 findings, deferred 2 (God-Object decomposition,
  WindowsAppSDK bump) — commit `1724e14`. Added `ScoreServiceScoringTests.cs` (`ComputeDayScore`
  had zero coverage) and `Dialogs/PromptRouter.cs` (extracted show-dialog-or-toast routing 3
  dialogs duplicated). **Round-3 audit** found all 27 fixes held + **1 new High**: `ReviewDialog`
  never got `KickoffDialog`'s "already showing, don't reopen" guard, so leaving it open past
  one EOD-watcher tick (1 min) could queue a duplicate — fixed via the same `_showing`/`ShowCore`
  split, enforced in both `Trigger` and `ShowAsync` since `TodayPage`'s manual Review button
  bypasses `Trigger` (report: https://claude.ai/code/artifact/8bcbfc6d-c998-4084-840c-23e8641483c2).
  Mid-round, the user requested two scheduling features (verified live via UIA): (1) "Reschedule…"
  (pick any day; that day + everything after shifts forward one) now on every open task, not
  just overdue — `SchedulePage.xaml.cs`. (2) "Replan all overdue" is no longer automatic; new
  `Dialogs/ReplanOverdueDialog` shows one date-picker per overdue task via
  `ScoreService.ReplanOverdueTo` (removed unused `ReplanAllOverdue`/`ReplanDailyBudgetMin`).
  Plans page now shows each plan's originally-due date + drift days — `PlansPage.xaml.cs`.
  **Later the same day**, the user asked to clear both deferred items. WindowsAppSDK
  `1.5.240627000`→`1.8.260529003` (+ `Microsoft.Windows.SDK.BuildTools` `1742`→`4654`, a
  transitive floor from the new WindowsAppSDK.Base) — release-notes review first found no
  breaking changes applicable to this app (unpackaged, no MSIX/wapproj, no custom FontFamily
  weight/stretch); behavior changes are opt-in via `RuntimeCompatibilityOptions`. God-Object
  decomposition: split `MainWindow.xaml.cs` (633→238) into `+Tray/+NativeInterop/+Startup/
  +Tracker.cs` and `ReportsPage.xaml.cs` (1069→255) into `+Tables/+TimeByApp/+Diary/+Styling.cs`
  — plain C# partial-class file splits along the file's own existing section comments, zero
  logic changes, chosen because the original deferral reason was regression risk and a pure
  file-move carries none; the Diary split was extracted byte-for-byte via `git show HEAD:...` +
  `sed` rather than retyped, since it contains literal (non-`\u`-escaped) Segoe Fluent Icons
  codepoints invisible in any terminal/diff and easy to corrupt by hand. Both verified with a
  full Debug+Release build, all 10 tests, and a live UIA pass across every page. Also added a
  short plan-drift note in the sidebar (`PlanDriftPanel`/`MainWindow.RefreshPlanDrift()`)
  mirroring the Plans page's own readout — refined moments later per the user's request to show
  a status line for *every* active plan, not just off-track ones: green "On track"/"Nd ahead
  of plan" or red "Nd late from plan".
- **2026-07-14 (round 4)**: Full 5-pass independent audit right after the SDK bump/decomposition/
  sidebar work above, plus a same-day UX refinement (sidebar plan block is now name-on-top +
  larger colored status line below, spacing between plans — commit `481c95b`). 0 Critical/High,
  8 Medium, 14 Low, 9 Info; report: https://claude.ai/code/artifact/edcb4afc-aea5-429d-bb30-4889c3b04ba6.
  **All 22 Medium/Low findings fixed same session** ("fix all"), verified with a full Debug+Release
  build, all 10 tests, `dotnet list package --vulnerable` (clean), and a live UIA pass. Key fixes:
  (1) `Plan.DriftDays(tasks)` is now the one shared formula the sidebar and Plans page both call,
  replacing two independently-typed copies; (2) `MainWindow.RefreshScore()` (which also refreshes
  the sidebar drift note) is now called after Schedule's day-off toggle, move-to-today, and
  reschedule, and after Plans' excluded-weekdays editor — previously none of those four refreshed
  it; (3) pinned `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 directly (overrides the older transitive
  version `Microsoft.Data.Sqlite` 8.0.6 pulls), clearing CVE-2025-6965; (4) Reports' drift-shaped
  card renamed `DriftCard`→`ExclusionImpactCard`, heading "SCHEDULE DRIFT"→"EXCLUSION IMPACT", so
  it no longer reads as contradicting the sidebar/Plans "drift" status it measures differently
  from; (5) sidebar plan name now has a tooltip, sits in a chip matching the pill/score-chip above
  it, and Plans page's on-track color now matches the sidebar's (green). Also: decomposed
  `ActivityTracker.PollOnce` (~130 tangled lines) into 5 named methods (`HandleSessionLock`,
  `HandleSleepGap`, `HandleIdleReturn`, `HandleActiveSession`, `HandleOutsideDiaryHours`) —
  behavior-preserving, verified line-by-line against the original control flow; added
  `Services/DateExtensions.cs`'s `ToIsoDate()` replacing 26 hand-typed copies of the date-key
  format string across 8 files (needed a `<Compile Include>` link added to the Tests project too);
  `Pages/PlanScoreAction.cs` and `Pages/TaskDetailsLink.cs` collapse three more duplicated
  boilerplate shapes (Today/Schedule score-mutating actions; the "Details" button). TickTick
  Connect dialog no longer pre-fills the saved secret into a revealable password box; the
  installer's default `config.default.json` no longer bakes in the user's real TickTick client ID.
  Confirmed clean (no fix needed): the partial-class decomposition is byte-for-byte identical to
  pre-split, no Critical/High in the app's own code, no auto-updater/telemetry, all SQL
  parameterized, memory-protection flags on in the built exe.
- **Standing lesson**: any "show a prompt on a timer" trigger needs BOTH (a) the
  `IsOnScreen()`-check-then-toast-fallback pattern, and (b) an "already showing, don't
  reopen" guard — `StartEodWatcher` was missing (a) in round 2, `ReviewDialog` was missing
  (b) in round 3, same underlying lesson two rounds running: when one dialog (kickoff) has a
  safety pattern another sibling (review) doesn't, grep for the *whole* pattern — not just
  the piece the current bug report mentions — across every sibling before calling it done.
  Also: subscribe to any `AppNotificationManager` event *before* calling `.Register()` — the
  reverse order throws at the WinRT layer for this unpackaged app.
- **2026-07-09** (three rounds): diary column width now computed from `RootScroller.ActualWidth`
  in code-behind (XAML-only attempts didn't hold, `1e7dc07`); window clamped to
  `DisplayArea.WorkArea` at launch; zero-padded `CalendarDatePicker` added to jump to a diary
  date; fixed a data-loss bug where shifting an already-completed task's day orphaned its
  `task_completions` row (completed tasks now excluded from every such shift, business rule 7);
  move-to-today changed from forward-shift to backward compaction; "head start on tomorrow?"
  now offers after *any* task today is done; added `multi_task_bonus_per_extra_task`; one
  live-data repair applied directly (`task_overrides`, `claude-code-10-level-mastery`, 20 rows)
  only after the user's explicit reviewed confirmation; pulled the session's testing/architecture
  lessons into the `windows-app-tester`/`windows-app-auditor` global skills.
- **2026-07-08**: Full 5-category audit + remediation (`2a7b31f`). Critical: TickTick secret
  was in git history twice, rewritten out via `filter-branch` (secret itself rotated
  2026-07-09). High: first-run activity-tracking disclosure added. 8M/4L fixed (retention,
  `ReportsPage.Render` decomposed, SQLite lock-error surfacing, shared `HttpClient`, in-app
  export/clear-history, dead Python-detection code removed). Renamed app to "Planillium"
  (display/build identity only — repo/namespace stay `MentorOverseer`); caught a
  `CredentialStore` target-name bug before it could orphan the stored TickTick token; repo
  consolidated (`master` fast-forwarded to `winui-rebuild`, ~430MB cruft removed, Python retired).
- **2026-07-07**: Full audit of the freshly-rebuilt WinUI app, all 18 findings applied (global
  exception handlers, single-instance mutex, `DialogGate` semaphore, `InvariantCulture`
  everywhere — OS locale Russian, app language English). WinUI became the sole app; v1.0.0
  shipped (Inno Setup installer).
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

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
2026-07-06; ~230→~50 lines on 2026-07-09; rounds 1-5 condensed into one paragraph on 2026-07-15)._

- **2026-07-07 to 07-14, rounds 1-5** (full detail in git log; each round's artifact link kept for
  reference): WinUI rebuild shipped v1.0.0 (07-07, 18 findings fixed: exception handlers,
  single-instance mutex, `DialogGate`, `InvariantCulture` throughout). TickTick secret found
  twice in git history, purged via `filter-branch`, rotated (07-08/09); app renamed "Planillium"
  (display only). Rounds 1-2 (4H/15M/18L/4I →
  https://claude.ai/code/artifact/1f5b15bb-c6d5-4ea0-a1db-a46b984db19e, 1H/14M/13L): diary
  column width, window-clamp-to-monitor, completed-task-shift data loss fixed (business rule 7),
  move-to-today backward compaction, `multi_task_bonus_per_extra_task`, `StartEodWatcher`/
  `DialogGate` wedge fix, `config.json`/`plans/active/*.json` gitignored (history NOT purged —
  Open TODOs). Round 3 (1H, https://claude.ai/code/artifact/8bcbfc6d-c998-4084-840c-23e8641483c2):
  `ReviewDialog` missing `KickoffDialog`'s reentrancy guard; per-task Reschedule +
  `ReplanOverdueDialog`; WindowsAppSDK → 1.8.260529003; God-Object file splits (`MainWindow.xaml.cs`,
  `ReportsPage.xaml.cs`). Round 4 (0H, 8M/14L/9I,
  https://claude.ai/code/artifact/edcb4afc-aea5-429d-bb30-4889c3b04ba6): `Plan.DriftDays` unified,
  SQLite CVE-2025-6965 pin, `ActivityTracker.PollOnce` split into 5 methods,
  `DateExtensions.ToIsoDate()`. Round 5 (3H/17M/13L/8I,
  https://claude.ai/code/artifact/e54a2317-7406-49ab-bc77-7607b9920860): `SplitDiaryEntryDialog`
  data-loss-on-failure, `KickoffDialog._showing` never wired, one date parse missing
  `InvariantCulture`. **Round 5 introduced the two mechanisms every later round keys off**:
  `Database.RunInTransaction`/`CreateCommand` (shared multi-write transaction wrapper, also used
  by `ScoreService`) and `DateExtensions.ToIsoTimestamp()`. Also: `ReviewDialog.ShowCore` split
  into 4 named steps, `Views/EmptyPlansState.cs` shared panel, `Log.cs` capped at 5MB,
  `TickTickAuth` logs only `error`/`error_description` never the raw body, test project pinned
  `System.Net.Http`/`System.Text.RegularExpressions` alongside SQLite. All fixable findings
  applied same-session each round; verified each time with full Debug+Release build + all tests +
  `dotnet list package --vulnerable` + live UIA pass.
- **2026-07-14 (round 6)**: 0 Critical, 4H/10M/11L/8I
  (https://claude.ai/code/artifact/4575736f-87f3-4926-b233-6135aaac0530). All rounds 1-5 fixes
  held. **Remediated 2026-07-15** (all 4H/10M + 8 of 11L — see that entry below for what was
  fixed and what's deferred).
- **2026-07-15**: Three live bug reports from the user on the running app (not from an audit pass) —
  investigated and two fixed, one partially diagnosed:
  1. **Fixed** — the evening review never auto-fired the night before (07-14) and gave no
     notification either; the user had to open and close it manually. Root cause:
     `ReviewDialog._offeredOn` (meant to stop the automatic EOD watcher from re-nagging once
     already offered that day) was being set at the end of `ShowCore`, which is shared by BOTH
     the automatic watcher AND `TodayPage`'s manual "Evening review" button
     (`Review_Click` → `ShowAsync` directly, bypassing `Trigger`/`ShouldOffer`). One daytime
     glance at that button to check progress silently burned the day's one automatic offer —
     no error, no toast, no dialog, for the rest of the day, with nothing in the log because
     nothing failed. Moved the `_offeredOn` assignment into `Trigger()` so only a real automatic
     offer sets it; manual previews no longer touch it. `Dialogs/ReviewDialog.cs`. Checked
     `KickoffDialog` for the same shape (it mirrors this class) — clean, its only two call sites
     both go through `Trigger`/`ShouldShow`.
  2. **Fixed** (feature gap, not a regression) — the diary section only ever rendered on
     navigation/search/edit, never live. Added a 30s repeating timer, active only while showing
     today with no search active, stopped via a new `ReportsPage.OnNavigatedFrom` when the page
     isn't visible. `Pages/ReportsPage.Diary.cs`, `Pages/ReportsPage.xaml.cs`.
  3. **Not conclusively root-caused** — no "where were you" prompt for the 06:00–10:04 gap this
     morning; tracking just started silently at 10:04 (confirmed via `time_diary`: no row, no
     dialog, no toast for that stretch). Traced `ActivityTracker.HandleSleepGap`/`HandleIdleReturn`
     exhaustively — by static reading it should have fired `OnIdleReturn` on the 10:04 resume.
     Two hardening changes went in regardless: `Log.Info` diagnostics at the exact decision points
     (`idleStart`/`idleEnd`/`accountedUntil`/whether the handler actually fired) so the next
     occurrence has hard evidence instead of re-guessing; and `MainWindow.Tracker.cs`'s
     `OnIdleReturn` subscriber was a discarded `_ = IdleReturnDialog.Trigger(...)` fire-and-forget
     — any fault inside became an unobserved task exception invisible until GC, the exact failure
     shape already once logged for this same call (2026-07-09). Now wrapped in try/catch so a
     fault is actually logged. **Open TODO — watch the log next time this happens.**
     `Services/ActivityTracker.cs`, `MainWindow.Tracker.cs`.

  Verified: full Debug+Release build, all 10 tests pass, live Release instance stopped (10:49,
  safely before the 20:00 EOD time)/rebuilt/relaunched clean with no startup errors, UIA-confirmed
  window rendered.
- **2026-07-15, continued**: Same-day follow-up turn — "update the skills, update CONTEXT.md,
  create a skill that decides when to do either + when to compact a session, fix all the round-6
  findings, build and commit." In order:
  - Reconciled the canonical skills repo (`Desktop/CLAUDE/skills`, its own git repo) against
    `~/.claude/skills` — several past sessions had edited the deployed copy directly instead of
    the canonical source per its own README, so canonical was stale. Pulled the deployed copies
    forward into canonical first (commit), *then* added this session's new lessons on top, then
    redeployed — otherwise `install-skills.sh --global` would have silently clobbered the drifted
    fixes. Folded into `windows-app-auditor`: the discarded-Task fire-and-forget exception pattern,
    and the manual-trigger-burns-automatic-offer pattern (both from the earlier 07-15 entry above).
  - **New skill: `knowledge-upkeep`** (`Desktop/CLAUDE/skills/knowledge-upkeep/SKILL.md`, deployed
    globally) — a housekeeping skill that decides when a project's context doc needs a new entry
    vs. needs compacting, when a session's lesson generalizes into a global skill vs. stays
    project-local, and when to mention `/compact` for the conversation itself. Added to
    `install-skills.sh`'s loop and the repo's README.
  - **Round-6 remediation**: all 4 Highs and all 10 Mediums fixed, plus 8 of 11 Lows (deferred:
    the shift-task-day dedup — sensitive scheduling code with a real regression history, extract
    later with more care; keyboard shortcuts — a feature addition, not a fix, out of scope for this
    pass; the `BuildDiarySection` 206-line split — a larger refactor than the remaining time
    budget warranted, still open). Highlights: `PlanStore.IsValidPlanId` is now the one definition
    both import and everyday use share; `TaskNoteView.Build`'s `onSave` now returns success and
    only commits the on-screen text once the write actually lands; `ReviewDialog.PersistReview`
    and `ReportsPage.Diary.MarkSelected` now go through `RunInTransaction`;
    `ActivityTracker.LogIdleAnswers` (new) batches a split-mode idle answer into one transaction
    instead of one connection per segment; `RescheduleTaskDialog`/`AddTaskDialog`/
    `ReplanOverdueDialog`/`EditDiaryEntryDialog` now return `bool?` (null=cancelled,
    false=save failed) so callers can finally tell a real failure apart from the user clicking
    Cancel, and surface it via each page's `SaveErrorBar` (new on `ReportsPage`/`PlansPage`);
    Archive/Restore/Add-plan/Add-task all refresh the sidebar and wrap their file operations in
    try/catch now; new `DateExtensions.ToIsoTimeOfDay()`/`ToDisplayDate()` and new
    `Services/JsonFileIO.cs` (`Indented` options + `WriteAllTextAtomic`, temp-file-then-`File.Move`)
    close out the remaining format/write duplication; `MainWindow.NativeInterop.cs`'s session-change
    handler now treats Fast User Switching and an RDP disconnect the same as a lock; Reports'
    Day/Week/Month/Year selector is now a `RadioButtons` group (matches Settings' theme picker),
    not four independent `ToggleButton`s; `KickoffDialog` gained a "Later" close button, the one
    recurring dialog that had none. Full detail/citations in the round-6 report link above.
  - **New bug found live, not from the audit** — while regression-testing the round-6 fixes,
    spotted (via the user flagging a screenshot of overlapping diary rows: an on-plan VS Code entry
    11:41→11:51 sitting inside an idle "Break" entry 11:40→11:55) that
    `ActivityTracker.HandleActiveSession`'s idle-detected branch closed the outgoing on/off-plan
    session through `now` (the poll that first notices the idle threshold crossed, itself up to
    `idle_threshold_minutes` — 10 min default — *after* the user actually stopped) instead of
    back-computing to the real idle-start instant the way `HandleSleepGap` already correctly does.
    The subsequent idle segment then starts at that same real idle-start instant, so the two
    entries overlap by up to the full idle threshold on *every* idle transition — on-plan/off-plan
    minutes (and therefore score) were being double-counted against idle time by up to 10 minutes
    each time, for as long as idle detection has existed. Fixed by computing the idle-start point
    once and using it for both the session's end and `_idleSince`. **Open TODO**: the two rows in
    the screenshot (and any other historical instances) are still sitting in `data/progress.db`
    as-is — correcting them needs the user's explicit go-ahead naming the specific rows/table per
    this file's Direct-database-access rule; not touched.
  - Verified: full Debug+Release build, all 10 tests, `dotnet list package --vulnerable` clean on
    both projects, live Release relaunch clean (no startup errors), UIA-confirmed the new
    `RadioButtons` period selector renders and switches correctly, git history re-swept for `.db`
    files (clean — closes the round-6 privacy pass's one incomplete check).
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
  Also (2026-07-15, found live not via audit): a once-per-day "don't re-offer" flag must only be
  set by the *automatic* trigger path — if a manual preview/glance button shares the same
  show-and-persist function as the automatic watcher (as `ReviewDialog.ShowCore` did), the manual
  path silently burns the automatic offer for the rest of the day, with no error to catch it on.
  When a dialog has both an automatic timer trigger and a manual button that calls the same
  underlying `ShowAsync`, ask whether state written at the end of that shared method makes sense
  from *either* caller, or only the automatic one.
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

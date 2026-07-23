# Planillium — Project Context

**Display name is "Planillium"** (renamed 2026-07-08; the app was originally internally
called Mentor-Overseer). The repo folder, GitHub repo, and C# namespace were all still
`MentorOverseer` for a while after the display-name rename — kept that way deliberately at
first (internal, invisible to users) — but the user confirmed wanting a full internal
rename too now that the app is public, done 2026-07-23: project folders/csproj files are
`winui/Planillium.App`/`winui/Planillium.App.Tests`, the C# namespace is `Planillium.App`
throughout. Three deliberate exceptions, all legacy-compatibility values that must keep
referencing *old* name(s) for existing-install migration/cleanup: `AppInfo.LegacyStartupRegistryValue` +
`CredentialStore`'s `LegacyService` (so an existing TickTick token isn't orphaned),
`StartupService.LegacyNames` (registry Run-key sweep — also lists `"Mentor-Overseer"`/`"NetherlandsMentor"`
from even earlier names), and `release/installer/app.iss`'s `DelRunKeyLegacy1`/`Legacy2`/`Legacy3`
entries (mirrors the same list). Compiled exe: `Planillium.App.exe`.

## What this app is
A desktop personal mentor and accountability companion that tracks the user's progress
across up to 2 active life/career plans simultaneously. It monitors his activity,
keeps him on-plan, logs his full day (06:00–20:00), and generates weekly reports.

## The user
- Location: Milan, Italy (moving to Utrecht/Eindhoven, NL)
- Goal: Land a Dutch Digital Transformation Manager role → HSM visa → EU citizenship
- Key tools: Power BI, Power Platform, SharePoint, MBA from Bologna
- Active plans: Netherlands Relocation, The Complete 10-Level Claude Code Mastery Guide
  (both under `plans/active/`)

---

## App architecture

**The WinUI app (`winui/Planillium.App`) is THE app.** It started as a Python/
Tkinter app (main.py), was fully rebuilt in WinUI 3/.NET 8 over 2026-07-06/07, and the
Python source was removed from the repo on 2026-07-08 once the WinUI app had shipped
everything it did (still recoverable from git history/tags if ever needed).

```
Planillium/
├── CONTEXT.md              ← you are here. Read this first every session.
├── CLAUDE.md               ← operating instructions (how to work here — build/verify/
│                              publish workflow, safety rules). CONTEXT.md is facts,
│                              CLAUDE.md is process; read both, keep them separate.
├── config.json             ← shared user settings, idle threshold, scoring rules (no
│                              secrets — TickTick client_secret/access_token live in
│                              Windows Credential Manager via CredentialStore, not on disk)
├── plans/
│   ├── active/             ← up to 2 active plan JSONs (e.g. netherlands.json)
│   ├── queued/             ← plan ideas saved for later (2026-07-22) — inert until
│   │                          activated (PlanStore.ActivateQueuedPlan resets start_date)
│   └── archive/            ← completed plans moved here; frees a slot for a new plan
├── data/
│   ├── progress.db         ← SQLite — see Database schema below
│   ├── winui_state.json    ← window size/theme/kickoff-review state
│   └── mentor-winui.log    ← app log
├── release/                ← Inno Setup installer pipeline (release.ps1, app.iss) — see
│                              the windows-app-releaser skill for how to cut a build
└── winui/Planillium.App/  ← WinUI 3 / .NET 8 source — see Tech stack below
```

---

## Tech stack
- **Framework:** WinUI 3 / .NET 8 (`net8.0-windows10.0.19041.0`), Windows App SDK 1.8.260529003
- **Build:** `dotnet build -p:Platform=x64 -c Release` from `winui/Planillium.App/` —
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
7. **The user's steady-state rule: one task per day.** A day holding two tasks is only ever
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
   active plans). Archiving now also offers to immediately start a queued idea if one
   exists (2026-07-22) — see business rule 11.
9. Score floor: daily score floors at −10; a `weekly_comeback_bonus` (20 pts) rewards
   a full week back on track after a bad stretch; a `multi_task_bonus_per_extra_task`
   (3 pts, added 2026-07-09) rewards each task completed beyond the first one on the
   same day, on top of the flat per-task rate.
10. **Day-off scoring (added 2026-07-17)**: when EVERY active plan considers a day off
    (recurring exclusion or manual day-off — `ScoreService.AllPlansScoringExempt`; one
    plan off while another still has real work due does NOT trigger this), that day's
    on/off-plan minutes, missed-task penalty, and streak bonus are all suppressed —
    "no points calculation" is the default for a day off. The one exception: a task
    actually brought in and completed that day (e.g. via Move-to-today onto an
    off day) still earns its own task-completion + multi-task-bonus credit — being off
    doesn't forfeit credit for real work done anyway. The same day-off dates are also
    excluded from Reports' aggregate totals (weekly/monthly/yearly summary, Time-by-App,
    Top Distractions) — tracked and still visible in the raw Diary list, just not
    counted toward any total. Editing a diary entry's category/time (or bulk-marking
    several) now recalculates that date's `daily_score` via `RecalculateDayScore`
    (unlike `CreditDayScoreIfMissing`, this overwrites an already-credited day — it has
    to, since the whole point is a stale figure needs updating). The off-plan nag alert
    also doesn't fire on a manually-off day (`ActivityTracker.IsFullyOffToday`) — but
    unlike a recurring rest day, tracking itself still runs normally on a manual day off
    (diary rows keep getting written); only scoring and the alert are suppressed.
11. **Queued plan ideas (2026-07-22)**: hitting the 2-active-plan limit in "Add Plan" no
    longer just blocks — it offers to save the new plan to `plans/queued/` instead. A
    queued plan is completely inert (not loaded into scoring/Today/Schedule, no
    `start_date` set at creation) until `PlanStore.ActivateQueuedPlan` moves it to
    `plans/active/` and resets `start_date` to the activation date. Two entry points to
    activate one: the Plans page's own "Queued ideas" section ("Start now," enabled only
    when a slot is free), or a suggestion dialog offered automatically right after
    archiving a plan frees one up.
12. The sidebar/Plans-page "Xd late from plan" (`Plan.DriftDays`) measures reschedule/overdue
    slip *beyond* the plan's own excluded-weekday pattern (current last-task date vs. the
    originally-due date, both already mapped through the exclusion pattern) — this is now the
    only "days late" figure in the app. Reports used to also show a second, differently-computed
    figure (the "Exclusion Impact" panel); removed 2026-07-23 for showing a different-looking
    number for the same plan that read as a bug (it wasn't — see the 2026-07-23 session note)
    and for not earning its keep otherwise.

---

## config.json key fields
Working hours, reminder/idle timing, `ticktick.client_id` (secret/tokens are in
Credential Manager, never here), `activity_rules` (on_plan/off_plan/neutral keyword
lists), `scoring` (task_completed/task_overdue_penalty/on_plan_hour/off_plan_hour/
streak_bonus_per_day/weekly_comeback_bonus), `score` (points_per_minute/
points_per_currency_unit/currency_symbol — the "buy entertainment time" economy),
`idle_activity_rules` (idle-dialog answer → category reclassification, the user
populates via Settings), `start_with_windows`, `appearance.theme_mode`,
`late_day_task_reminder_hours` (added 2026-07-20, default 2.0 — how long before
`end_of_day_summary_time` the once-a-day "tasks still open" toast fires). Edited via
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
on 2026-07-17 after the round-7 audit; ~680→~110 lines on 2026-07-22; ~630→~300 lines on
2026-07-23 morning; ~610→~180 lines later the same day; ~494→~130 lines that same evening
after re-audit iteration 2 landed)._

- **2026-07-23, full day**: three morning items shipped — (1) confirmed the sidebar's "days
  late from plan" and Reports' old "Exclusion Impact" panel measured genuinely different things
  (not a shared-state bug), user called the second panel "useless" so it was removed outright,
  `Plan.DriftDays` is now the app's only "days late" readout (business rule 12); (2) Diary's
  "App - Page" column split into independently filterable App/Page columns + an "All time"
  checkbox + live subtotal (live-verification caught the checkbox only refreshing the list, not
  the header/date-nav — fixed to call the full `Render()`); (3) full internal rename
  `MentorOverseer`→`Planillium` across all `.cs`/XAML/project files, three legacy-compat values
  deliberately left untouched (`AppInfo.LegacyStartupRegistryValue`, `CredentialStore.LegacyService`,
  `StartupService.LegacyNames`). All three verified clean build + 86/86 tests.
  Then a full 5-category audit (24 findings: 0 Critical/High, 9 Medium, 10 Low, 5 Info) →
  "fix all": 18 straightforward AUTO-FIX items applied directly — unguarded
  `TickTickSection.LoadAsync` try/catch; old-name strings → `AppInfo.DisplayName`; a shared
  `AppInfo.MaxActivePlans` constant (replacing 5 hand-typed `2`s); `ConfigService` gained
  `WorkStartTime/WorkEndTime/ReminderGraceMinutes/ReminderIntervalMinutes/IdleThresholdMinutes`
  (fixed a duplicated-default drift between `ActivityTracker` and `SettingsPage`); `ReportsPage.
  Diary`'s bulk mark-as-category action + two lazy-init timers extracted into named methods; a
  per-connection read-timeout fix in `TickTickAuth`'s loopback listener (local-DoS); a file-size
  cap on plan import; `Uri.EscapeDataString` on TickTick IDs; a `DangerButtonStyle` resource on
  genuinely destructive buttons only; two `DateExtensions` date-format helpers; a daily
  diary-retention-prune watcher (previously only ran once at launch); `THIRD-PARTY-NOTICES.md`
  regenerated; `dotnet format` + using-order fixes. Four items needed the user's own call (asked
  once, batched): **#9** two abandoned local git branches still held the user's real name/an old
  plan file — user chose delete, both `git branch -D`'d (later `git gc --prune=now`'d clean by
  the privacy re-audit). **#17** added a "Pause tracking" tray menu item, session-only. **#6**
  all 26 `ContentDialog` construction sites across 19 files migrated onto one shared factory,
  `Dialogs/DialogControls.Build(...)`, each site's exact original title/buttons/`DefaultButton`
  preserved. **#8** (splitting `ActivityTracker`'s Win32-interop code out of its 819-line
  God-Object shape) — user chose to **defer** to its own focused session; still open, see below.
  Two regression catches during remediation, both verified against real usage *before* applying
  the audit's own suggested fix rather than after: the contrast-color finding's suggested delete
  of `App.xaml`'s override would have broken real `{ThemeResource}` XAML bindings — kept both
  copies, added cross-reference comments instead; the fixed-pixel-width finding's suggested
  `MinWidth` switch was applied to `SettingsPage.xaml` but would have undone a deliberate
  fixed-width layout decision in `ReportsPage.Diary.cs` — left that one alone. A live incident,
  likely self-inflicted: the live Release exe was stopped twice (file-lock) to verify the fixes,
  and the second stop appears (by log timeline) to have landed on top of an open, unanswered
  idle-return "welcome back" dialog and killed it mid-interaction — not a code defect, no data
  lost (evening review's gap-sweep re-catches it), folded into the global `windows-app-auditor`
  skill's runtime-safety rules.
  **Re-audit iteration 2** (same 5 parallel passes against the fixed code): Security came back
  clean. Architecture found one real regression — the new "Pause tracking" feature was silently
  undone by `RestartTracker()` (called on every Settings autosave/diary re-categorization), with
  the tray label left lying about actual state — fixed with a `_trackingPaused` field checked in
  `RestartTracker()` (`MainWindow.Tray.cs`/`MainWindow.Tracker.cs`). Code-quality found one Low —
  `SettingsPage.xaml.cs`'s `RetentionDays.Value` still read a local closure instead of the
  already-added `ConfigService.DiaryRetentionDays()`, the exact "fixed one sibling, missed the
  other" pattern this round was meant to eliminate — fixed (one line). Privacy found two Lows —
  the deleted git branches' commits were still dangling/unreachable (fixed via `git reflog expire
  --expire=now --all && git gc --prune=now`, verified via `git fsck --unreachable`), and
  MANUAL.md/CHANGELOG.md hadn't been updated for this round's user-visible changes (fixed —
  MANUAL.md's "Activity tracking" bullet now mentions Pause tracking; CHANGELOG's Unreleased
  section gained entries for Pause tracking and the daily prune-watcher fix). The UX pass was
  re-run after being stopped once earlier — 26-site dialog-factory migration came back clean
  (every button/`DefaultButton` diffed against its pre-migration original), but found 3 new
  issues in this round's own new surface, all fixed: pausing tracking only updated the sidebar's
  text label, leaving the colored dot and "current window" line stuck on stale state
  (`MainWindow.Tray.cs`'s `TogglePauseTracking` now also resets both); Settings' tracker-status
  paragraph was computed once at page-load only, so toggling Pause from the tray while Settings
  was already open left it lying (added a `MainWindow.TrackingStateChanged` event, mirroring the
  existing `NotificationCenter.UnreadChanged` pattern, that `SettingsPage` subscribes to);
  Diary's "Clear filters" button didn't clear the adjacent search box, and — a second gap found
  by inspection while fixing that, not by the audit — it also called the partial
  `RenderDiaryResults()` instead of `Render()`, so clearing "All time" left the header
  caption/date-nav stale (same bug class as the morning's "All time" checkbox fix, on the one
  sibling handler that still hadn't been updated to match). Verified: clean Debug build (0
  warnings) + 86/86 tests; Release build's copy step hit the live instance's file lock as
  expected — left it running rather than kill it a third time same day.

**Pre-2026-07-18 arc, condensed** (full detail in git log / the linked artifacts): WinUI 3
rebuild landed 07-07 as v1.0.0 (18 findings fixed at ship time, TickTick secret purged from
git history and rotated). Audit rounds 1-6 (07-09 to 07-15) introduced the mechanisms every
later round built on — `Database.RunInTransaction`, `DateExtensions.ToIsoTimestamp()`,
`JsonFileIO` atomic writes, `PlanStore.IsValidPlanId`, transactional dialogs with a `SaveErrorBar`
— plus fixed diary column width, window-clamp-to-monitor, the completed-task-shift data-loss
bug (business rule 7), move-to-today backward compaction, `ReviewDialog` reentrancy, three
Add-Plan wizard templates keying phases wrong, and an idle-detection double-counting bug (open
question at the time: some already-written overlapping `time_diary` rows from before the fix —
resolved below, 07-18 full scan). 07-16 fixed day-off/reschedule task-shifting to skip over
already-taken days (`NextWorkingDay`/`PrevWorkingDay`). 07-17's full 5-category audit (1
High/9 Medium/~15 Low-Info) added `TreatWarningsAsErrors`, a shared `CategoryStyle.cs` color
table, a "Clear all my data" Settings action, and Settings autosave; the same day added the
day-off scoring feature (business rule 10: `AllPlansScoringExempt`, `RecalculateDayScore`,
`IsFullyOffToday`).

- **2026-07-18, rounds 8–11 audits + remediation** (round 8 artifact:
  https://claude.ai/code/artifact/016b54c5-9852-4e12-9092-c5fdb799b4e9): 4 rounds, ~60 findings
  (0 Critical, 1 High ×2, rest Medium/Low/Info), fixed same-day each round, verified via clean
  builds + growing tests (19→83) + `dotnet list package --vulnerable`. Headline fixes: R8-01
  `ScoreService.CurrentStreak` took an optional `asOf` instead of always anchoring
  `DateTime.Today` (silent streak-bonus-zeroing bug editing past diary entries, same class fixed
  in `ReportData.WeekStats`); R8-05 unconditional export-file deletion on "Clear all my data,"
  whose own re-audit caught 2 new silent-failure issues in that same fix (see Standing lessons);
  round 10 added `CredentialStore.Delete`/"Disconnect TickTick" + a "Your name" Settings field;
  closed-form `PlanDayForDate`/`DateForPlanDay` fix (see Standing lessons); a full-history scan
  found 42 overlapping `time_diary` row-pairs (06-29–07-16), only 2 matching the known bug
  signature — **user's call: leave the data untouched**; personal-data git-history purge (real
  name → "the user" across 134 commits + stripped config/plans, `git filter-repo` scratch-clone).
  Same week: `posting-plan`/`project-media` global skills bootstrapped.
- **2026-07-20/21, tracking fixes + first public release**: late-day task reminder shipped;
  `AppNames.Sub()` gained a "File Explorer" case (was silently dropping the folder/tab name);
  unread-notification tray dot added. **Diary-tracking-gap bug RESOLVED** (reported 3× since
  07-15): `PollOnce`'s `HandleSessionLock`/`HandleSleepGap` call order let a lock notification
  stamp a resuming gap's start as "now," collapsing it to zero — fixed by swapping the order.
  Reports page slow-load fixed (eager row construction → "build first N, Show More" pattern).
  GitHub repo renamed `mentor-overseer`→`planillium` (after deleting a stray duplicate squatting
  the name since 07-08). **First real release (v1.1.0)**: `release/release.ps1` run clean for
  the first time, MIT `LICENSE` added, repo flipped Public, `v1.1.0` tagged + GitHub Release
  created. Unsigned (no Authenticode cert, SmartScreen wall documented not fixed).
- **2026-07-22, six shipped items**: desktop shortcut icon fixed (pointed at the exe itself
  instead of a path that went stale after the folder rename). **`DispatcherQueueTimer.Tick`
  silently never firing — root-caused**: a `Log.Info` probe never fired across 60+ intervals
  despite `IsRunning=True`; all three watchers (EOD/kickoff/late-day-reminder) switched to
  `System.Threading.Timer` + `_dq.TryEnqueue`, each rooted in a field. Queued plan ideas
  (v1.2.0) shipped — `plans/queued/`, `PlanStore.LoadQueuedPlans/ActivateQueuedPlan/
  DeleteQueuedPlan`, `AddPlanDialog`'s at-limit choice, `PlansPage`'s "Queued ideas" section.
  **Tray icon stuck-badge bug root-caused**: `TaskbarIcon` disposes whatever `Icon` it replaces,
  but the old code cached and reused the same two `Icon` objects — second reuse threw
  `ObjectDisposedException` and left the tray stuck (also the likely explanation for an earlier
  "tray icon vanished" report); fixed by never reusing an `Icon` instance across assignments.
  Diary category/app filtering added to Reports. Tray unread-dot recap added
  (`PendingNotificationsDialog`, "While you were away"), then a same-evening XamlRoot-not-ready
  startup race in it fixed the same way `InitTrackingAsync` was (guarded, dual-triggered check).
  Settings layout overflow fixed (a row split in two); sidebar/Reports score-label confusion
  fixed (a "BALANCE" caption distinguishing running-balance from live-formula-preview). Reddit
  launch post turned out to be held by r/ClaudeAI's karma gate, not removed — reformatted for
  the Megathread instead (lesson: "removed by Reddit's filters" while still visible to the OP
  with engagement is an automatic hold, not a rejection — check the subreddit's posted rules).
- *(Still open from 2026-07-17: "keyboard/dark-mode/timing items needing a live human check,"
  never confirmed done — worth a quiet check next time Settings/theming is touched.)* Also from
  07-20: `ActivityTracker.ActiveWindowTitle` falls back to the process name when both the raw
  title and `ExeAppNames` lookup are empty (was showing ~118 bare "-" diary rows/day).
- **Standing lessons** (apply every session, not just the one that taught them):
  - **Simplicity is king: prefer the algebraic/closed-form fix over a caching layer when the
    thing being repeated has structure to exploit.** The `PlanDayForDate`/`DateForPlanDay`
    O(days-elapsed) walk (round-5 finding #28) could have been "fixed" by memoizing results per
    plan — but that adds an invalidation surface (must be cleared whenever `ExcludedWeekdays`
    changes) for a problem the math itself dissolves: the exclusion pattern is weekly-periodic,
    so skipping full weeks in closed form turns O(days-elapsed) into O(1) with zero state to
    keep in sync, ever. When a repeated calculation has periodic/structural regularity, look for
    the closed form before reaching for a cache — a cache is the right tool when the underlying
    work is genuinely irreducible (e.g. `ScoreService.DaysOff`'s DB-backed per-instance cache),
    not when it's just an unexploited pattern in the math.
  - **A remediation's own re-audit must re-check the fix's *own* new code, not just confirm the
    original findings are gone.** 2026-07-18 round-8: the R8-05 fix (delete export files on
    "Clear all my data") wrapped each file-delete in its own try/catch that logged-and-swallowed
    failures — the exact "silent failure, nothing shown to the user" shape that made R8-05 a
    finding in the first place, now reproduced inside its own fix, plus in the untouched sibling
    `ClearHistory_Click` that had carried the same latent bug the whole time. The full 5-pass
    re-audit caught it because it re-ran the whole privacy checklist against the new code instead
    of only checking "is R8-05 gone" — confirms remediation-loop.md's "re-run the FULL audit, not
    just the touched files" instruction is protecting against a real, not hypothetical, failure
    mode: a fix silently reintroducing (or revealing) the same bug class it was meant to close.
  - **"Fix one sibling, miss the other" — general pattern + full history now lives in the global
    `windows-app-auditor` skill (4 rounds, 4 shapes, most recently 07-17's color-table +
    messenger-list duplicates); this project's specific unfixed-until-caught instance: round-5's
    shared color table (`ReportsPage.Styling.CategoryBrushKey`) never got the tray pill
    (`MainWindow.Tracker.UpdatePill`) migrated onto it, and a doc comment wrongly claiming "both
    now read from this one table" went uncorrected for two rounds.
  - Any "show a prompt on a timer" trigger here needs BOTH the `IsOnScreen()`-check-then-toast-
    fallback pattern AND an "already showing, don't reopen" guard — confirmed missing on
    `StartEodWatcher` (round 2), `ReviewDialog` (round 3), `KickoffDialog` (round 5). A once-per-day
    "don't re-offer" flag must only be set by the *automatic* trigger path — if a manual preview
    button shares the same show-and-persist function as the automatic watcher, the manual path
    silently burns the automatic offer with no error to catch it (confirmed: `ReviewDialog`
    07-15).
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
    auto-mode classifier enforces this and will block an unnamed attempt. Read-only queries
    against it (cross-validating a UI figure against ground truth) are always fine.
  - `CopyFromScreen`/GDI `BitBlt` doesn't capture WinUI3 Mica/DirectComposition content —
    use `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`). For exact layout comparisons,
    UI Automation `BoundingRectangle` beats pixel-diffing screenshots.
  - When a background poll first *notices* a state change (idle crossing a threshold, a timer
    tick), the poll's own timestamp is not the same as when that state change actually happened —
    it can lag by up to the full poll/threshold interval. Log/close out events at the
    back-computed real moment, not at "now," or two records meant to be back-to-back end up
    overlapping instead (see the 2026-07-15 idle-transition overlap bug below).
  - **`git filter-repo` must never run in-place in a repo that has other live `git worktree`
    checkouts attached** (this repo has 3) — it refuses to run at all unless the repo looks
    like a fresh clone (or `--force`), and forcing it in-place risks corrupting the other
    worktrees since they share the same object store. Do the rewrite in an isolated scratch
    clone (`git init` + `git fetch <local-repo-path> branch:branch` for just the branches in
    scope — this also cleanly excludes any other local-only branches from the rewrite), verify
    with a git blob-level diff (`git diff <old-sha> <new-sha> --stat`, not a raw filesystem
    diff — checkout line-ending differences between two separate clones make raw `diff -rq`
    falsely report nearly every file as changed), then push from there and reset the real
    working copy afterward. Also: `--replace-text` only rewrites file blob content, not commit
    messages — a separate `--replace-message` pass is needed to actually remove a string from
    "history" in the sense a user means it (2026-07-18).
  - **When a code comment already anticipates a source of confusion (e.g. "these two numbers
    can legitimately differ"), the comment alone doesn't prevent a real user from hitting that
    exact confusion again** — the 2026-07-23 DriftDays/shiftDays report is the round-4 finding's
    predicted confusion happening for real, a year of code-comments later. If a fix for this
    class of thing is ever revisited, put the clarification where the user actually looks (the
    UI itself), not only in a doc comment only a future session will read.
- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **`ActivityTracker`'s Win32-interop code (819 lines) still needs splitting out of its
    God-Object shape** — flagged by the 2026-07-23 audit (finding #8), user deliberately
    deferred it to its own focused session rather than bundle it with 22 other fixes, since
    this file is behind most of the app's real historical bugs and deserves care, not a rushed
    batch change.
  - **Tray icon reportedly vanished entirely after being clicked (2026-07-22), not yet
    root-caused independently** — though the 2026-07-22 stuck-badge investigation (see above)
    found a very likely same-root-cause explanation (`ObjectDisposedException` in the old tray
    icon code) and fixed it; watch the log if this specific symptom (app appearing to fully
    close, not just fail to reopen) recurs post-fix.
  - **A full-history scan (2026-07-17/18) found 42 overlapping diary-row pairs from 06-29 through
    07-16, not just the one 07-15 instance previously flagged, plus 2 rows with end_time before
    start_time.** Only 2 of the 42 cleanly match the documented `HandleActiveSession` bug
    signature; the other 40 are mostly 1-2 minute boundary artifacts with no single confirmed
    cause. **Settled, not an action item**: the user's call was to leave the data untouched
    rather than guess-correct it (2026-07-18); revisit only if a clear mechanism for the other 40
    turns up on its own.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field specifically (not
    "App Service URL").
  - One remaining follow-up from the 2026-07-09 TickTick client-secret rotation: the app's
    Windows Credential Manager entry still holds the *old* secret until the user reconnects
    TickTick from Settings (disconnect → "Connect TickTick" → re-auth writes the new value) —
    until then, TickTick sync will fail with an auth error using the now-invalid old secret.
  - **Resolved-and-closed, kept as one-line pointers for date reference** (full detail was here
    before the 2026-07-23 compaction — see git log for that prose if ever needed): full internal
    `MentorOverseer`→`Planillium` rename, done 2026-07-23 (see session note above); diary-
    tracking-gap bug, resolved 2026-07-21 (`PollOnce` call-order fix); LinkedIn/Reddit
    autonomous-publish for `posting-plan`, dropped 2026-07-22 (both platforms' APIs turned out
    gated/unsuitable — a dormant Reddit OAuth2 tool was built and kept at
    `~/Desktop/CLAUDE/skills/posting-plan/tools/reddit-publish/` in case policy changes);
    `PlanDayForDate`/`DateForPlanDay` O(days-elapsed) walk, fixed 2026-07-18 (closed-form
    replacement, see Standing lessons); TickTick OAuth client secret, rotated 2026-07-09 (user
    confirmed at developer.ticktick.com); personal-data git-history scrub before going public,
    done 2026-07-18 (`git filter-repo` scratch-clone, 134 commits, verified via blob diff);
    v1.1.0 push + GitHub Release + repo flipped Public, done 2026-07-21; stray duplicate
    `devbaghda/planillium` repo (a stale 07-08 snapshot squatting the name), deleted 2026-07-21
    after the user granted the needed `delete_repo` OAuth scope and Recycle-Bin permissions.

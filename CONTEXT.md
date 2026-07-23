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
2026-07-23, folding the 07-18 through 07-22 entries down to their essential facts)._

- **2026-07-23, "days late" mismatch investigated — not a bug, confirmed by hand-computing
  both figures against real data**: user flagged the sidebar showing "13d late from plan" for
  *both* active plans while Reports' Exclusion Impact panel showed "4 day(s) later"/"6 day(s)
  later" for them respectively, plus "1 task(s) currently overdue." Read `Plan.DriftDays`
  (`Models/PlanModels.cs`) and the Exclusion Impact panel's `shiftDays` (`ReportsPage.xaml.cs`)
  side by side — a doc comment already flagged (round-4 audit) that these are deliberately
  different measurements sharing no name, but the user hit the same *visual* confusion again
  since the on-screen text alone doesn't say so. Independently hand-recomputed both from the
  real plan JSONs (`start_date`, `total_days`, `excluded_weekdays` — both plans exclude
  Sat/Sun) and the real `task_overrides` table (read-only query) for each plan's actual
  rescheduled task days: got **DriftDays = 13 for both plans** (coincidence — each computed
  independently from its own start date/overrides, verified to genuinely both land on 13) and
  **shiftDays = 4 / 6** respectively — an exact match to what the user saw, confirming the
  numbers are correct, not a shared-state bug. Root of the confusion: DriftDays measures how
  much a plan's *finish* has slipped from reschedules/overdue tasks on top of its normal
  weekend-exclusion pattern; shiftDays measures something unrelated — how many calendar days
  the weekend-exclusion pattern *alone* has cost reaching *today's* plan-day, ignoring any
  reschedule. First fix attempt: added a one-line clarifying note under the Exclusion Impact
  panel's header explaining the distinction. **Superseded same session**: the user decided the
  panel wasn't worth keeping at all ("I think it is useless") — removed `ExclusionImpactCard`
  and its call site entirely rather than keep clarifying it. The sidebar's "late from plan"
  figure (`Plan.DriftDays`) is now the only "days late" readout in the app; business rule 12
  (above) reflects this. Verified: clean Debug build, 0 warnings, after both the add and the
  removal.
- **2026-07-23, Diary app/page column split + all-time filter + subtotal**: user request — split
  the diary's combined "App - Page" display into two independently filterable columns (e.g.
  App="Chrome"/Page="GitHub", or App="Telegram"/Page="Liza Ponomarenko"), let the category/app/page
  filters search the whole retention window instead of just the day on screen, and show a running
  subtotal (entry count + total time) for whatever's currently filtered. `ReportsPage.Diary.cs`:
  `DiaryList`'s row `Grid` now has separate App/Page/Details columns (was one combined "what"
  column) built from `AppNames.Group`/`AppNames.Sub` directly rather than the combined `Label`;
  `_diaryAppFilter` now matches `Group` only (was `Label`) and a new `_diaryPageFilter` matches
  `Sub` — both persisted static fields, survive navigation like the existing search/date state.
  New `_diaryAllTime` bool (a "All time (not just this day)" checkbox) widens the query range to
  the same window free-text search already used (`today−retention` to `today`) without requiring
  search text — factored as `wideRange = searching || _diaryAllTime` everywhere the old `searching`
  check alone used to gate date-nav enablement, the header's caption, and whether a day-only or
  wide-range query runs. A new subtotal `TextBlock` above the results sums `filteredList`'s
  `Dur` and formats via a new `FormatDuration` helper. **Bug caught and fixed during live
  verification**: the "All time" checkbox's handler initially only called `RenderDiaryResults()`
  (like the other filter combos), which updates the list but not `DiaryHeader()` (built once per
  full `Render()`) — so the "TIME DIARY · ALL TIME" caption and the date-nav arrows' disabled
  state lagged a step behind the checkbox until something else triggered a full page render.
  Fixed by having the checkbox's handler call `Render()` instead, matching what the date-nav
  buttons already do for the same reason. Verified live end-to-end via read-only UI Automation
  against the real running instance (no data mutation): all three filter combos and the "All time"
  checkbox render with correct option lists (app options are bare group names like "Chrome",
  not the old combined label); selecting "Chrome" in the app filter narrowed the subtotal to "9
  entries · 11m total"; toggling "All time" widened it to "3341 entries · 222h 11m total" across
  the full 90-day retention window with the caption/date-nav updating immediately after the fix;
  "Clear filters" correctly reset everything back to the default single-day view. Clean
  Debug+Release build (0 warnings) + 86/86 tests both before and after the live-caught fix.
- **2026-07-23, full internal rename away from `MentorOverseer`**: user confirmed wanting this
  2026-07-21 ("Yes, rename it everywhere, including internally (bigger job)"), queued until now.
  Scope: C# namespace `MentorOverseer.App` → `Planillium.App` across all 76 `.cs` files (`namespace`/
  `using` lines only — a targeted replace, not a blind string swap, since a blind one would have
  broken three intentional legacy-compatibility values, below); `x:Class` in all 7 XAML files updated
  to match (missed by the initial namespace-only pass, caught before it could cause a build
  failure); project folders `winui/MentorOverseer.App`→`winui/Planillium.App` and
  `...App.Tests`→`...App.Tests` renamed, along with both `.csproj` files and the Tests project's
  `RootNamespace`/`Compile Include` paths; `Planillium.App.csproj`'s own `RootNamespace` updated to
  match its already-correct `AssemblyName`; path references updated in `README.md`,
  `THIRD-PARTY-NOTICES.md`, `CHANGELOG.md`, `CLAUDE.md`, `release/release.ps1`,
  `release/README.md`, and `release/installer/app.iss`'s icon path. **Deliberately left untouched**
  (confirmed by reading each site's actual logic, not just grepping): `AppInfo.LegacyStartupRegistryValue`
  (`"MentorOverseer"`) and `CredentialStore`'s `LegacyService` that reads it; `StartupService.LegacyNames`
  (also lists even-older `"Mentor-Overseer"`/`"NetherlandsMentor"`); and `app.iss`'s
  `DelRunKeyLegacy1`/`Legacy2`/`Legacy3` uninstall steps, which mirror that same list. All three are
  real, intentional backward-compatibility code, not stale references.
  **A folder-rename permission wall**: `git mv`/`Directory.Move` on both project folders initially
  failed with plain "Access denied" despite no individual file being exclusively locked (verified by
  attempting to open every file for exclusive read/write first) — traced to two background
  processes holding directory-level handles: the C# Dev Kit's MSBuild `BuildHost` and a lingering
  `vstest.console` host from an earlier `dotnet test` run, both killed safely (routine IDE/tooling
  processes that restart on their own). Even after that, the top-level directory rename still failed
  — worked around by creating the new folder fresh and moving every item into it individually
  (confirmed zero per-file failures) rather than renaming the directory entry itself, then deleting
  the now-empty original. Verified end-to-end: clean Debug+Release build (0 warnings) + 86/86 tests
  from the new paths; live instance rebuilt and relaunched from `winui/Planillium.App/bin/...`
  (confirmed via `Get-Process ... | Select Path`), clean startup log with no errors, and read-only UI
  Automation confirmed every nav page (Today/Schedule/Reports/Plans/Settings) still renders
  correctly post-rename.

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
  https://claude.ai/code/artifact/016b54c5-9852-4e12-9092-c5fdb799b4e9 — full findings/fixes in
  git log): 4 rounds, ~60 findings total (0 Critical, 1 High × 2 rounds, rest Medium/Low/Info),
  all fixed same-day each round, each verified via clean Debug+Release build + growing test
  suite (19→83 tests) + `dotnet list package --vulnerable`. Headline fixes: **R8-01** (High)
  `ScoreService.CurrentStreak` took an optional `asOf` instead of always anchoring
  `DateTime.Today`, fixing a silent streak-bonus-zeroing bug when editing past diary entries
  (same bug also found/fixed in `ReportData.WeekStats`); **R8-05** unconditional export-file
  deletion on "Clear all my data" (user's explicit choice over an opt-in checkbox), whose own
  re-audit caught 2 new silent-failure issues in that same fix, closed same day (see Standing
  lessons). Round 9's sub-agents failed outright on a usage-limit error, ran as one direct pass
  instead (found `ExportFileNames` duplicate-literal + `Log.Friendly` wrapping for ~10
  raw-exception-message sites). Round 10 cross-confirmed sibling gaps rounds 8/9 had missed in
  untouched files, added `CredentialStore.Delete`/"Disconnect TickTick",
  `ScoreService.Overrides()` memoization, a "Your name" Settings field. Round 11's **R11-01**
  (missing try/catch on round 10's own new Disconnect button) was independently caught by two
  different audit passes landing on the same finding. Also: closed-form
  `PlanDayForDate`/`DateForPlanDay` performance fix (see Standing lessons); a full-history scan
  found 42 overlapping `time_diary` row-pairs (06-29 through 07-16), only 2 matching the known
  bug signature — **user's call: leave the data untouched**; and the personal-data git-history
  purge (real name → "the user" across 134 commits + stripped `config.json`/
  `plans/active/*.json`/a stray `obj/` tree, via `git filter-repo` in an isolated scratch clone
  — see Standing lessons for why never in-place). Same week: `posting-plan`/`project-media`
  global skills built and bootstrapped for this project with a first drafted post.
- **2026-07-20, three investigations**: (1) late-day task reminder feature shipped
  (`late_day_task_reminder_hours` Settings field, reuses `TodayPage`'s own "today's open tasks"
  definition). (2) `AppNames.Sub()` never had a case for "File Explorer," silently dropping the
  folder/tab name from every such diary row — fixed. (3) Unread-notification tray dot
  (`NotificationCenter.cs`, persisted counter cleared on window `Activated`, GDI+-composited
  red-dot badge icon) plus diagnostic logging for a then-unexplained "no EOD popup appeared"
  report — the diagnostics added here caught the real diary-tracking-gap bug the next day.
- **2026-07-21, diary-tracking-gap RESOLVED** (closed a bug reported three times since 07-15):
  root cause was `PollOnce`'s call order — `HandleSessionLock` ran *before* `HandleSleepGap`, so
  on a poll resuming after a long real gap (e.g. Windows throttling the app's timer overnight
  while backgrounded) with a pending lock notification, the lock handler stamped the gap's start
  as "now" and `HandleSleepGap`'s own `_idleNotified` guard then silently skipped the check that
  would have anchored it correctly — the whole gap collapsed to zero with no trace. Fixed by
  swapping the call order. Live in Release, confirmed via the existing diagnostic log lines.
- **2026-07-21, three more shipped items**: Reports page slow-load fix (root cause was eager
  WinUI element construction for every diary row, even hidden ones; fixed with a "build first N,
  defer the rest behind Show More" pattern). Posting-plan content (`posts/`, `POSTING_PLAN.md`,
  `SOCIAL_POSTS.md`) removed from the public repo, both current tree and full git history (same
  scratch-clone `git filter-repo` approach) since it isn't part of the app. GitHub repo renamed
  `devbaghda/mentor-overseer` → `devbaghda/planillium` to match the app's display name, after
  deleting a stray duplicate repo that had been squatting the `planillium` name since 07-08.
- **2026-07-21, first real release build (v1.1.0) + public-readiness pass**: `release/release.ps1`
  (Inno Setup pipeline) existed but had never actually been run — ran it clean for the first
  time: version bumped 1.0.0→1.1.0, an MIT `LICENSE` added, full verify checklist run (fresh
  install to a disposable directory, smoke test, uninstall-preserves-data check). Repo flipped
  Private→Public, `v1.1.0` tag pushed, GitHub Release created with the installer + checksum
  attached. Unsigned (no Authenticode cert) — first run shows SmartScreen's "unrecognized app"
  wall, documented rather than fixed (would need a paid cert).
- **2026-07-22, desktop shortcut icon fixed**: `IconLocation` still pointed at the pre-rename
  local folder path (before `CLAUDE\mentor-overseer` → `CLAUDE\Planillium` on disk), so Windows
  silently fell back to a generic icon even though the launch target itself was correct. Fixed
  by pointing `IconLocation` at the exe itself (`Planillium.App.exe,0`, which already embeds the
  icon via the csproj) instead of a separate loose path that can go stale on a future move.
- **2026-07-22, `DispatcherQueueTimer.Tick` silently never firing — root cause found and fixed**:
  investigating "no late-day-task-reminder popped up despite genuinely open tasks" (plus "no
  tray unread dot," same underlying symptom), a `Log.Info` probe as the literal first line of
  the `Tick` handler never once fired across 60+ expected intervals over an hour, despite
  `timer.IsRunning` reading `True` immediately after `Start()`. Conclusively a `DispatcherQueueTimer`
  whose `Tick` silently never fires despite reporting itself as running — likely also the real
  explanation for a 07-20 "EOD review never appeared" report that used the same timer pattern
  and was never conclusively diagnosed at the time. **Fix**: all three watchers (EOD, kickoff,
  late-day-reminder) now use `System.Threading.Timer` instead, callback marshaled onto the UI
  thread via `_dq.TryEnqueue(...)`, each rooted in a field (unlike `DispatcherQueueTimer`, an
  unreferenced `System.Threading.Timer` is GC-eligible). Verified live — the same probe fired
  within one interval of the first restart under the new mechanism. Standing lesson folded into
  the global `windows-app-auditor` skill: `IsRunning=True` is not proof a `DispatcherQueueTimer`
  will ever tick — instrument the handler's first line and watch several intervals before
  trusting it.
- **2026-07-22, queued plan ideas (v1.2.0)**: user asked for a way to not lose a 3rd plan idea
  while capped at 2 active plans — capture it as a "to-do," then get offered it once a slot
  frees up. New `plans/queued/` dir, `PlanStore` gained `LoadQueuedPlans`/`ActivateQueuedPlan`/
  `DeleteQueuedPlan`. `AddPlanDialog`'s at-limit block became a choice ("Archive a completed plan
  first" vs. "Save idea for later") — the latter runs the same wizard/import flow targeting
  `plans/queued/`, skipping `start_date` (set later at activation via the same surgical-JsonNode-
  patch pattern `SetExcludedWeekdays` uses). `PlansPage` gained a "Queued ideas" section (Start
  now/Delete); `ArchiveAsync` now offers a `StartQueuedPlanDialog` picker right after a
  successful archive if any ideas are queued. Verified end-to-end in a disposable `MENTOR_ROOT`
  sandbox (synthetic data, not the user's real plans): at-limit choice → queue → correct file
  written → Queued ideas UI renders correctly → archive → post-archive suggestion appears
  (correctly serialized via `DialogGate`) → activation moves the file to `plans/active/` with
  `start_date` reset. Clean Debug build (0 warnings) + 83/83 tests (no new automated tests —
  file-I/O-driven UI flow with no existing test-fixture pattern to extend, consistent with
  Archive/Restore). Shipped as v1.2.0.
- **2026-07-22, tray icon stuck-badge bug root-caused and fixed**: user reported the red dot
  stayed on even after clicking through the recap dialog (below). The live log's stack trace
  pinned it exactly: `System.ObjectDisposedException: Object name: 'Icon'` at `Icon.get_Handle()`,
  called from `H.NotifyIcon.TaskbarIcon.UpdateIcon`. Root cause: the old code cached exactly two
  long-lived `Icon` objects (plain/badged) and handed the same object to `TaskbarIcon.Icon`
  repeatedly — but `TaskbarIcon` disposes whatever icon it's replacing once a new one is
  assigned, so the *second* time either cached object came back around it had already been
  disposed out from under it, throwing and leaving the tray permanently stuck on whatever badge
  state it was in. Almost certainly also the real explanation for an earlier, never-conclusively-
  diagnosed "tray icon vanished / app appeared to close after clicking" report — same code path,
  same failure mode. Confirmed the app itself never actually closed during any of this (a
  read-only `time_diary` query around both exception timestamps showed continuous, ungapped
  tracking rows straight through them). **Fix**: `MainWindow.Tray.cs` rewritten to never reuse an
  `Icon` object across more than one assignment — the badged variant is pre-rendered to real
  `.ico` bytes once at startup (via `Icon.Save`, not the old `Icon.FromHandle` wrapper), and a
  brand-new `Icon` is constructed from either those bytes or the plain icon file on every swap,
  left undisposed by our code (consistent with `TaskbarIcon` taking ownership). This also let the
  old shutdown-time `DestroyIcon`/handle-tracking cleanup be deleted entirely. Verified: clean
  Debug+Release build (0 warnings) + 86/86 tests; live click-through wasn't feasible this session
  (tray UI can't be reliably driven by UI Automation, and seeding a repro needs either a genuine
  toast or hand-editing live `winui_state.json`, which needs the user's confirmation per
  CLAUDE.md's data-write rule) — stopped/rebuilt/relaunched the live instance regardless,
  confirmed clean startup; **actual click-through was later confirmed live the same evening**,
  see the recap-dialog entry below.
- **2026-07-22, diary category/app filtering added**: user request — filter the Reports page's
  Diary list by column, confirmed to mean category (on_plan/off_plan/paid/neutral/idle) and app,
  combinable. `ReportsPage.Diary.cs` gained two persisted (survives-navigation) static fields
  `_diaryCategoryFilter`/`_diaryAppFilter`, two `ComboBox`es in `BuildDiarySection`, and a "Clear
  filters" button. Filters apply independently of the free-text search box and, unlike it, work
  in single-day view too. Verified live via read-only UI Automation against the real running
  instance (no data mutation): selecting "Off-plan" showed exactly 22 rows, cross-checked
  against a direct read-only DB query (exact match); adding an app filter on top narrowed to 13,
  all matching; "Clear filters" correctly reset both.
- **2026-07-22, tray unread-dot recap added, then a startup race in it fixed same evening**:
  user reported the red dot appeared correctly (07-20's `NotificationCenter` fix) but clicking
  the tray icon opened the app with nowhere to see what the notification had been about.
  `StateService.AppState` gained a `PendingNotifications` list (capped at 10, oldest dropped
  first); `NotificationCenter.Record` (renamed from `MarkUnread`) now stores title/message/
  timestamp alongside incrementing the unread count, and `NotificationCenter.TakePending`
  (replaces `MarkAllRead`) returns and clears both in one step. New
  `Dialogs/PendingNotificationsDialog.cs` shows a "While you were away" recap (oldest first).
  New `NotificationCenterTests.cs` (3 tests, 86 total). **Follow-up bug caught live the same
  evening**: a leftover unread notification from before a relaunch triggered the recap dialog on
  the very first `Activated` event during startup — before `Content.XamlRoot` was ready —
  throwing `ArgumentException` ("This element does not have a XamlRoot") into an unobserved
  fire-and-forget task, the same race `InitTrackingAsync` had already been fixed for once
  before. Fixed the same way: the recap check is now a guarded `CheckPendingNotifications()`
  method, wired to both `Activated` and `root.Loaded` — whichever fires with a valid `XamlRoot`
  first drains the pending state and shows the dialog. Verified live end-to-end: a real "Day's
  winding down" toast fired, a fresh relaunch picked up its persisted unread state on startup
  with no exception, and read-only UI Automation confirmed the "While you were away" dialog
  rendered with the exact title/message before being closed — this also stands as the delayed
  live click-through confirmation for the stuck-badge fix above.
- **2026-07-22, Settings layout overflow + sidebar/Reports score-label confusion fixed**:
  `SettingsPage.xaml`'s hours-and-reminders row was overflowing the 720px content column by
  ~200px, silently clipping `LateDayReminderHours` off-screen — split into two stacked rows.
  Separately, the sidebar's point total and Reports' "Today's Score" looked like the same number
  but aren't (sidebar = running balance; Reports = a live formula preview) — added a small
  "BALANCE" caption above the sidebar figure to distinguish them.
- **2026-07-22, Reddit launch post filtered by r/ClaudeAI's karma gate**: the published Reddit
  post showed "removed by Reddit's filters" but remained visible to the OP with a real
  upvote/comment — the tell for an automatic hold, not a mod removal. Turned out to be
  r/ClaudeAI's own posted rule: 50+ karma required for the main feed, otherwise redirected to a
  Project Showcase Megathread. Reformatted the post into a shorter Megathread-comment version
  (`posts/2026-07-20-launch-announcement-reddit.md`) for the user to paste there instead.
  **Lesson for future `posting-plan` use**: a Reddit post showing "removed by Reddit's filters"
  to the OP while still visible with engagement is an automatic hold, not a rejection — check
  the subreddit's own posted rules/sidebar for a karma gate or megathread-redirect rule before
  assuming a mod-approval request is the right next step.
- *(Also not yet resolved from 2026-07-17's audit: "keyboard/dark-mode/timing items that need a
  live human check" were deferred at the time as needing hands-on verification — never confirmed
  done in any later session; worth a quiet check next time Settings/theming is touched.)* Also
  from 2026-07-20's window-title investigation: `ActivityTracker.ActiveWindowTitle` now falls
  back to the process name when both the raw title and the `ExeAppNames` lookup are empty
  (previously ~118 diary rows/day showed a bare "-", most commonly the desktop briefly focused
  mid-app-switch).
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

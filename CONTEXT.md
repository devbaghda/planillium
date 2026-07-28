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
populates via Settings), `appearance.theme_mode`,
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
2026-07-23 morning; ~610→~180 lines later the same day; ~494→~130 lines that same evening after
re-audit iteration 2 landed; ~486→~90 lines on 2026-07-24 after that day's own audit round;
~509→~65 lines later the same day after a second 07-24 audit round + fix; ~256→~220 lines that
same evening after a third 07-24 audit round + fix; ~570→~488 lines total on 2026-07-28 after
two same-day fixes pushed the file back past the ~300-line threshold)._

- **2026-07-23, full day**: full internal rename `MentorOverseer`→`Planillium` (3 legacy-compat
  values deliberately untouched); Diary App/Page filter split; removed Reports' redundant
  "Exclusion Impact" panel (business rule 12). Full 5-category audit, 24 findings, fixed same day
  (18 auto; 4 by user call: 2 stale branches with personal data deleted, tray "Pause tracking"
  toggle, all 26 `ContentDialog` sites onto one `DialogControls.Build` factory, `ActivityTracker`'s
  819-line God-Object split **deferred** — see Open TODOs). Same-day 5-pass re-audit closed 4
  regressions + 3 UX gaps. Verified: clean build + 86/86 tests.
- **2026-07-24, three same-day audit rounds** (22/22/12 findings, 0 Critical): round 1 re-verified
  07-23 held, fixed a fresh Diary filter-row overflow plus ~7 smaller findings, and separately
  root-caused/fixed "have to switch pages to see the new day" (`TodayPage`/`SchedulePage`'s
  `NavigationCacheMode="Enabled"` never recomputed "today" overnight) via `StartDayChangeWatcher`
  (`33b86ec`). Round 2 (3 independent passes) caught the watcher fix only covered Today/Schedule,
  not Plans/Reports; also fixed a note-wipe risk (`TaskNoteView.AnyEditInProgress`) and Schedule
  re-snapping on refresh, plus ~10 smaller items (watcher-timer boilerplate, a11y names, VACUUM
  on diary-prune, docx zip-bomb size check) (`c37a97a`). Round 3 fixed its own round-2 regressions
  (VACUUM moved back off the UI thread; zip-bomb check switched from trusting the header size to
  counting real decompressed bytes) plus gave notes a stronger fix (`TaskNoteView` now persists
  open drafts across any rebuild, not just the day-change watcher's — let the watcher's note guard
  be removed entirely), `VacuumAndCheckpoint()` now truncates the WAL too, and a few smaller items.
  All 3 rounds verified: clean build + 86/86 tests.

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

- **2026-07-18 through 07-22, condensed** (full detail in git log; round-8 artifact:
  https://claude.ai/code/artifact/016b54c5-9852-4e12-9092-c5fdb799b4e9): four audit rounds
  (~60 findings, 0 Critical) fixed same-day each, verified via clean builds + growing tests
  (19→83) — `ScoreService.CurrentStreak`/`ReportData.WeekStats` took an optional `asOf` instead
  of always anchoring `DateTime.Today` (silent streak-bonus bug editing past entries); closed-form
  `PlanDayForDate`/`DateForPlanDay` (see Standing lessons); `CredentialStore.Delete`/"Disconnect
  TickTick"; a full-history scan found 42 overlapping `time_diary` row-pairs, only 2 matching the
  known bug signature — **user's call: leave the data untouched**; personal-data git-history purge
  (134 commits, `git filter-repo`). Then: late-day task reminder shipped; `AppNames.Sub()` gained
  a "File Explorer" case; the diary-tracking-gap bug (reported 3×) resolved by fixing
  `PollOnce`'s `HandleSessionLock`/`HandleSleepGap` call order; Reports slow-load fixed
  (build-first-N pattern); GitHub repo renamed to `planillium`; **first public release v1.1.0**
  (unsigned, SmartScreen wall accepted not fixed). Then: desktop shortcut fixed; `DispatcherQueueTimer.Tick`
  root-caused as silently never firing (confirmed via a `Log.Info` probe across 60+ expected
  intervals) — all watchers switched to `System.Threading.Timer` + `_dq.TryEnqueue`, field-rooted;
  queued plan ideas (v1.2.0) shipped; tray icon stuck-badge bug root-caused (`TaskbarIcon` disposes
  a reused `Icon`, causing `ObjectDisposedException`) and fixed; Diary category/app filtering
  added; tray unread-dot recap added; Settings layout overflow and sidebar/Reports score-label
  confusion both fixed. *(Still open from 2026-07-17: "keyboard/dark-mode/timing items needing a
  live human check," never confirmed done.)* `ActivityTracker.ActiveWindowTitle` falls back to
  the process name when both the raw title and `ExeAppNames` lookup are empty (07-20, was
  showing ~118 bare "-" diary rows/day). Same week: `posting-plan`/`project-media` global skills
  bootstrapped; a Reddit launch post held by r/ClaudeAI's karma gate (not removed) was reformatted
  for the Megathread instead.
- **2026-07-27**: App wouldn't start at all (instant `XamlParseException` on every launch,
  before `OnLaunched`'s first log line). Git-bisected to `ece15f5`'s `SetDefaultDllDirectories`
  call (07-24 finding #10, DLL-hijack hardening) breaking WinRT's native activation of the
  bundled WinUI3 DLLs on this machine; the app hadn't actually been relaunched since 07-24, so
  it sat broken-but-untested for 3 days (see Standing lessons). The finding's own text already
  showed the app's real `DllImport`s are protected KnownDLLs regardless of search order, so
  removing the call outright is a clean revert, not a tradeoff. Fixed in `App.xaml.cs`, committed
  `4d0f161`, pushed.
  Separately same day: Diary appeared to start whenever the PC was first touched that morning
  instead of the configured 06:00, because the wake-from-sleep "welcome back" toast
  (`IdleReturnDialog`/`ActivityTracker.HandleIdleReturn`) only ever logged the gap if a UI handler
  was *not* wired (never true in the running app) — a missed/unclicked toast meant that stretch
  never entered the diary. First fix attempt (an evening-review gap sweep) was **rejected by the
  user** — they wanted it logged immediately, matching the old Python modal dialog's guarantee
  (always logged something on close, real answer or `"dismissed"`/`"unaccounted time"`). Correct
  fix: `HandleIdleReturn` now always logs `"unaccounted time"` the instant a gap is detected,
  and `LogIdleAnswer`/`LogIdleAnswers` gained `ClearIdlePlaceholder` to swap that placeholder for
  a real answer without creating a duplicate/overlapping row. The specific already-missed stretch
  that day wasn't backfilled (would need a direct `progress.db` write, not authorized). Verified:
  clean build + 86/86 tests + relaunch, committed `d3373a5`, pushed.
- **2026-07-28**: Diary's per-entry "Edit"/"Split" buttons were unreachable. Root cause (found
  after 4 dead-end attempts — narrow-window, scrollbar visibility, alignment all had zero effect):
  `ReportsPage.Styling.cs`'s shared `Card()` helper's non-zero `CornerRadius` corner-clips content
  to the Border's own *arranged* bounds regardless of child MinWidth/alignment (see Standing
  lessons) — the Card wrapping the diary list was arranged at ~880px while rows had grown to
  ~1000px+ since the 07-23 App/Page column split, silently clipping away Edit/Split for 5 days
  with no scrollbar, overhang, or automation trace (`BoundingRectangle: Empty`). Fixed by giving
  that `Card()` instance an explicit `MinWidth` (950 + its own padding). Verified two ways:
  `ScrollItemPattern.ScrollIntoView()` now returns a real on-screen rect for the actual button,
  and a post-scroll screenshot shows every row's Edit/Split rendering normally. Clean build +
  86/86 tests + relaunch.
  Separately same day: a missed idle-return toast read via the "While you were away" recap dialog
  (app reopened some other way, not by clicking the toast) showed the toast's own "click to log
  where you were" text with nothing behind it but "Close" — the general "recap replays a prompt's
  copy without its action" bug class (applies to all 3 timed prompts, not just idle-return; see
  Standing lessons). Root cause: `PendingNotification` only ever stored `Title`/`Message`/`AtIso`,
  never the toast's `action`/`mins`/`start` args `OnNotificationInvoked` uses for a direct click.
  Fixed by round-tripping those args end to end (`ToastNotifier.Show` → `NotificationCenter.Record`
  → new `PendingNotification.Args` dict, JSON-persisted) and giving `PendingNotificationsDialog` a
  real per-item action button (label keyed off the action) that calls
  `MainWindow.HandleNotificationActivation` (made `internal`) after closing the recap — the same
  dispatch a direct toast click already used. Items with no/unrecognized action still render as
  plain text only. Verified: clean build + 86/86 tests + relaunch; the new button's actual
  click-through wasn't live-tested (would need completing a real idle-answer or hand-editing live
  `winui_state.json`, neither done without the user's go-ahead) — rests on matching
  `OnNotificationInvoked`'s already-proven dispatch path exactly.
- **Standing lessons** (apply every session, not just the one that taught them):
  - **A dialog/UI surface that repeats another prompt's copy ("click to X") must also carry that
    prompt's action, not just its text** — text and action can silently drift apart the moment a
    prompt is ever shown through a second surface (a recap, a log, a history view) that wasn't
    part of its original click path. When adding a second place that displays a prompt's message,
    check whether the *action* needs to travel with it too, not just the words.
  - **A `Border` with a non-zero `CornerRadius` corner-clips its content to its own *arranged*
    bounds, independent of the child's MinWidth/DesiredSize/HorizontalAlignment** — none of the
    normal layout-sizing levers (MinWidth on a descendant, explicit alignment) can compensate
    for a too-narrow ancestor Border once it clips; the Border itself needs to be sized wide
    enough. A clipped-away element's UI Automation `BoundingRectangle` reads as `Empty` (not a
    valid off-screen rect), which looks identical to "this element was never given layout space
    at all" — don't let that automation signature rule out clipping as the cause. If a
    dynamically-built row/card that use to fit suddenly doesn't (after some sibling column grew
    wider), check every rounded-corner `Border` between the overflowing content and its
    ScrollViewer, not just the ScrollViewer's own settings or the content's own MinWidth.
  - **A hardening fix that touches process-wide native init (DLL search order, security
    mitigations, anything set once at startup) isn't actually verified until the app has been
    launched fresh after it, not just built clean + unit-tested.** The 07-27 `SetDefaultDll
    Directories` regression (see session note above) passed a clean build and the full test
    suite on 07-24 and sat in `App.xaml.cs` for 3 days before anyone actually relaunched the
    live GUI app — the same "trust that it looks right instead of re-running the exact check"
    failure already called out below for compiler warnings, just for a runtime effect a
    compiler/test suite can't see at all. After any change to process bootstrap/native interop,
    actually relaunch the app once before calling the finding closed — don't let "builds clean"
    stand in for it.
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

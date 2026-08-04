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
          "category": "profile", "duration_min": 60, "tools": ["VS Code", "Chrome - Coursera"] }
      ]
    }
  ]
}
```
Required fields: `id`, `name`, `phases`. `mentor_note` (per task) and `briefing`
(plan-level) are what drives the app's "mentor" UI (💡 note under a task, MENTOR'S
NOTE in Details, 📋 Briefing button) — a plan imported without them just won't show
mentor commentary; that's a content gap in the source JSON, not a bug. `tools` (per
task, added 2026-07-28) is the list of specific apps/tools/websites that task needs —
`AddPlanDialog` teaches every distinct one to `config.json`'s `activity_rules.on_plan`
list right after a real (non-queued) import (`ConfigService.LearnActivityRule`, same
mechanism the Diary's manual "mark on-plan" bulk action already used), with a short
confirmation dialog naming what was learned. The "Add Plan" wizard's 3 prompt templates
(`Services/PlanTemplates.cs`) ask Claude to include `mentor_note`, `briefing`, and
`tools` (the Reformat template only fills `tools` where the user's own source text
actually implies a specific tool, matching its existing "don't invent content" rule).

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
1. **Working hours (`working_hours.start`/`.end`, config.json + Settings; default 08:00–20:00)
   are the diary tracking window too** — one pair of hours governing both, since 2026-08-04.
   They decide when the off-plan nag may fire *and* when activity is recorded at all
   (`ActivityTracker.InWorkingHours`, the single predicate both use). Outside them nothing is
   tracked and no gap is counted against the user.
2. The diary window used to be a pair of hardcoded 06:00/20:00 statics in `ActivityTracker`,
   unrelated to working hours and unreachable from the UI — so moving the working day to 08:00
   left 06:00–08:00 still tracked and back-filled as "unaccounted time" every morning (the
   2026-08-04 report). It spent a few hours as its own `diary_hours` config block with its own
   Settings pair before the user's call to merge the two: one pair of hours is the whole idea,
   a second pair is just another thing to keep in sync. **A `diary_hours` block in an existing
   config.json is inert** — not read at all, and stripped on the next Settings save.
   `SettingsPage.SaveRules` rejects work start ≥ end, which only became worth enforcing once
   these hours gated tracking: inverted, that isn't a short day, it's no tracking at all.
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
13. **"Day X of Y" is a progress counter, not a calendar counter** (`Plan.ProgressDay`, added
    2026-08-04) — **display only**, on Today/Plans/Schedule. Every scheduling, overdue and
    scoring decision still uses `Plan.PlanDay` (the pure calendar count); the two are
    deliberately different numbers for different questions. The rule: stop at the earliest plan
    day still holding an incomplete task, never run ahead of the calendar day, never exceed the
    plan length shown after the "of". So missing day 10's task keeps the header on day 10 the
    next morning, and working ahead doesn't release it — only closing the day behind it does.
    That can't strand the counter, because **the user's stated way to skip a day's task is to
    reschedule it** (Reschedule / "Replan all overdue"), which moves its `AssignedDay` forward
    and releases the counter by itself; a task nobody reschedules and nobody finishes is exactly
    the case that should hold it. The plan-length clamp is what stops a 28-day plan reading
    "Day 30 of 28" once it overruns — lateness is reported by the overdue list and `DriftDays`
    (rule 12), not by inflating this number. All three screens read the same value on purpose
    (see the 2026-07-23 DriftDays lesson on two screens showing different-looking numbers for
    the same thing).

---

## config.json key fields
Working hours (which are also the diary window — business rule 1),
reminder/idle timing, `ticktick.client_id` (secret/tokens are in
Credential Manager, never here), `activity_rules` (on_plan/off_plan/neutral keyword
lists), `scoring` — all 12 keys now listed once in `Services/ScoringRules.cs`, which is the
single source of their defaults, ranges and Settings labels; the formula, the config lookup
(`ConfigService.ScoringRate(key)`) and the Settings SCORING section all read from that table,
so an absent key behaves exactly as the old hardcoded default did —, `score` (points_per_minute/
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
_An index, not an archive — blow-by-blow detail for any entry lives in git log. Compress
aggressively rather than letting this grow: it has been compacted ~15 times since 2026-07-06
(852→224 was the first; the latest, 2026-08-04, took this section from ~503 to ~210 lines and
promoted the Standing lessons into their own section — they had been accumulating as sub-bullets
under one arbitrary 07-29 entry, which is a good way to lose them in the next pass)._

### Standing lessons
_The durable ones. Every entry here cost a real bug to learn; none may be dropped in a
compaction. General versions of several now also live in the global `windows-app-auditor` /
`windows-app-tester` skills._

**Verification discipline**
- **Re-run the exact check that flagged something, after fixing it** — don't trust that it looks
  right. A build regression (`ReportsPage.xaml.cs:369`) survived three sessions as an assumed
  "pre-existing harmless warning" because nobody re-ran a clean `/warnaserror` build.
- **A change to process bootstrap or native interop isn't verified until the app has actually
  been relaunched** — not just built clean and unit-tested. The 07-24 `SetDefaultDllDirectories`
  hardening passed both and then sat in `App.xaml.cs` for 3 days, having broken WinUI's native
  activation so the app wouldn't start at all (found 07-27, reverted).
- **A remediation's re-audit must re-check the fix's own new code, not just confirm the original
  finding is gone.** Round-8's R8-05 fix reproduced the very "silent failure" shape it was
  written to close, inside itself — caught only because the full checklist was re-run rather
  than "is R8-05 gone".
- **Check every sibling** before calling a fix complete. Grep for the *shape*, not the reported
  call site. This project's uncaught instance: round-5's shared colour table
  (`ReportsPage.Styling.CategoryBrushKey`) never got the tray pill (`MainWindow.Tracker
  .UpdatePill`) migrated onto it, and a doc comment falsely claiming "both now read from this one
  table" stood for two rounds.
- **Don't infer a business rule** from one comment or one screenshot. If a fix depends on a rule
  that isn't written down, ask.

**Safety around real data**
- Never simulate input (clicks/keystrokes) that would mutate real plan/score data — verify
  data-mutating logic by code inspection plus a clean build. Any direct write to
  `data/progress.db` outside the app's own code needs explicit confirmation naming the table and
  change (the harness's auto-mode classifier enforces this and rejects a vague "yes"). Read-only
  queries against it are always fine.
- Check `end_of_day_summary_time` before killing a live instance near EOD — killing it early can
  skip that day's evening-review popup entirely.
- The live instance runs from `bin\x64\Release\...`, not Debug — confirm before trusting a Debug
  rebuild as verification.
- **To exercise the UI without touching real data, run a second Debug instance against a scratch
  `MENTOR_ROOT`** (copy config.json + plan files, empty DB) with `MENTOR_INSTANCE_SUFFIX=verify`
  — the DEBUG-only mutex suffix exists for exactly this. Back-dating the scratch plans' start
  date stages date-dependent behaviour that is otherwise very hard to reach (2026-08-04).
- **`git filter-repo` must never run in-place in a repo with other live worktrees attached** —
  it refuses unless the repo looks freshly cloned, and forcing it risks corrupting them via the
  shared object store. Rewrite in an isolated scratch clone (`git init` + `git fetch <path>
  branch:branch`, which also excludes local-only branches), verify with a git *blob* diff
  (`git diff <old> <new> --stat` — a raw `diff -rq` falsely flags nearly every file, because two
  clones differ in line endings), push from there, reset the real working copy after. Also:
  `--replace-text` only rewrites blob content — a separate `--replace-message` pass is needed to
  get a string out of "history" in the sense a user means it.

**WinUI / layout**
- **A `Border` with a non-zero `CornerRadius` corner-clips its content to its own *arranged*
  bounds**, regardless of the child's MinWidth, DesiredSize or alignment — no layout lever on a
  descendant can compensate; the Border itself must be wide enough. A clipped element's UI
  Automation `BoundingRectangle` reads `Empty`, identical to "never given layout space", so that
  signature must not rule clipping out. When a dynamically-built row that used to fit stops
  fitting after a sibling column grew, check every rounded-corner Border between the content and
  its ScrollViewer.
- **`Expander` needs `HorizontalContentAlignment="Stretch"` or its content is arranged at its
  own minimum width**, no matter how wide the Expander itself is. A three-column grid inside one
  collapses to the width of its text instead of filling the row — the same silent under-sizing
  class as the rounded-`Border` clip above, with the same absence of any error. Set it on every
  Expander, and verify with UIA rects rather than by eye (2026-08-04).
- `CopyFromScreen`/GDI `BitBlt` doesn't capture WinUI3 Mica/DirectComposition content — use
  `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`). For exact layout comparisons, UIA
  `BoundingRectangle` beats pixel-diffing.
- Cached `AutomationElement` references go stale across any `Render()` that rebuilds the visual
  tree — re-query fresh inside loops.
- `DispatcherQueueTimer.Tick` was confirmed to silently never fire while `IsRunning` read true
  (60+ missed intervals, diagnostics in place). All watchers use `System.Threading.Timer` +
  `_dq.TryEnqueue`, each **stored in a field** — an unrooted one is GC-eligible and stops firing.
- Subscribe to `AppNotificationManager` events *before* `.Register()`; the reverse order throws
  at the WinRT layer.

**Prompts, timers and state**
- Any "show a prompt on a timer" trigger needs BOTH the `IsOnScreen()`-check-then-toast-fallback
  pattern AND an "already showing, don't reopen" guard — confirmed missing on `StartEodWatcher`,
  `ReviewDialog`, `KickoffDialog`. A once-per-day "don't re-offer" flag must be set only by the
  *automatic* path: a manual preview button sharing the same show-and-persist function silently
  burns the automatic offer, with no error to catch it.
- **A surface that repeats another prompt's copy ("click to X") must carry that prompt's action
  too, not just its text.** They drift apart the moment a prompt is shown through a second
  surface (a recap, a log, a history view) that wasn't part of its original click path.
- **When a background poll first *notices* a state change, the poll's timestamp is not when the
  change happened** — it lags by up to the full poll/threshold interval. Close events out at the
  back-computed real moment, not "now", or two records meant to be back-to-back overlap instead.
- **State that must survive a rebuild has to live outside the thing being rebuilt.** A `static`
  field seeded once at class load is not the same as state that tracks "today" — Reports' diary
  date was static and so stayed pinned to yesterday overnight even though the day-change watcher
  faithfully re-rendered the page (2026-08-04).

**Design / simplicity**
- **Prefer the closed form over a cache when the repetition has structure.** `PlanDayForDate`'s
  O(days-elapsed) walk could have been memoized — but that adds an invalidation surface for a
  problem the maths dissolves: the exclusion pattern is weekly-periodic, so skipping whole weeks
  in closed form is O(1) with no state to keep in sync. A cache is right when the work is
  genuinely irreducible (e.g. `ScoreService.DaysOff`), not when it's unexploited structure.
- **A defaults table beats a default retyped at each call site.** `("task_overdue_penalty", -5)`
  appeared three times and five more rules were consts with no config presence at all; adding a
  Settings UI would have made a sixth copy. One `ScoringRules` table now feeds the formula, the
  config lookup and the Settings section (2026-08-04).
- **A code comment anticipating a confusion doesn't prevent the confusion.** The 2026-07-23
  DriftDays/shiftDays report is a round-4 finding's *predicted* confusion happening for real, a
  year of comments later. Put the clarification where the user looks — the UI — not only in a
  doc comment a future session reads.
- A dedup helper (e.g. `ToIsoDate()`) needs a second pass to check *adjacent* formats (e.g.
  timestamps) weren't left uncovered by the same helper.

### Session log

**Pre-2026-07-18 arc** (detail in git log): WinUI 3 rebuild landed 07-07 as v1.0.0 (18 findings
fixed at ship time; TickTick secret purged from git history and rotated). Audit rounds 1-6
(07-09→07-15) introduced the mechanisms every later round built on — `Database.RunInTransaction`,
`DateExtensions.ToIsoTimestamp()`, `JsonFileIO` atomic writes, `PlanStore.IsValidPlanId`,
transactional dialogs with a `SaveErrorBar` — and fixed diary column width, window-clamp-to-
monitor, the completed-task-shift data-loss bug (business rule 7), move-to-today backward
compaction, `ReviewDialog` reentrancy, three Add-Plan templates keying phases wrong, and
idle-detection double-counting. 07-16 fixed day-off/reschedule shifting to skip already-taken
days (`NextWorkingDay`/`PrevWorkingDay`). 07-17's full 5-category audit added
`TreatWarningsAsErrors`, the shared `CategoryStyle.cs` colour table, "Clear all my data", and
Settings autosave; same day, day-off scoring shipped (business rule 10).

**2026-07-18 → 07-22**: four audit rounds (~60 findings, 0 Critical), each fixed same-day, tests
19→83. `ScoreService.CurrentStreak`/`ReportData.WeekStats` took an optional `asOf` (a silent
streak-bonus bug when editing past entries); closed-form `PlanDayForDate`/`DateForPlanDay`;
`CredentialStore.Delete` + "Disconnect TickTick"; a full-history scan found 42 overlapping
`time_diary` pairs, only 2 matching the known bug — **user's call: leave the data untouched**;
personal-data git-history purge (134 commits, `git filter-repo`). Then: late-day task reminder;
`AppNames.Sub()` "File Explorer" case; the diary-tracking-gap bug resolved via `PollOnce`'s
`HandleSessionLock`/`HandleSleepGap` call order; Reports slow-load (build-first-N); repo renamed
to `planillium`; **first public release v1.1.0** (unsigned, SmartScreen wall accepted). Then:
desktop shortcut; `DispatcherQueueTimer` root-caused (see Standing lessons); queued plan ideas
(v1.2.0); tray stuck-badge (`TaskbarIcon` disposing a reused `Icon`); Diary category/app
filtering; tray unread-dot recap; Settings overflow and sidebar/Reports score-label confusion.
`ActivityTracker.ActiveWindowTitle` falls back to the process name when title and `ExeAppNames`
are both empty (was producing ~118 bare "-" rows/day). Same week: `posting-plan`/`project-media`
skills bootstrapped; a Reddit launch post held by r/ClaudeAI's karma gate was reformatted for
the Megathread.

**2026-07-23/24**: full internal rename `MentorOverseer`→`Planillium` (3 legacy-compat values
deliberately untouched — see top of file); Diary App/Page filter split; Reports' redundant
"Exclusion Impact" panel removed (business rule 12); all 26 `ContentDialog` sites unified onto
`DialogControls.Build`; tray "Pause tracking". Root-caused "have to switch pages to see the new
day" (`NavigationCacheMode="Enabled"` never recomputed "today") via `StartDayChangeWatcher`,
extended to Plans/Reports after a re-audit caught the gap; note-wipe risk fixed
(`TaskNoteView.AnyEditInProgress`, later strengthened to persist drafts across any rebuild);
Schedule re-snap-on-refresh; Diary filter-row overflow; VACUUM moved off the UI thread; docx
zip-bomb check switched to counting real decompressed bytes; `VacuumAndCheckpoint()` truncates
the WAL. Four 5-category audit rounds (24+22+22+12 findings, 0 Critical) plus two re-audits, all
fixed same-day, 86/86 tests each time.

**2026-07-27**: app wouldn't start at all — bisected to the 07-24 DLL-hardening
`SetDefaultDllDirectories` call breaking WinRT activation of the bundled WinUI3 DLLs; the real
`DllImport`s are protected KnownDLLs regardless of search order, so removal was a clean revert
(`4d0f161`). Separately: the diary appeared to start whenever the PC was first touched rather
than at the configured hour, because the wake-from-sleep toast only logged a gap when no UI
handler was wired (never true in the running app). A first fix (an evening-review sweep) was
**rejected** — the user wants it logged immediately, matching the old Python guarantee.
`HandleIdleReturn` now always logs "unaccounted time" the instant a gap is detected
(`d3373a5`).

**2026-07-28**: Diary Edit/Split buttons unreachable — root cause was `Card()`'s rounded
`CornerRadius` corner-clipping (see Standing lessons), found after 4 dead ends; fixed with an
explicit `MinWidth`. Missed-notification recap replayed a prompt's text with no action behind
it — `PendingNotification` now round-trips the toast's own args. **Feature**: plan tasks gained
a `tools` list, taught into `activity_rules.on_plan` via `TeachPlanTools` (with a confirmation
naming what was learned); both live plan files were hand-edited to add tools, after
cross-checking every existing `task_completions`/`task_overrides`/`task_notes` row's task text
against the new JSON (zero mismatches — that check, not schema validity, was what mattered); 12
new keywords taught. Then: `TeachPlanTools` wired into queued-plan activation; diary description
AutoSuggestBox; `LearnActivityRule` refuses a bare browser name; Reports width cap pulled into
shared constants; Diary "Show more" batched at 50. Then a **full 5-category audit**: security
clean, 7 findings — Show-more progress lost to the 30s refresh; the idle placeholder literal
centralized as `DiaryCategory.IdlePlaceholder`; `BuildDiarySection` split 389→294 lines (the
pieces with no shared mutable state only — `RenderDiaryResults` deliberately left inline); three
dedups. One accepted as-is: `PlanStore.ActivateQueuedPlan` writes then deletes non-atomically —
narrow, self-healing, **settled, not an action item**. `56cbdb7`.

**2026-07-29**: `SplitDiaryEntryDialog`'s "+ Add activity" never had its `Click` wired (confirmed
pre-existing via `git show`; the near-identical `IdleReturnDialog` button was correct, so not a
sibling-drift case). Then: the Diary's Category/App/Page/search filters didn't narrow each
other — the dropdown *option* lists were built from unfiltered rows while the results applied
all four. Fixed by extracting four predicates and building each dropdown from rows matching every
*other* active filter (faceted-search shape), so options and results share one definition. A
read-only query against the real DB confirmed some Chrome/LinkedIn rows genuinely are `off_plan`
(manually recategorized), so LinkedIn legitimately still appears under Category=Off-plan — correct,
not a bug. Also: idle rows now show the typed answer in the Page column instead of "—", and
durations lost their parentheses.

**2026-08-04**: four user requests, then four follow-ups. (1) *Diary window*: was hardcoded
06:00–20:00 inside `ActivityTracker`, unrelated to working hours, so moving the working day to
08:00 still logged and back-filled every morning from 06:00. Briefly given its own `diary_hours`
config block and Settings pair; the user's call the same day was to **merge it into working
hours** — one pair of hours, `InDiaryHours` collapsed into `InWorkingHours`, Settings boxes
removed, a stray `diary_hours` block now inert and stripped on next save. `SaveRules` now rejects
work start ≥ end (inverted, that's not a short day, it's no tracking at all). Two display strings
that hardcoded "06:00–20:00" now read the live values. (2) *Reports not rolling over overnight*:
not the day-change watcher (it did re-render) but `_diaryDate`, a static seeded once at class
load — added `_diaryFollowsToday`, set via a single `GoTo` all four date controls route through.
(3) *"Day X of Y"* → `Plan.ProgressDay`, business rule 13; the user resolved the "hole further
back" ambiguity themselves ("if I want to skip day 10 I do replanning"), which is what makes
stall-on-first-unfinished-day safe. (4) *Reports totals*: shared `AddTotalsRow` under both summary
tables; Tasks/Score columns deliberately blank. Follow-ups: **every** scoring rule became editable
via a new SCORING section in Settings, built from a new `ScoringRules` table that also feeds the
formula and the config lookup (see Standing lessons); and **`ActivityTracker`'s God-Object split
finally landed** (deferred since 07-23) — 855→597 lines, with `NativeInput` (Win32 P/Invoke),
`WindowTitleResolver` (title decoration + pid cache), `ActivityClassifier` (keyword matching) and
`DiaryWriter` (the two `time_diary` statements) extracted. Only pieces owning state nothing else
touched were moved; the poll loop's interlocking session/idle/alert state stayed put, and
`EffectiveClass` stayed with it because it reads `PaidUntil`. Public surface unchanged
(Classify/ClassifyIdleText/StripUnreadBadge remain as forwarders). Verified: clean build
(0 warnings) + 120/120 tests, Release rebuilt and relaunched, plus live UI Automation against a
scratch-root instance (see Standing lessons for the technique) — all three screens read "Day 1 of
28"/"Day 1 of 160" at calendar day 8 with "7 day(s) late — from day 1" beside them; Reports totals
aligned to their columns with real bounding rects (no repeat of the 07-28 clipping); all four
diary date controls stepped correctly and a past day stayed pinned across four page switches.

**2026-08-04 (evening)**: two UI requests. (1) *Settings* — asked for either a menu or collapsible
sections, with the choice left to whichever is better. Chose **collapsible `Expander`s** (seven:
General / Hours & reminders / Scoring / Activity keywords / Idle-answer library / TickTick / Data).
The deciding argument was not aesthetics: this page **saves as one group** — `SaveRules` writes
working_hours, reminders, retention, both keyword blocks and scoring in a single `Mutate` — so a
menu would scatter those across destinations while the save still wrote all of them, persisting
fields the user never navigated to. Seven groups is also below the size where a nav level earns a
permanent slot, and a menu shows one group at a time so finding a setting requires already knowing
its category. WinUI's own guidance names Expander for settings groups (manual visibility toggling
is its documented anti-pattern). Each header carries a **live summary** (`RefreshSummaries`, read
from config not from the controls, so a header can never advertise a value that failed validation)
— the collapsed page is an at-a-glance overview rather than seven closed doors. Tracker status and
`SaveStatus` deliberately sit outside every section: status isn't a setting, and a save triggered
from one section must stay visible after you collapse it. (2) *Reports* — everything above the
diary now follows the period selector. The summary table, distractions and time-by-app already
did; the **score card always showed today** and the **insights were always computed from this
week** regardless. New `ReportData.PeriodStats` aggregates score/tasks/minutes over the selected
period via one `DailyMinutes` pass (raw `time_diary` + `diary_daily_rollup`, two queries not 365)
plus in-memory per-day scoring. Scores are recomputed rather than read from `score_ledger`
deliberately: the ledger only holds days the app was running to credit, so a stretch where it
wasn't open would read as zero rather than as what those days earned. Card relabelled "SCORE
EARNED — <period>" to keep it distinct from the sidebar's BALANCE (all-time, and net of
purchases). Verified live: all four periods relabel and re-scope every block, and the Year card's
minutes (56h10m / 21h40m) **match the summary table's own Total row exactly** — two independently
computed paths agreeing, which is the check that matters here given this page's history of two
similar numbers disagreeing. Settings verified via UIA rects: keyword boxes 221px × 3 across the
full width, all 12 scoring boxes in two 337px columns, nothing clipped. 124/124 tests (4 new,
covering PeriodStats' boundaries and nesting invariants).

- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **The diary's midnight rollover has never been observed actually happening** — every other
    part of that fix was verified live, but the rollover itself needs the clock to cross midnight
    with the app sitting on Reports. If the diary still shows yesterday some morning, the
    assignment at the top of `BuildDiarySection` is the first place to look.
  - **The 2026-08-04 scoring Settings section and the tracker split have not been exercised in
    the live app** — clean build and 120/120 tests only. The split is behaviour-preserving by
    construction (moved code verbatim, forwarders left behind) but it touches the poll loop, which
    is this project's highest-risk file.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field specifically (not
    "App Service URL").
  - **Settled, not action items** (recorded so they don't get re-opened): the 42 overlapping
    `time_diary` pairs from 06-29→07-16 — only 2 match the known `HandleActiveSession` signature,
    the rest are 1-2 minute boundary artifacts with no confirmed cause, and the user's call
    (2026-07-18) was to leave the data untouched, revisiting only if a mechanism turns up on its
    own; and `PlanStore.ActivateQueuedPlan`'s non-atomic write-then-delete (2026-07-28).
  - **Resolved-and-closed, kept as one-line pointers for date reference** (prose in git log):
    internal `MentorOverseer`→`Planillium` rename, 2026-07-23; diary-tracking-gap bug, 2026-07-21
    (`PollOnce` call order); LinkedIn/Reddit autonomous publishing for `posting-plan`, dropped
    2026-07-22 (APIs gated/unsuitable — a dormant Reddit OAuth2 tool is kept at
    `~/Desktop/CLAUDE/skills/posting-plan/tools/reddit-publish/`); `PlanDayForDate` closed form,
    2026-07-18; TickTick client secret rotated 2026-07-09 and **reconnected in the app 2026-08-04**
    (the stale-credential follow-up is closed); personal-data git-history scrub, 2026-07-18;
    v1.1.0 + GitHub Release + repo flipped Public, 2026-07-21; stray duplicate
    `devbaghda/planillium` repo deleted 2026-07-21; tray icon vanishing after a click — user
    confirmed fine 2026-08-04; the 2026-07-17 "keyboard/dark-mode/timing" live-check item — user
    closed it 2026-08-04.

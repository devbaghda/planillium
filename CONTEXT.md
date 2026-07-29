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
two same-day fixes pushed the file back past the ~300-line threshold; ~578→~566 lines later the
same day after a third same-day round (condensed the 07-23/24 and 07-27 entries into tighter
prose; left this same-day round's own entries less compressed since they're the freshest and
most likely to matter next session — revisit at the next compaction pass); ~628 lines after a
fourth same-day round (a full audit + remediation pass) — the older 07-18-through-07-22 and
07-27 paragraphs were re-checked and are already near their practical compression floor (further
squeezing risks dropping the still-open "keyboard/dark-mode/timing" TODO or other facts), so this
pass added its own entry without re-compacting old ones; a genuine re-compaction is still owed
next time this section is touched, once today's four entries are no longer the freshest thing in
it)._

- **2026-07-23/24, condensed** (full detail in git log): full internal rename
  `MentorOverseer`→`Planillium` (3 legacy-compat values deliberately untouched — see top of file);
  Diary App/Page filter split; removed Reports' redundant "Exclusion Impact" panel (business
  rule 12); all 26 `ContentDialog` sites unified onto one `DialogControls.Build` factory; tray
  "Pause tracking" toggle added; `ActivityTracker`'s 819-line God-Object split **deferred** by
  user call — see Open TODOs. Root-caused/fixed "have to switch pages to see the new day"
  (`NavigationCacheMode="Enabled"` never recomputed "today" overnight) via `StartDayChangeWatcher`,
  first on Today/Schedule then extended to Plans/Reports once a re-audit caught the gap; fixed a
  note-wipe risk (`TaskNoteView.AnyEditInProgress`, later strengthened so drafts persist across
  any rebuild, not just the watcher's); Schedule re-snap-on-refresh fix; Diary filter-row overflow
  fix; VACUUM-on-diary-prune moved off the UI thread (then back off again after a regression);
  docx zip-bomb check switched from trusting header size to counting real decompressed bytes;
  `VacuumAndCheckpoint()` now truncates the WAL too. Four full 5-category audit rounds across the
  two days (24+22+22+12 findings, 0 Critical, plus two re-audit passes) all fixed same-day each,
  verified via clean build + 86/86 tests each time.

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
- **2026-07-27**: App wouldn't start at all (instant `XamlParseException` on every launch).
  Git-bisected to `ece15f5`'s `SetDefaultDllDirectories` call (07-24 DLL-hijack hardening)
  breaking WinRT's native activation of the bundled WinUI3 DLLs — sat broken-but-unrelaunched
  for 3 days (see Standing lessons). The app's real `DllImport`s are protected KnownDLLs
  regardless of search order, so removing the call was a clean revert. Fixed, `4d0f161`, pushed.
  Separately: Diary appeared to start whenever the PC was first touched each morning instead of
  the configured 06:00, because the wake-from-sleep "welcome back" toast only logged a gap if a
  UI handler was *not* wired (never true in the running app) — a missed toast meant that stretch
  never entered the diary. First fix attempt (an evening-review gap sweep) was **rejected by the
  user** — they wanted it logged immediately, matching the old Python app's guarantee. Fixed:
  `HandleIdleReturn` now always logs `"unaccounted time"` the instant a gap is detected;
  `ClearIdlePlaceholder` swaps it for a real answer later without creating a duplicate row.
  Verified: clean build + 86/86 tests + relaunch, `d3373a5`, pushed.
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
- **2026-07-28 (feature)**: User request: when Claude generates a plan, it should also list the
  apps/tools/websites each task needs, and that list should feed into Planillium's existing
  on-plan/off-plan "library" (Settings' ACTIVITY KEYWORDS section, backed by config.json's
  `activity_rules.on_plan`/`off_plan` keyword lists — the same mechanism `ConfigService.
  LearnActivityRule` already exposed for the Diary's manual "mark selected as on-plan" bulk
  action). Implementation: `PlanTask` gained a `Tools` field (list of strings, additive content
  like `mentor_note`/`detail` — a plan without it just has nothing to teach); all 3 prompt
  templates (`PlanTemplates.cs`) now ask for a per-task `"tools"` array, with explicit guidance
  to name things precisely (e.g. "Chrome - Coursera" not bare "Chrome") since these become
  substring-matched keywords against real window titles (`ActivityTracker.Classify`) — an
  over-broad name would misclassify unrelated activity as on-plan. `AddPlanDialog.TryImport`
  deserializes the freshly-written plan and extracts `PlanStore.DistinctTools(plan)`
  (case-insensitively deduped across every task, added to `PlanStore`); the actual teaching
  (`ConfigService.LearnActivityRule` per tool, then `RestartTracker`) plus a one-button "Learned
  new on-plan apps" notice naming what was taught (pointing back to Settings for edits/removal)
  live in a new shared `Dialogs/TeachPlanTools.RunAsync`, called once the import dialog itself
  has closed — since silently changing something that affects scoring/alerts felt wrong to do
  with zero visibility. Only wired for a real, non-queued import at this point; a queued idea's
  tools aren't taught until/unless it's later activated (`PlanStore.ActivateQueuedPlan` doesn't
  currently repeat this step — see Open TODOs). `TaskDetailDialog` also gained a "TOOLS" section
  so a task's list is visible/traceable from the task itself, not just discoverable via Settings.
  Verified: clean build (0 warnings), 90/90 tests (4 new, covering `PlanStore.DistinctTools`'s
  dedup/blank-filtering/empty-list behavior), and live UI Automation through the actual wizard
  (generated a real prompt, confirmed the schema/instructions render correctly) — stopped short
  of a full click-through import, since this app's two active plan slots were already full and a
  real import would have written a live plan file.
  **Same-day follow-up**: user asked whether tools could be added to plans already several days
  active, or whether they'd need deleting first. Answer given: never delete —
  `task_completions`/`task_overrides`/`task_notes`/`score_ledger` are keyed to plan_id + exact
  task text, and a fresh import of the same id is blocked outright (`TryImport`'s "already
  active" check) while a different id would sever all accumulated progress; `tools` is purely
  additive, so an in-place edit is always safe. User chose "you do it all": (1) hand-edited both
  live plan files (`plans/active/netherlands.json`, 76 tasks; `plans/active/
  claude-code-10-level-mastery.json`, 23 tasks) to add a task-appropriate `tools` list per task,
  leaving genuinely tool-agnostic tasks (in-person errands, waiting-on-a-process admin steps)
  with an empty list rather than forcing a keyword onto everything; (2) added the "Teach on-plan
  apps…" button described above so an already-active plan can pick up tools added after the
  fact — closes the "already active" half of the queued/already-active gap noted above (queued-
  plan activation still doesn't call it, see Open TODOs); (3) ran it once for both plans via the
  real button. Before writing either plan file, cross-checked every existing
  `task_completions`/`task_overrides`/`task_notes` row's `task_text` for both plan ids against
  the new JSON — zero mismatches, so progress/notes stayed linked (this check, not JSON-schema
  validity, is what actually mattered for safety here). Result: 12 genuinely new keywords taught
  (`Claude Code`, `Terminal`, `VS Code`, `Meetup`, `Eventbrite`, `IND.nl`, `Airbnb`, `Bunq`,
  `Zorgwijzer`, `Funda`, `Pararius`, `Kamernet`, `Power BI Desktop`) — several tasks named tools
  already present in `activity_rules.on_plan` from before this feature (`LinkedIn`, `Excel`,
  `Notion`, `Microsoft Learn`, `Power Apps`, `GitHub`), which `LearnActivityRule`'s existing dedup
  handled with no duplicates. Verified: clean build, 90/90 tests, and the actual "Teach on-plan
  apps" buttons clicked live (not just code-reviewed) — confirmed via the resulting confirmation
  dialogs' exact keyword lists and a before/after read of `config.json`'s `activity_rules.on_plan`
  array.
- **2026-07-28 (same-day follow-up round)**: user gave three more requests plus two mid-turn
  interjections, all landed in one pass. (1) Wired `TeachPlanTools.RunAsync` into
  `PlanStore.ActivateQueuedPlan`'s two UI callers (`PlansPage`'s "Start now" and
  `StartQueuedPlanDialog`) — closes the queued-plan gap noted above; a queued idea's tools are now
  taught the moment it's activated, not just left for a later manual button click. (2) Diary's
  Edit/Split dialogs' description field was a plain `TextBox`, forcing recurring descriptions
  ("lunch", "dog walk") to be retyped by hand every time — added `Database.MostFrequentDescriptions()`
  (top-N by count, excluding the placeholder `dismissed`/`unaccounted time` values) and switched
  both dialogs to `AutoSuggestBox` filtering that list as you type. (3) User flagged that teaching
  a bare app/browser name (e.g. "Chrome") as on-plan is too coarse — a browser hosts both on-plan
  and off-plan content depending on the tab. `ConfigService.LearnActivityRule` now returns `bool`
  and refuses any keyword exactly matching `AppNames.Browsers` (made `internal` for this), so
  neither the Diary's bulk mark-selected action nor `TeachPlanTools` can teach a whole browser as
  one category; Settings' own free-text ACTIVITY KEYWORDS boxes write `config.json` directly and
  are deliberately left unguarded (a power-user manual-edit surface, not an automatic guess).
  6 new unit tests. *Mid-turn interjection*: Reports page's content column was capped at a flat
  880px regardless of window size, so the Diary section's "Split" button needed horizontal
  scrolling even with empty space on both sides of a wide window — pulled the cap into shared
  `ReportsPage.DiaryListWidth`/`DiaryCardWidth` constants and widened the page's own
  `maxContentWidth` to match, without touching any other Reports section's layout. *Second
  mid-turn interjection*: Diary's "Show more" link used to reveal every remaining hidden row (up
  to `maxSearchResults = 300`) in one click; changed `DiaryList` to a self-re-adding
  `AddShowMoreIfNeeded` local function revealing 50 at a time, relabeling/re-adding itself while
  rows remain. Verified live: clicking it three times in one script (to avoid the diary's own
  30-second live-refresh timer resetting the reveal count mid-test, a pre-existing behavior not
  specific to this fix) grew the row count 40→90→140→190, exactly +50 each time, with the label
  staying accurate. Verified overall: clean build (0 warnings), 96/96 tests (10 new total), and
  live UI Automation for every piece — the two already-active plans' "Teach on-plan apps" buttons
  (from the earlier entry above) plus this round's AutoSuggestBox popup (real past descriptions),
  Reports button bounding rects (0/40 out of bounds), and the Show-more batching all confirmed via
  real clicks/coordinates, not just code review.
- **2026-07-28 (full 5-category audit + remediation)**: ran all five `windows-app-auditor`
  passes directly (parallel sub-agents were offered, user declined; ran sequentially with own
  Read/Grep/Bash instead) against the whole app, weighted toward today's least-scrutinized new
  code. Security came back clean (0 Critical/High/Medium) — SQL fully parameterized, table names
  always from a fixed internal list, plan-id path traversal already guarded, docx zip-bomb check
  already correct (real byte-count, not header size), `app.manifest` already `asInvoker` +
  PerMonitorV2, no telemetry beyond TickTick's own API. 7 real findings, all fixed except one
  accepted-as-is:
  1. **(Medium, fixed)** Diary's new batched "Show more" silently lost its progress every 30
     seconds while viewing "today"/"All time" with no search — `RenderDiaryResults()`'s periodic
     live-refresh call rebuilt `DiaryList()` from scratch, and the revealed-row count was a local
     variable with no persistence. Fixed via two new static fields (`_diaryRowsShown`,
     `_diaryRowsShownScopeKey`) — `RenderDiaryResults` computes a scope signature (day/search/
     filters/all-time) and only resets the count on an actual scope change, not a same-scope
     refresh tick. Verified live: revealed 90 rows, waited 35+ real seconds (past the 30s
     interval), still showed 90; toggling "All time" off (a genuine scope change) correctly
     dropped back to 40.
  2. **(Medium, fixed)** The idle-answer placeholder pair `"unaccounted time"`/`"dismissed"` was
     hand-typed in 4 places across 3 files (`ActivityTracker.cs` ×2, `IdleReturnDialog.cs` ×2,
     `Database.cs` ×2 queries, one of them added this same session) with no shared constant —
     exactly the kind of duplicated-literal drift this project has hit before (this pair was
     already renamed once, from "dismissed"). Centralized as `DiaryCategory.IdlePlaceholder`/
     `LegacyIdlePlaceholder`; all 6 call sites now reference it (2 of Database's now parameterize
     the SQL `IN (...)` instead of inlining the literals, matching the file's existing style).
  3. **(Medium, fixed — user confirmed via AskUserQuestion, since a structural change is a
     decision not a mechanical fix)** `ReportsPage.Diary.cs`'s `BuildDiarySection` was 389 lines,
     mixing search/filter UI, the mark-toolbar, the list area, and two timers' setup in one
     method — now the single largest file in the app, surpassing the already-known
     `ActivityTracker.cs` God Object. Split the 4 pieces that build UI with **zero shared mutable
     state** into their own methods (`BuildDiarySearchBox`/`BuildDiaryFilterRow`/
     `BuildDiaryMarkToolbar`/`BuildDiaryResultsArea`), using tuple-deconstruction return values
     (`var (categoryBox, appBox, ...) = BuildDiaryFilterRow();`) so every downstream reference in
     `RenderDiaryResults`/event-wiring needed **zero changes** — same identifiers, same closures.
     `RenderDiaryResults` itself (~130 lines) deliberately stayed inline: it's the one piece that
     genuinely shares mutable state (`selectedIds`/`lastRows`/`syncingFilters`) with the rest, and
     forcing it apart would risk the exact closure-capture bug class this file's own old doc
     comment was warning about. Net: 389 → 294 lines (real reduction, not "now under 80" —
     said so plainly rather than overclaiming). Verified live post-split: all 3 filter combo
     boxes, the mark-toolbar buttons, and the Show-more batching (40→90 again) all still render
     and behave identically.
  4. **(Low, fixed)** A comment in `AddPlanDialog.cs` (written earlier this same session) said a
     queued plan's tools "aren't taught until/unless activated" and that `ActivateQueuedPlan`
     "doesn't currently repeat this step" — but fix #1 of the earlier same-day round already
     closed that exact gap. Updated the comment to describe current behavior.
  5. **(Low, fixed)** `StartQueuedPlanDialog.ShowAsync` and `PlansPage`'s `QueuedRow` "Start now"
     handler independently re-implemented the identical "activate → reload from disk → teach
     tools" sequence. Extracted `TeachPlanTools.ActivateQueuedPlanAsync(xamlRoot, planId,
     logContext)`; both call sites now share it.
  6. **(Low, fixed)** `EditDiaryEntryDialog.cs`/`SplitDiaryEntryDialog.cs` each hand-typed the
     identical ~10-line AutoSuggestBox filter-as-you-type wiring added earlier this session.
     Extracted `DialogControls.WireFrequentSuggestions(box, frequent)`; both now call it.
  7. **(Low, accepted/not fixed)** `PlanStore.ActivateQueuedPlan` writes the new active-plan file
     then deletes the queued one as two separate non-atomic steps — a crash in between would
     briefly duplicate the plan in both folders. Narrow window, self-healing (re-activating just
     repeats both steps), not worth a cross-file transaction for this. Left as-is, documented here
     rather than silently dropped.
  Verified overall: clean build (0 warnings) + 96/96 tests + live relaunch after every batch, per
  the `remediation-loop.md` discipline. Committed `56cbdb7`, pushed.
- **2026-07-29**: user reported `SplitDiaryEntryDialog`'s "+ Add activity" button did nothing —
  stuck at exactly the two starting rows. Root cause: the button was created and added to its
  toolbar, but `addRowBtn.Click` was never actually wired to call `AddRow` — confirmed pre-existing
  via `git show` on a commit from before this whole session started, so not a regression from any
  of today's/yesterday's work. `IdleReturnDialog`'s own near-identical split-mode "+ Add activity"
  button (same `AddRow` pattern this dialog was clearly modeled on) already wired its `Click`
  correctly, so this wasn't a "fix one sibling, miss the other" case — just a wiring line dropped
  when this dialog was first written. Fixed by adding `addRowBtn.Click += (_, _) => AddRow(null,
  category, null);` (same defaults as the second seed row). Verified live: clicking it twice grew
  the row count 2→3→4 via real `InvokePattern.Invoke()` calls against the actual dialog, cancelled
  before closing (no live data mutated). Clean build + 96/96 tests.
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
- **2026-07-29**: user reported the Diary's Category/App/Page filters and search box weren't
  interconnected — picking Category = Off-plan still left Chrome/LinkedIn selectable in the App/
  Page dropdowns as if the category filter weren't applied. Root cause: `appsInView`/`pagesInView`
  (the lists that populate the App/Page dropdown options) were computed straight from the full
  unfiltered `rows` for the day, while the actual results list applied all four filters — so the
  dropdown *options* never narrowed even though the *results* were already filtering correctly.
  Fixed by extracting the four filter checks (`MatchesCategory`/`MatchesApp`/`MatchesPage`/
  `MatchesSearch`) into local predicate functions, then building each dropdown's option list from
  rows matching every *other* active filter (standard faceted-search shape) and building the
  results list from all four ANDed together — same predicates, so dropdown options and results
  can never disagree. Separately verified via a **read-only** query against the real
  `data/progress.db` (loaded the app's own compiled SQLite DLLs into PowerShell,
  `Mode=ReadOnly`) that some real Chrome/LinkedIn diary rows genuinely are `off_plan` — specific
  named-contact LinkedIn messaging sessions the user had manually recategorized via the Diary's
  bulk "mark selected as off-plan" action — so LinkedIn legitimately can still appear as a Page
  option under Category=Off-plan; that's correct behavior, not a bug. Live-verified the App
  dropdown narrowing from 26 options to 4 after applying Category=Off-plan via UI Automation;
  Page-dropdown narrowing and search-box interconnection were verified by code inspection only
  (all four dropdowns/results share the same predicate functions, so the mechanism is identical)
  rather than further live clicking. Clean build, 96/96 tests. Not yet committed/pushed as of this
  entry — awaiting explicit instruction per this repo's commit convention.
- **2026-07-29 (same-day follow-up)**: user reported two Diary display issues from a screenshot —
  idle rows showing what they'd typed answering "what were you doing" (e.g. "airbnb") only in the
  details column next to the duration, with the Page column still showing a bare "—"; and every
  entry's duration wrapped in parens ("(12m)"). Root cause of the first: `AppNames.Sub(window)`
  returns null for idle (and anything else with no app-detected sub-item), so the Page column
  always fell back to "—" regardless of whether the user had actually answered — the answer
  itself only ever reached the separate `desc` field shown in the details column. Fixed in
  `DiaryList`/`BuildRow` (`ReportsPage.Diary.cs`): when there's no detected page AND a
  description exists, the description now displays in the Page column instead of "—" (italicized,
  same visual cue the details column used to use, to mark it as user-typed rather than
  app-detected), and is no longer duplicated in the details column for that row. Entries that
  already have a real detected page (e.g. a Chrome tab's actual site) are untouched — a
  description there still shows in the details column as a supplementary note, since it doesn't
  replace real page info. Second fix: dropped the wrapping parens from the details column's
  duration text unconditionally (`"(12m)"` → `"12m"`; `"“desc” (12m)"` → `"“desc” 12m"`). Both are
  pure-display changes — no filter/query/DB logic touched, so the interconnected-filters fix
  earlier this session is unaffected. Clean build, 96/96 tests; not live-UI-verified this round
  (a live UI Automation check was attempted and rejected earlier this session — relying on build +
  test + code review for this batch). Not committed/pushed yet.
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

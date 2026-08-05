# Planillium — Project Context

> Handoff document — **read start to finish** every session. The settled **business rules and
> standing lessons live next door in `DECISIONS.md`**, a lookup register to consult before
> changing anything in the areas it covers, not to read through.
>
> **Compaction threshold: 400 lines** — count with `wc -l`, don't estimate (PowerShell's
> `Measure-Object -Line` silently skips blank lines and under-reported this file by ~60).
>
> Compacted hard on 2026-08-04: 771→476, then 547→520 by condensing prose, and finally **535→347
> by splitting the two registers into `DECISIONS.md`** once it was clear the remaining bulk wasn't
> narrative to squeeze but reference material sitting in the wrong file. Editing prose could never
> have reached 400 — ~235 of those lines were specification and 106 were standing lessons, while
> the handoff notes this project's `CLAUDE.md` rule actually refers to were only ~139. Same move,
> same reason, as the DigiFlow project's own split. **Nothing was rewritten in the move.**

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

## Key business rules → `DECISIONS.md`

**Moved out 2026-08-04.** The 13 numbered rules with their full rationale — working hours as the
diary window, plan-day arithmetic, overdue accrual, the one-task-per-day steady state and why
Reschedule/Day-off and Move-to-today deliberately differ, archiving, the score floor and bonuses,
day-off scoring, queued plan ideas, `DriftDays`, and the progress-based "Day X of Y" counter.

**Read the relevant rule before changing anything it governs.** Several of them record a decision
the user made *after* I argued the opposite, and the reasoning is the point, not the rule text.

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

### Standing lessons → `DECISIONS.md`

**Moved out 2026-08-04.** Verification discipline, safety around real data (including the
scratch-`MENTOR_ROOT` technique for exercising the UI without touching live data), the WinUI
layout traps (rounded-`Border` clipping, `Expander` content stretch, `DispatcherQueueTimer`),
prompt/timer/state rules, and the design lessons. Every entry there cost a real bug to learn and
none may be dropped in a compaction.

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

**2026-07-23 → 07-29** (four dated rounds, condensed): internal rename `MentorOverseer`→`Planillium`
(3 legacy-compat values deliberately untouched — see top of file); Diary App/Page filter split;
Reports' redundant "Exclusion Impact" panel removed (business rule 12); 26 `ContentDialog` sites
unified onto `DialogControls.Build`; tray "Pause tracking"; `StartDayChangeWatcher` for "have to
switch pages to see the new day" (`NavigationCacheMode="Enabled"` never recomputed today), extended
to Plans/Reports after a re-audit caught the gap; note-wipe risk (`TaskNoteView.AnyEditInProgress`,
later strengthened to persist drafts across any rebuild); Schedule re-snap; Diary filter-row
overflow; VACUUM off the UI thread; docx zip-bomb check counts real decompressed bytes;
`VacuumAndCheckpoint()` truncates the WAL. Four 5-category audits (24+22+22+12 findings, 0 Critical)
plus two re-audits, all fixed same-day, 86/86 tests.
**07-27**: app wouldn't start at all — bisected to 07-24's `SetDefaultDllDirectories` breaking WinRT
activation of the bundled WinUI3 DLLs; the real `DllImport`s are protected KnownDLLs regardless of
search order, so removal was a clean revert (`4d0f161`). Same day: the diary appeared to start
whenever the PC was first touched, because the wake-from-sleep toast only logged a gap when no UI
handler was wired (never true in the running app); a first fix (evening-review sweep) was
**rejected** — the user wants it logged immediately, matching the old Python guarantee, so
`HandleIdleReturn` now always logs "unaccounted time" the instant a gap is detected (`d3373a5`).
**07-28**: Diary Edit/Split unreachable via `Card()`'s rounded-`CornerRadius` clip (Standing
lessons), found after 4 dead ends; missed-notification recap replayed a prompt's text with no action
— `PendingNotification` now round-trips the toast's args. *Feature*: plan tasks gained a `tools`
list taught into `activity_rules.on_plan` via `TeachPlanTools`; both live plan files were hand-edited
after cross-checking every `task_completions`/`task_overrides`/`task_notes` row's task text against
the new JSON (zero mismatches — **that** check, not schema validity, was what mattered); 12 keywords
taught; later wired into queued-plan activation. Also: diary-description AutoSuggestBox;
`LearnActivityRule` refuses a bare browser name; Diary "Show more" batched at 50. Then a full
5-category audit — security clean, 7 findings, including `BuildDiarySection` split 389→294 lines
(only the pieces with no shared mutable state; `RenderDiaryResults` deliberately left inline). One
accepted as-is: `PlanStore.ActivateQueuedPlan` writes then deletes non-atomically — narrow,
self-healing, **settled, not an action item**. `56cbdb7`.
**07-29**: `SplitDiaryEntryDialog`'s "+ Add activity" never had its `Click` wired (pre-existing per
`git show`; the near-identical `IdleReturnDialog` button was correct, so not sibling drift). Diary
Category/App/Page/search filters didn't narrow each other — dropdown *options* were built from
unfiltered rows while results applied all four; fixed by extracting four predicates and building
each dropdown from rows matching every *other* active filter, so options and results share one
definition. A read-only query against the real DB confirmed some Chrome/LinkedIn rows genuinely are
`off_plan` (manually recategorized), so LinkedIn legitimately appears under Category=Off-plan —
correct, not a bug. Idle rows now show the typed answer in the Page column instead of "—".

**2026-08-04** (one session, three rounds — all shipped, verified and pushed; commits `e4c4f11`,
`ee981c0`, `488424f`, `0ffdde4`). *Reported issues*: (1) the diary window was hardcoded 06:00–20:00
inside `ActivityTracker`, unrelated to working hours, so moving the working day to 08:00 still
logged and back-filled every morning from 06:00 — briefly given its own `diary_hours` config block
and Settings pair, then **merged into working hours** on the user's call (one pair of hours,
`InDiaryHours` collapsed into `InWorkingHours`, a stray `diary_hours` block now inert and stripped
on next save; `SaveRules` rejects work start ≥ end, since inverted that is no tracking at all).
Two display strings that hardcoded "06:00–20:00" now read live values. (2) Reports never rolled
over at midnight — not the day-change watcher (it did re-render) but `_diaryDate`, a static seeded
once at class load; fixed with `_diaryFollowsToday`, set through a single `GoTo` all four date
controls route through. (3) "Day X of Y" → `Plan.ProgressDay`, business rule 13; the user resolved
the "hole further back" ambiguity themselves ("if I want to skip day 10 I do replanning"), which is
what makes stall-on-first-unfinished-day safe. (4) Reports gained a shared `AddTotalsRow` under
both summary tables (Tasks/Score columns deliberately blank).
*Then*: all 12 scoring rules became editable (new SCORING section in Settings, built from a new
`ScoringRules` table that also feeds the formula and the config lookup — see `DECISIONS.md`).
**`ActivityTracker`'s God-Object split landed** (deferred since 07-23): 855→597 lines, extracting
`NativeInput` (Win32 P/Invoke), `WindowTitleResolver` (title decoration + pid cache),
`ActivityClassifier` (keywords), `DiaryWriter` (the two `time_diary` statements). Only pieces owning
state nothing else touched moved; the poll loop's interlocking session/idle/alert state stayed, and
`EffectiveClass` with it (reads `PaidUntil`). Public surface unchanged via forwarders.
*Then*: Settings became seven `Expander`s — **superseded next day, see below**. And **everything
above the diary on Reports now follows the period selector** (the card always showed today, the
insights always this week). `ReportData.PeriodStats` aggregates from one `DailyMinutes` pass (raw
`time_diary` + `diary_daily_rollup`, two queries not 365) plus in-memory per-day scoring; scores are
recomputed rather than read from `score_ledger` **deliberately** — the ledger only holds days the
app ran to credit, so a stretch where it wasn't open would read as zero. Card relabelled "SCORE
EARNED — <period>" to stay distinct from the sidebar's BALANCE (all-time, net of purchases).
*Verified*: 124/124, Release rebuilt/relaunched each round, live UIA on a scratch-root instance —
"Day 1 of 28"/"Day 1 of 160" at calendar day 8 with "7 day(s) late"; Year card minutes matched the
summary table's Total row exactly, two independent paths agreeing; nothing clipped; diary date
controls stepped correctly and a past day stayed pinned across page switches.
**2026-08-05** (same session, four further rounds). The Expander Settings was **rejected on sight**
("put an additional sub-menu on the right side, the settings pages should be of the same size with
no bouncing") and replaced by a right-hand `ListView`, one panel visible at a time. No-bouncing is
structural: page-grid row 1 is `*` (content rect follows the window, never the selection); all seven
panels share one grid cell (collapsed siblings aren't measured); the status strip is fixed-height,
since sizing to its text grows a line whenever a save message appears. The one-group-save objection
that had argued for Expanders doesn't apply — every panel stays loaded, so `SaveRules` writes the
same values whatever is shown. Summaries moved to the menu entries, keeping the overview property.
Sized for the **900dip minimum** (~388 for content): hours rows became star-column grids, Data
buttons 2×2, three headers shortened, `LayoutScoringGrid` reflows the 12 scoring inputs 1↔2 columns
off measured width; caps are column-level `MaxWidth` (control-level centres the control and rows
stop sharing a left edge). *Verified* UIA at 900 and 1500dip: pane/menu/status rects identical
across all seven sections at each width. Two defects looking would not have caught — every menu item
announced a **blank accessible name** (a `ListViewItem` with panel content derives none), and six
scoring boxes read `BoundingRectangle.Empty`, ambiguous between clipped and below-the-fold, resolved
via `ScrollIntoView`. 124/124.
*Then* ("align all the reports"): three left edges for one column unified into
`LabelColumnWidth`/`ColumnGap`/`SubRowIndent` (`ReportsPage.Styling.cs`), read by both tables, the
distraction list and `AppUsageRow`. *Verified* against a **copy** of the real DB (deleting stale
`-wal`/`-shm` sidecars first — see `DECISIONS.md`): label columns end 574, first figures start 586.
*Then* ("add all the categories and summary for the rows"): tables carried only on/off-plan; now
all five via `DiaryCategory.ReportOrder` (deliberately separate order from `EditableOptions`, test
asserts same five values) plus a per-row Total — **which changed meaning**, all tracked time not
on+off (Year total 80h10m→294h34m). New `ReportData.CategoryMinutes`; three near-duplicate queries
collapsed into one `DailyMinutes`, fixing two latent bugs found in the collapse: `MonthBuckets`
never read `diary_daily_rollup` at all, and the rollup's neutral/paid/idle columns had never been
read back despite being stored correctly. HTML/CSV exports match.
*Then* (decimal hours + Score everywhere): `FmtMins`→`FmtHours`, `"5,5 h"`, everywhere except the
diary (kept as h/m — a clock event, not a quantity); `Suggestions` had its own hand-formatted
string, now fixed too. Month/Year gained Tasks and Score columns and totals (a prior round's
explicit "leave blank" decision, overridden). Required day-off dates to contribute score while
contributing no minutes — `DailyRows` is now the one walk behind the week table, both bucket
tables and the score card, so a table's score total equals the card by construction; test asserts
it. Found by measuring at 900dip minimum: the Score column was **clipped off entirely** by the
card, a rounded `Border` clipping in silence — tables now sit in a horizontal `ScrollViewer`
(`Scrollable()`), confirmed reachable via `ScrollItemPattern.ScrollIntoView`. 132/132 tests.
*Then*: **"the app asks me about my absence twice and makes two identical recordings"** — a real
bug in `ActivityTracker.PendingDayGap` (the evening review's "where have you been?" sweep), two
compounding causes. (1) It judged only by `db.LastDiaryEnd()`, with no idea a session was
currently open — someone continuously active in one app right through review time read as having
been away for that whole stretch; answering it logged an idle row, then the still-open session
flushed a few polls later and logged the SAME span again as a real on/off-plan row. (2) It never
remembered what it had already asked about — Today's manual "Evening review" preview button calls
`ReviewDialog.ShowAsync` directly, bypassing the automatic once-a-day guard, so a second click
re-asked about and re-logged whatever the first had already covered. Fixed with two clamps inside
`PendingDayGap` itself: a new lock-protected `_openSessionStart` (mirroring `_sessionStart`,
written only by a new `SetSession` — replaced eight independently-typed assignment triples, one
per branch, which is exactly the shape that would have let a mirror added by hand miss a site)
caps how far the gap can extend; the existing `_accountedUntil` (already used by the poll-thread
path) now also caps how far back it can start. *Verified*: two new tests reproduce both bugs
directly against a real throwaway diary table (`ActivityTrackerPendingGapTests`) and fail without
the fix; 136/136 total. Root-caused via git archaeology, not guessing: traced through the
2026-07-27→07-28 history of `PendingLeadingGap`/`PendingDayGap` to find the one check that was
never added. Release rebuilt and relaunched (EOD checked first — 17:34 against a 20:00 review).
- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **The diary's midnight rollover has never been observed actually happening** — every other
    part of that fix was verified live, but the rollover itself needs the clock to cross midnight
    with the app sitting on Reports. If the diary still shows yesterday some morning, the
    assignment at the top of `BuildDiarySection` is the first place to look.
  - **The tracker split has not been exercised in the live app** — clean build and 124/124 tests
    only. It is behaviour-preserving by construction (moved code verbatim, forwarders left behind)
    but it touches the poll loop, which is this project's highest-risk file. *(The scoring Settings
    section was live-checked on 08-05: all 12 inputs present, correctly sized and reachable at both
    window extremes. Their **values** were still not edited live — that writes `config.json` and
    restarts the tracker, so it stays code-inspection-only.)*
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

# Planillium — Project Context

> Handoff document — **read start to finish** every session. The settled **business rules and
> standing lessons live next door in `DECISIONS.md`**, a lookup register to consult before
> changing anything in the areas it covers, not to read through.
>
> **Compaction threshold: 400 lines** — count with `wc -l`, don't estimate (PowerShell's
> `Measure-Object -Line` silently skips blank lines and under-reported this file by ~60).
>
> Compacted hard on 2026-08-04 (771→476→520→**347 by splitting the two registers into
> `DECISIONS.md`**, once clear the remaining bulk was reference material, not narrative to squeeze).
> **Nothing was rewritten in the move.**

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
across up to 3 active life/career plans simultaneously. It monitors his activity,
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
│   ├── active/             ← up to 3 active plan JSONs (e.g. netherlands.json)
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
- **App name constant:** `AppNames`, `AppInfo.MaxActivePlans = 3` (raised from 2, 2026-08-13)

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

**Through 2026-07-29** (detail in git log): WinUI 3 rebuild landed 07-07 as v1.0.0. Audit rounds
1-6 (07-09→07-15) introduced `Database.RunInTransaction`, `DateExtensions.ToIsoTimestamp()`,
`JsonFileIO` atomic writes, `PlanStore.IsValidPlanId` and transactional dialogs with a
`SaveErrorBar` — the mechanisms every later round built on — and fixed diary column width,
window-clamp-to-monitor, the completed-task-shift data-loss bug (business rule 7), move-to-today
backward compaction, `ReviewDialog` reentrancy, three Add-Plan templates keying phases wrong, and
idle-detection double-counting. 07-16 fixed day-off/reschedule shifting to skip already-taken days.
07-17 added `TreatWarningsAsErrors`, `CategoryStyle.cs`, "Clear all my data", Settings autosave, and
day-off scoring (business rule 10). 07-18→07-22 (four more audit rounds, tests 19→83):
`CurrentStreak`/`WeekStats` took an optional `asOf` (silent streak-bonus bug editing past entries);
closed-form `PlanDayForDate`/`DateForPlanDay`; `CredentialStore.Delete`; 42 overlapping `time_diary`
pairs found, only 2 matching the known bug — **user's call: leave untouched**; personal-data
git-history purge (134 commits); late-day task reminder; diary-tracking-gap fix; repo renamed
`planillium`, **v1.1.0 public**; `DispatcherQueueTimer` root-caused (Standing lessons); queued plan
ideas (v1.2.0); tray stuck-badge; Diary category/app filtering; `ActiveWindowTitle` process-name
fallback (~118 bare "-" rows/day fixed); `posting-plan`/`project-media` skills bootstrapped. 07-23:
internal rename `MentorOverseer`→`Planillium` (3 legacy-compat values deliberately untouched, see
top of file); Diary App/Page filter split; "Exclusion Impact" panel removed (business rule 12); 26
`ContentDialog` sites unified onto `DialogControls.Build`; `StartDayChangeWatcher` for the
new-day-without-switching-pages gap; note-wipe risk fixed; VACUUM off the UI thread; docx zip-bomb
check counts real decompressed bytes; four 5-category audits (0 Critical) plus two re-audits,
86/86 tests. **07-27**: app wouldn't start — bisected to 07-24's `SetDefaultDllDirectories`
breaking WinRT activation, clean revert; wake-from-sleep toast only logged a gap with no UI handler
wired, `HandleIdleReturn` now always logs "unaccounted time" the instant a gap is detected. **07-28**:
Diary Edit/Split unreachable via `Card()`'s rounded-clip (Standing lessons); plan tasks gained a
`tools` list (`TeachPlanTools`, 12 keywords, zero mismatches); diary AutoSuggestBox; "Show more"
batched at 50. **07-29**: `SplitDiaryEntryDialog`'s "+ Add activity" never wired; Diary filters
didn't narrow each other; idle rows show the typed answer in Page not "—".

**2026-08-04 → 08-05** (one continuous session, many rounds — commits `e4c4f11`, `ee981c0`,
`488424f`, `0ffdde4`): diary window merged into working hours (`InDiaryHours`→`InWorkingHours`,
`SaveRules` rejects work start ≥ end); Reports' midnight rollover fixed (`_diaryFollowsToday`, one
`GoTo`); "Day X of Y"→`Plan.ProgressDay` (business rule 13); shared `AddTotalsRow`; all 12 scoring
rules made editable (`ScoringRules` table feeds both the formula and Settings). `ActivityTracker`'s
God-Object split landed (855→597 lines): `NativeInput`/`WindowTitleResolver`/`ActivityClassifier`/
`DiaryWriter` extracted, public surface unchanged. Reports above the diary follow the period
selector throughout (`ReportData.PeriodStats`; scores recomputed, not read from `score_ledger`
**deliberately** — the ledger only holds days the app ran to credit); card relabelled "SCORE
EARNED" vs. the sidebar's all-time BALANCE. Settings' Expander layout **rejected on sight** ("no
bouncing"), replaced by a right-hand `ListView` (900dip min) — fixed blank accessible names + an
off-screen `BoundingRectangle.Empty` scoring-box bug along the way. Report tables aligned
(`LabelColumnWidth`/`ColumnGap`/`SubRowIndent`) and gained all 5 categories + a per-row Total —
**meaning changed**, Year total 80h10m→294h34m; new `ReportData.CategoryMinutes`; consolidating 3
near-duplicate queries into `DailyMinutes` fixed 2 latent bugs (`MonthBuckets` never read
`diary_daily_rollup`; its neutral/paid/idle columns were never read back). Hours switched to decimal
(`FmtHours`); Month/Year gained Tasks/Score totals via `DailyRows`. "Asks about absence twice": two
compounding causes in `ActivityTracker.PendingDayGap`, fixed via `_openSessionStart`/
`_accountedUntil` clamps. New `PageLayout.cs`/`CenterContent()` fixed the pre-07-28 width bug on
Today/Schedule/Plans. "Fill the gap with the following day's task" reverses the 07-09 no-gap-closing
call (business rule 7); `RescheduleTask` now runs one compact-then-push formula, future-only
(`ReplanOverdueDialog` still leaves past days alone). User approved closing the existing days-22/23
gap retroactively: `ScoreService.CompactFutureGaps`, one-time Debug button, DB backed up, verified
21→39, button removed. New `DiaryTag` axis (`time_diary.tag`, first ADD COLUMN migration) —
descriptive only, never read by ScoreService; 5 values (Routine/Documents/Studioshoo/Selfdev/
Procrastination); hit the `ContentDialog` width-cap bug a third time, fixed. Verified via live UIA
on scratch-root instances throughout; ended the arc at 144/144.

**2026-08-07**: two user reports on Tag/idle-answer. (1) Tag was riding inside the diary row's
Details column as a suffix — now its own fixed-width column (`DiaryList`/`BuildRow`, "—" when
unset). (2) `IdleReturnDialog` had no Category/Tag field — category was silently re-derived via
`ClassifyIdleText`, no way to correct the guess, tag didn't exist there. Added explicit combos:
single mode auto-fills Category from the guess and keeps re-guessing until touched (`catTouched`
flag), then stops; split rows are manual only. Chip click now fills the description field instead
of submitting instantly, to leave room to see the new fields. `DiaryWriter.LogSession` gained `tag`;
`LogIdleAnswer(s)` now take explicit category(+tag) instead of deriving it. 3 new tests, 147/147,
**not yet live-UIA-verified** (see Open TODOs).

**Then** ("check if the score balance is calculated correctly"): audited read-only against a DB copy,
no dupes/gaps/unknown reasons. Found a real bug: today's `daily_score` frozen at 0 despite 51 more
on-plan minutes tracked since — a diary edit recomputes that date's score regardless of whether it's
*today*, but `ReviewDialog.PersistReview` used skip-if-present logic for today's real end-of-day
credit, so the earlier incidental recompute pre-empted it forever. Fixed: `PersistReview` now calls
`RecalculateDayScore(today)`. **Real-data fix, user-confirmed**: deleted stale `score_ledger` row
140, backed up first; 08-06 had the same problem locked in (formula gives 31, ledger held 0),
corrected row 138 to delta=31 after the same confirmation, balance -4→27. Checked siblings:
`ReviewDialog`'s disabled "Close the day" button could still fire Enter while disabled — hardened,
also found in `SpendDialog`/`SplitDiaryEntryDialog`/`IdleReturnDialog` split mode. 147/147.
**Then** ("Mark … buttons for tags"): second toolbar row under Mark on-plan/off-plan/neutral, one
button per `DiaryTag.Options` entry plus "Clear tag." `MarkSelectedDiaryRowsTag` doesn't share a
helper with `MarkSelectedDiaryRows` (category changes also touch activity-rule learning + score
recalc; tag doesn't). No dedicated test — page-level UI, outside test scope.

**2026-08-13**: new `ScoreService.ManuallyMarkedDaysOff` (distinct dates with a `plan_days_off` row,
narrower than `AllPlansScoringExempt`/`ScoringExemptDates`, which also count recurring rest days) —
shipped as a standalone always-three-numbers Reports card + Schedule summary, then corrected same
session ("everything besides the diary should update based on the chosen timescale... we do not
need an additional card... remove the day-offs info from the schedule page"): reworked into
`ReportData.PeriodTotals.DayOffs` (bounded period-start-through-today like every other figure
there), folded into the score card's caption line; Schedule's line removed. Tests:
`ManuallyMarkedDaysOffTests.cs` + two `ReportPeriodStatsTests.cs` additions. 152/152.

**Real finding while verifying**: `dotnet test -c Release` was silently running the whole suite
against the REAL `data/progress.db` — `TestRootFixture`'s `MENTOR_ROOT` override lived behind
`#if DEBUG`, and since `AppPaths.cs` is source-linked (not project-referenced) into the Tests
project, a Release run compiled it with `DEBUG` undefined, dropping the override. Left 112 orphaned
rows (fake plan ids) across `task_overrides`(78)/`task_completions`(14)/`plan_days_off`(20), plus —
found later via a screenshot, "theres a mess there" — 42 fake `time_diary` rows on 08-07 and 08-13
(`TestWindow`/`test-window-<guid>` windows, `idle-split-`/`idle-single-<guid>` descriptions, one
invalid category `some_future_category`); all deleted after backup + confirmation, the one genuine
08-13 row (a real "unaccounted time" gap, 14:52–18:00) kept. `score_ledger` isn't plan-scoped, so a
test crediting "today" could in principle have clobbered a real row — not retroactively auditable,
nothing further done beyond rows 138/140 already fixed 08-07. **Fix**: override now also requires
`PLANILLIUM_TESTS`, defined unconditionally in both configs. 151/151 both configs; real row counts
confirmed unchanged after a post-fix Release run. **Lesson**: a `#if DEBUG`-gated test-only hook is
only as safe as "tests always build Debug."

Same screenshot also asked to remove the diary's horizontal scrollbar — root cause: `DiaryListWidth`
was stale (set 07-23, never updated for the 08-07 Tag column), so the row had outgrown the page's
own max width, forcing the scrollbar on any window. Recomputed `DiaryListWidth` (950→870), narrowed
Page (210→130)/Details (260→160) — both already ellipsis-trim with a tooltip — switched scroller
`Visible`→`Auto`.

**"I am afraid you deleted also the real data from today starting from 8 am"**: the pre-delete
backup already had nothing before 09:00, so the delete (test-fingerprint-only) didn't touch it.
Dug further: the log shows a genuine 08:00→11:28 gap, and `HandleIdleReturn` unconditionally logs
an "unaccounted time" placeholder the instant that's detected — that row should exist and doesn't,
in the backup or now. **Left open**: no error logged, cause not found.

**"Increase the ongoing projects number to 3"**: `AppInfo.MaxActivePlans` 2→3, single source of
truth, confirmed via grep no hardcoded 2-count assumptions anywhere. `MANUAL.md`/`README.md`
updated to match.

**2026-08-14** ("the plan that finished should not have moved... I do not have a way to restore
it"): `claude-code-10-level-mastery.json` was sitting in `plans/archive/`, but only 11/23 tasks had
ever had a completion event (10 done, 1 unmarked) — well short of the 100% `PlansPage`'s Archive
button requires to even be clickable (`IsEnabled = complete`). Rules out the app's own Archive flow;
likely moved outside the app — **unconfirmed, no log or reliable timestamp survived**. File verified
intact (4 phases, 23 tasks) and moved back to `plans/active/` (same op as the app's own Restore
button). Archive/Restore only ever moves the JSON; DB rows (`task_completions`/`task_overrides`/
`plan_days_off`/`task_notes`, keyed by plan_id) are untouched by either — confirmed all 4 tables
still had every row (11/22/5/4). No restart needed/done — both pages re-read `plans/` from disk on
every render, and one right after yesterday's tracking-gap investigation risked creating another.
Also found the same `.gitignore` gap noted in the MaxActivePlans commit above — now closed.

**"I want to see a short context example of the fields... a description on top with the blank
fields... so I understand what to fill in and how it's going to look"**: `AddPlanDialog` gained a
live mad-libs preview — real prompt words, each `{token}` replaced by that field's current text
(bold/accent) or a muted italic `[Field label]` while empty, sourced from `PlanTemplates.cs`'s own
string so it can't drift from what's copied to claude.ai. **v1** was a prefix-cut of the template —
cluttered with filler, clipped past the dialog's real edge (`ContentDialog` caps width at the
`ContentDialogMaxWidth` theme resource regardless of content's `MinWidth` — same bug class hit 3
times before, `SplitDiaryEntryDialog.cs`). **v2**: `ExtractPreview` keeps only sentences naming a
blank, joined with " … "; dialog overrides `ContentDialogMaxWidth` (640, was silently capped ~520).

**v3 (same session): "add plan button stopped working"** — self-inflicted. `Modes`'s static
initializer calls `ExtractPreview`, which loops over `PreviewTokens`, declared *after* `Modes`; C#
runs static field initializers in textual order, so `Modes` built against a still-null array —
`TypeInitializationException` the instant anything touched `AddPlanDialog`. Fixed by reordering.
Live-UIA confirmed the crash/live-typing fix — **but its "unclipped" claim was wrong**:
`AutomationElement.Name`/`BoundingRectangle` report a TextBlock's own text/self-computed layout, not
whether an ancestor is visually clipping it, so that check couldn't have caught the width bug at all.

**v4: "still the text is going outside the boundaries"** — the real clipping bug, still unfixed.
SDK's template (`generic.xaml`, WindowsAppSDK.WinUI 1.8.260528001):
`Border[MaxWidth=ContentDialogMaxWidth] > ScrollViewer[HorizontalScrollBarVisibility=Disabled] >
Grid[Padding=24] > (content)` — inner Padding costs 48px before content sees any space, and the
ScrollViewer can't scroll to absorb overflow. v2 set `panel.MinWidth` to the *same* value as the
outer `ContentDialogMaxWidth` override (640 both) — forced the panel to demand 48px more than the
non-scrolling area had, the identical bug moved from ~520 to ~640. Fixed: new
`DialogContentWidth = DialogWidth - 64`, panel/preview `MaxWidth`s sized to that, not the outer cap.
**Verified with an actual screenshot this time** (`System.Drawing.CopyFromScreen` off the real
window rect, not UIA text properties) at both maximized (1920px) and resized to 900px: preview wraps
cleanly, no clipping either way. 152/152 tests, all four rounds.

- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **How the 08-14 archive move happened is unconfirmed** — see that entry above; watch for a recurrence.
  - **The 08:00–11:28 gap on 2026-08-13 never produced a diary row — cause unconfirmed** (ruled
    out as the test-data cleanup, see that entry). Revisit if it recurs.
  - **Not yet live-UIA-verified** (clean build + tests only): 08-13's narrowed diary columns/Auto
    scrollbar (widths 870/130/160 computed, not measured live); 08-13's Reports DayOffs figure;
    08-07's IdleReturnDialog Category/Tag fields + Mark-tag toolbar row (chip-click change and
    auto-classify-until-touched wiring unexercised live).
  - **The diary's midnight rollover has never been observed actually happening** — every other part
    of that fix was verified live, but the rollover itself needs the clock to cross midnight with the
    app sitting on Reports. If the diary still shows yesterday some morning, the assignment at the
    top of `BuildDiarySection` is the first place to look.
  - Scoring Settings' 12 inputs are live-checked (08-05, present/reachable at both window extremes)
    but their **values** have never been edited live — that writes `config.json` and restarts the
    tracker, so it stays code-inspection-only.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field (not "App Service URL").
  - **Settled, not action items**: 42 overlapping `time_diary` pairs 06-29→07-16 (only 2 match
    `HandleActiveSession`, rest unconfirmed, user's call 2026-07-18 — leave untouched);
    `ActivateQueuedPlan`'s non-atomic write-then-delete (2026-07-28); ~150-230MB memory footprint
    (2026-08-06, mostly `NavigationCacheMode="Enabled"` + WinUI3's baseline, no leak — leave as-is).
  - **Resolved-and-closed, one-line pointers** (prose in git log): `MentorOverseer`→`Planillium`
    rename 2026-07-23; diary-tracking-gap bug 2026-07-21 (`PollOnce` order); LinkedIn/Reddit
    auto-publishing for `posting-plan` dropped 2026-07-22 (dormant Reddit OAuth2 tool at
    `posting-plan/tools/reddit-publish/`); `PlanDayForDate` closed form 2026-07-18; TickTick secret
    rotated 2026-07-09, reconnected 2026-08-04; personal-data git-history scrub 2026-07-18; v1.1.0 +
    GitHub Release + repo flipped Public 2026-07-21; duplicate repo deleted 2026-07-21; tray icon
    vanishing — confirmed fine 2026-08-04; 2026-07-17 keyboard/dark-mode/timing item; **the 08-04
    tracker split — confirmed exercised live**, not just built: the log shows
    `HandleSleepGap`/`HandleIdleReturn`/`HandleActiveSession` all firing correctly through 08-07.

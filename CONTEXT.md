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

**Through 2026-07-22** (detail in git log): WinUI 3 rebuild landed 07-07 as v1.0.0. Audit rounds
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
fallback (~118 bare "-" rows/day fixed); `posting-plan`/`project-media` skills bootstrapped.

**2026-07-23 → 07-29** (condensed): internal rename `MentorOverseer`→`Planillium` (3 legacy-compat
values deliberately untouched, see top of file); Diary App/Page filter split; "Exclusion Impact"
panel removed (business rule 12); 26 `ContentDialog` sites unified onto `DialogControls.Build`;
`StartDayChangeWatcher` for the new-day-without-switching-pages gap; note-wipe risk fixed; VACUUM off
the UI thread; docx zip-bomb check counts real decompressed bytes. Four 5-category audits (0
Critical) plus two re-audits, 86/86 tests. **07-27**: app wouldn't start — bisected to 07-24's
`SetDefaultDllDirectories` breaking WinRT activation; clean revert. Wake-from-sleep toast only logged
a gap with no UI handler wired; `HandleIdleReturn` now always logs "unaccounted time" the instant a
gap is detected. **07-28**: Diary Edit/Split unreachable via `Card()`'s rounded-clip (Standing
lessons); plan tasks gained a `tools` list (`TeachPlanTools`, 12 keywords, zero mismatches); diary
AutoSuggestBox; "Show more" batched at 50. **07-29**: `SplitDiaryEntryDialog`'s "+ Add activity" never
wired; Diary filters didn't narrow each other; idle rows show the typed answer in Page not "—".

**2026-08-04 → 08-05** (one continuous session, many rounds — commits `e4c4f11`, `ee981c0`,
`488424f`, `0ffdde4`): diary window merged into working hours (`InDiaryHours`→`InWorkingHours`,
`SaveRules` rejects work start ≥ end); Reports' midnight rollover fixed (`_diaryFollowsToday`, every
date control routed through one `GoTo`); "Day X of Y" → `Plan.ProgressDay` (business rule 13, user
resolved the "hole further back" ambiguity themselves); shared `AddTotalsRow`; all 12 scoring rules
made editable (`ScoringRules` table, feeds both the formula and Settings). `ActivityTracker`'s
God-Object split landed (855→597 lines): `NativeInput`/`WindowTitleResolver`/`ActivityClassifier`/
`DiaryWriter` extracted, forwarders kept the public surface unchanged. Reports above the diary
follows the period selector throughout (`ReportData.PeriodStats`; scores recomputed rather than
read from `score_ledger` **deliberately** — the ledger only holds days the app ran to credit); card
relabelled "SCORE EARNED" to stay distinct from the sidebar's all-time BALANCE. Settings' Expander
layout was **rejected on sight** ("no bouncing") and replaced by a right-hand `ListView`, one panel
visible at a time, sized for the 900dip minimum — found and fixed blank accessible names plus
off-screen `BoundingRectangle.Empty` scoring boxes (`ScrollIntoView`) along the way. "Align all the
reports": `LabelColumnWidth`/`ColumnGap`/`SubRowIndent` unified. Report tables gained all 5
categories plus a per-row Total — **meaning changed**, Year total 80h10m→294h34m; new
`ReportData.CategoryMinutes`, and consolidating three near-duplicate queries into `DailyMinutes`
fixed two latent bugs (`MonthBuckets` never read `diary_daily_rollup`; the rollup's neutral/paid/idle
columns were never read back). Hours switched to decimal (`FmtHours`, diary kept h/m); Month/Year
gained Tasks/Score + totals via `DailyRows`. "Asks about absence twice": two compounding causes in
`ActivityTracker.PendingDayGap`, fixed with `_openSessionStart`/`_accountedUntil` clamps,
root-caused via git archaeology. New `Pages/PageLayout.cs`/`CenterContent()` fixed the same
pre-07-28 width bug on Today/Schedule/Plans. "Fill the gap with the following day's task" reverses
the 07-09 no-gap-closing call (business rule 7, re-confirmed with the user); `RescheduleTask` now
runs one combined compact-then-push formula, future-only guarded (`ReplanOverdueDialog` still needs
past days left alone). User then approved closing the existing days-22/23 gap retroactively: new
`ScoreService.CompactFutureGaps`, applied once via a temporary Debug-only button, DB backed up and
verified 21→39 sequential, button removed after. New independent `DiaryTag` axis added
(`time_diary.tag`, this app's first ADD COLUMN migration) — never read by ScoreService, purely
descriptive; five user-literal values (Routine/Documents/Studioshoo/Selfdev/Procrastination); hit
and fixed the same `ContentDialog` width-cap bug a third time. Verified throughout via live UIA on
scratch-root instances; ended the arc at 144/144.

**2026-08-07**: two user reports on Tag/idle-answer. (1) Tag was riding inside the diary row's
Details column as a suffix, easy to lose next to the duration — now its own fixed-width column
(`DiaryList`/`BuildRow`, column 3, "—" when unset). (2) `IdleReturnDialog` ("welcome back") had no
Category/Tag field anywhere — category was silently re-derived from typed text via
`ClassifyIdleText` with no way to see/correct the guess, tag didn't exist there at all. Added
explicit combos to both modes: single mode auto-fills Category from the classifier guess and
keeps re-guessing until the user picks one themselves (`catTouched` flag), then stops; split rows
are manual only, matching `SplitDiaryEntryDialog`'s own "a recorded block isn't auto-classified"
choice. Chip click used to submit instantly — now fills the description field instead, since
instant-submit left no room to see the new fields (one extra tap on the common path).
`DiaryWriter.LogSession` gained `tag`; `LogIdleAnswer`/`LogIdleAnswers` now take explicit category
(+optional tag) instead of deriving it internally. Split rows reused `SplitDiaryEntryDialog`'s
proven `ContentDialog`-width-cap fix pre-emptively rather than hitting the same bug a third time.
3 new tests, 147/147. **Not yet live-UIA-verified** (see Open TODOs).

**Then** ("check if the score balance is calculated correctly"): audited read-only against a DB copy
— sum arithmetic, single writer (`AddLedger`), `sl_reason_date` UNIQUE — no dupes/gaps/unknown
reasons. Found a real live bug: today's `daily_score` frozen at 0 (written 08:40am) despite 51 more
on-plan minutes tracked since — traced via the log + `winui_state.json`'s `last_review` (still 08-06,
ruling out an early review). Root cause: a diary edit recomputes that date's score via
`RecalculateDayScore` regardless of whether it's *today* (correct for a past day) — but
`ReviewDialog.PersistReview` used `CreditDayScoreIfMissing` (skip-if-present) for today's real
end-of-day credit, so the earlier incidental recompute pre-empted it forever. Fixed: `PersistReview`
now calls `RecalculateDayScore(today)`, already proven by an existing test
(`RecalculateDayScore_OverwritesAnAlreadyCreditedDay`) — dialog-layer call site changed, no new test
needed. **Real-data fix, user-confirmed**: deleted the one stale row (`score_ledger` id 140) after
explicit confirmation naming the table/row; DB backed up first. User then asked directly whether
08-06 (already past, so never auto-recredited) was undercounted the same way — it was: real formula
against that day's final data gives 31, ledger held 0. Computed via a throwaway, isolated test that
set its own `MENTOR_ROOT` against a scratch copy (real plans + DB copy), never the real code path,
to avoid hand-replicating the formula. Corrected row id 138 to delta=31 after the same explicit
naming-the-row confirmation; balance -4→27. Test file deleted after use.
Checked siblings while in there: `ReviewDialog`'s disabled "Close the day" button sits next to
`DefaultButton=Primary`, and WinUI's DefaultButton can still fire on Enter while disabled — real
WinUI behaviour, ruled out as *this* incident's cause but hardened anyway, and found in three more
dialogs (`SpendDialog`, `SplitDiaryEntryDialog`, `IdleReturnDialog` split mode) — each now switches
`DefaultButton` off Primary while disabled and re-validates before writing. 147/147, Release
rebuilt/relaunched.
**Then** ("Mark … buttons for tags"): second toolbar row under Mark on-plan/off-plan/neutral — one
"Mark <label>" button per `DiaryTag.Options` entry plus "Clear tag," built in a loop. New
`MarkSelectedDiaryRowsTag` doesn't share a helper with `MarkSelectedDiaryRows`: category changes
also touch activity-rule learning and a score recalc, neither applies to a tag. No dedicated test,
same as its sibling — page-level UI, outside this project's Service/Data test scope.

**2026-08-13** ("totals for day-offs … added by me manually"): new `ScoreService.ManuallyMarkedDaysOff`
(distinct calendar dates with a `plan_days_off` row, deliberately narrower than
`AllPlansScoringExempt`/`ScoringExemptDates` which also count a plan's recurring weekday rest days).
**First shipped, then corrected same session**: v1 added a standalone always-three-numbers
(week/month/year) card on Reports plus a summary line on Schedule — the user pushed back on both:
"everything besides the diary should update based on the chosen timescale... the same about the
day-offs statistics", "we do not need an additional card for it", "remove the day-offs info from
the schedule page". Reworked to match: `DayOffs` joined `ReportData.PeriodTotals` itself (bounded
period-start-through-today, same as every other figure there — deliberately not a smarter
future-aware boundary, for consistency with the rest of the card) and folded into the existing
Reports score card's caption line ("N days off marked"); the Schedule summary was removed outright,
nothing replaced it there. Tests renamed/moved to match: `ManuallyMarkedDaysOffTests.cs` keeps the
raw-method scope tests (manual-only, range boundary, cross-plan dedup, recurring-exclusion
exclusion), two new tests in `ReportPeriodStatsTests.cs` pin the period-selector and today-boundary
behavior. 152/152.

**Same session, real finding**: while verifying, `dotnet test -c Release` turned out to have been
silently running the whole suite against the REAL `data/progress.db`, not an isolated copy —
`TestRootFixture`'s `MENTOR_ROOT` override lived behind `#if DEBUG` on the assumption tests always
build Debug; a `-c Release` run recompiles the same source file (source-linked, not a project
reference — see `Planillium.App.Tests.csproj`) with `DEBUG` undefined, so the override silently
never existed and `AppPaths.Root` fell through to the real repo's own `config.json`/`plans/`.
Confirmed via matching `score_ledger` row counts between a "fresh" filtered test run and the real
DB. Left 112 orphaned rows under fake plan ids across `task_overrides` (78), `task_completions`
(14), `plan_days_off` (20) — deleted after backup (`data/backup/progress.db.20260813_180041.bak`)
and explicit confirmation naming the exact tables/counts; none were ever read by the real app.
`score_ledger` has no `plan_id` (keyed only by reason+date), so a test crediting/recalculating
"today" could in principle have clobbered a real day's ledger row too — **not retroactively
auditable**, a test-derived score and a real one are indistinguishable after the fact; nothing
further done there beyond the two rows (138, 140) already corrected on 08-07 for unrelated reasons.
Fixed properly: the override now also requires `PLANILLIUM_TESTS`, a constant the test project
defines unconditionally in **both** configurations (`Planillium.App.Tests.csproj`'s
`DefineConstants`), so isolation no longer depends on which configuration `dotnet test` happens to
build. Verified: 151/151 passing in both `-c Release` and default Debug; real DB's `score_ledger`
row count unchanged (78) after a Release run post-fix. **Lesson for any source-linked test
project**: a `#if DEBUG`-gated test-only hook is only as safe as "tests always build Debug" — untrue
the moment anyone runs `-c Release`, and the failure mode is silent, not a build error.

**Then**: a screenshot ("theres a mess there") surfaced the *rest* of the `time_diary` pollution —
the test-isolation bug above hadn't just hit the 3 plan-scoped tables, it wrote 42 fake diary rows
too (literal `TestWindow`/`test-window-<guid>` windows, `idle-split-`/`idle-single-<guid>`
descriptions, one row with an invalid category `some_future_category`), on the two real dates a
Release test run happened: 2026-08-07 and 2026-08-13. Backed up
(`data/backup/progress.db.20260813_190410.bak`) and deleted after confirmation naming the table and
count; the one genuine row sharing 08-13 with them (a real detected "unaccounted time" gap,
14:52–18:00) was identified and left alone. Same request also asked to remove the diary's
horizontal scrollbar by narrowing the description columns — root cause turned out to be
**`DiaryListWidth` itself was stale**: set for the App/Page column split (07-23), never revisited
when the Tag column was added (08-07), so the row's true natural width had quietly outgrown the
page's own max content width, forcing the scrollbar to engage on *any* window, not just a narrow
one. Recomputed `DiaryListWidth` (950→870), narrowed Page (210→130) and Details (260→160) — both
already ellipsis-trim with a tooltip, narrowing just makes that the common case — and switched the
scroller from permanently-`Visible` back to `Auto` now that content actually fits.

- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **08-13's narrowed diary columns / Auto scrollbar have not been live-UIA-verified** — clean
    build only; the exact new widths (870/130/160) were computed from the pre-Tag-column math, not
    measured live.
  - **08-13's Reports score-card DayOffs figure has not been live-UIA-verified** — clean build +
    152/152 tests only.
  - **08-07's IdleReturnDialog Category/Tag fields, and the Mark-tag toolbar row, have not been
    live-UIA-verified** — clean build + 147/147 tests only. The width-cap fix reapplies an
    already-proven pattern, but the chip-click change and auto-classify-until-touched wiring
    haven't been exercised live.
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
    `HandleActiveSession`; rest unconfirmed; user's call 2026-07-18 — leave untouched);
    `ActivateQueuedPlan`'s non-atomic write-then-delete (2026-07-28); ~150-230MB memory footprint
    (2026-08-06, mostly `NavigationCacheMode="Enabled"` + WinUI3's own baseline, no leak found;
    lighter frameworks exist but a multi-week rewrite for an unsized win — **leave as-is**).
  - **Resolved-and-closed, one-line pointers** (prose in git log): `MentorOverseer`→`Planillium` rename
    2026-07-23; diary-tracking-gap bug 2026-07-21 (`PollOnce` order); LinkedIn/Reddit auto-publishing
    for `posting-plan` dropped 2026-07-22 (APIs gated/unsuitable; dormant Reddit OAuth2 tool at
    `posting-plan/tools/reddit-publish/`); `PlanDayForDate` closed form 2026-07-18; TickTick secret
    rotated 2026-07-09, reconnected 2026-08-04; personal-data git-history scrub 2026-07-18; v1.1.0 +
    GitHub Release + repo flipped Public 2026-07-21; duplicate `devbaghda/planillium` repo deleted
    2026-07-21; tray icon vanishing — confirmed fine 2026-08-04; 2026-07-17 keyboard/dark-mode/timing
    item; **the 08-04 tracker split — confirmed exercised live**, not just built: the log shows
    `HandleSleepGap`/`HandleIdleReturn`/`HandleActiveSession` all firing correctly through 08-07.

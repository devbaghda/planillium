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
> **Nothing was rewritten in the move.** Compacted again 2026-09-01 (423→381) by tightening the
> Session log's prose — every date, fact, root cause and open item preserved, only wording cut.

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
aggressively; compacted ~16 times since 2026-07-06 (852→224 first; 2026-08-04 split
`DECISIONS.md` out and promoted Standing lessons into their own section)._

### Standing lessons → `DECISIONS.md`

**Moved out 2026-08-04.** Verification discipline, safety around real data (including the
scratch-`MENTOR_ROOT` technique for exercising the UI without touching live data), the WinUI
layout traps (rounded-`Border` clipping, `Expander` content stretch, `DispatcherQueueTimer`),
prompt/timer/state rules, and the design lessons. Every entry there cost a real bug to learn and
none may be dropped in a compaction.

### Session log

**Through 2026-07-29** (full detail in git log): WinUI 3 rebuild landed 07-07 as v1.0.0. Audit
rounds 1-6 (07-09→07-15): `Database.RunInTransaction`, `JsonFileIO` atomic writes,
`PlanStore.IsValidPlanId`, transactional dialogs — the mechanisms every later round built on —
plus completed-task-shift data-loss fix (rule 7), move-to-today backward compaction. 07-16:
day-off/reschedule overlap fix. 07-17: `TreatWarningsAsErrors`, "Clear all my data", day-off
scoring (rule 10). 07-18→07-22 (tests 19→83): `asOf`-aware streaks; closed-form `PlanDayForDate`;
42 overlapping `time_diary` pairs found, only 2 matching the known bug — **user's call: leave
untouched**; personal-data git-history purge (134 commits); repo renamed `planillium`, **v1.1.0
public**; `DispatcherQueueTimer` root-caused (Standing lessons); queued plan ideas (v1.2.0);
`ActiveWindowTitle` process-name fallback. 07-23: internal rename `MentorOverseer`→`Planillium`
(3 legacy-compat exceptions, see top of file); "Exclusion Impact" panel removed (rule 12); 26
`ContentDialog` sites unified onto `DialogControls.Build`; four 5-category audits (0 Critical),
86/86 tests. 07-27: app wouldn't start — bisected to `SetDefaultDllDirectories` breaking WinRT
activation, reverted; `HandleIdleReturn` now always logs "unaccounted time" on detection. 07-28:
Diary Edit/Split unreachable via rounded-clip (Standing lessons); plan tasks gained `tools`.
07-29: `SplitDiaryEntryDialog`'s "+ Add activity" never wired; Diary filters didn't narrow each
other.

**08-04→08-05** (commits `e4c4f11`/`ee981c0`/`488424f`/`0ffdde4`): diary window merged into
working hours (`SaveRules` rejects start≥end); midnight rollover fixed; "Day X of Y"→
`Plan.ProgressDay` (rule 13); all 12 scoring rules made editable (`ScoringRules` table).
`ActivityTracker` God-Object split: `NativeInput`/`WindowTitleResolver`/`ActivityClassifier`/
`DiaryWriter` extracted, public surface unchanged. Reports follow the period selector (scores
recomputed, not read from `score_ledger` — deliberate, the ledger only holds days the app ran).
Settings' Expander rejected on sight ("no bouncing") → `ListView`. Report tables gained all 5
categories + per-row Total (**meaning changed**, Year total 80h10m→294h34m); 3 near-duplicate
queries consolidated, fixing 2 latent bugs. Hours→decimal. "Asks about absence twice": two
compounding causes in `ActivityTracker.PendingDayGap`, fixed via `_openSessionStart`/
`_accountedUntil` clamps (related but distinct from 08-17's idle-return duplicate below).
"Fill gap with next day's task" reverses the 07-09 no-gap-closing call (rule 7);
`RescheduleTask` now one compact-then-push formula, future-only; user approved closing the
existing days-22/23 gap retroactively (DB backed up). New `DiaryTag` axis (`time_diary.tag`),
descriptive only. Verified live UIA; 144/144.

**08-07**: (1) Tag moved from a Details-column suffix to its own fixed-width column
(`DiaryList`/`BuildRow`, "—" when unset). (2) `IdleReturnDialog` gained explicit Category/Tag:
single mode auto-fills from `ClassifyIdleText`'s guess and keeps re-guessing until touched
(`catTouched`), then stops; split rows manual only; chip click fills the description field
instead of submitting instantly. `DiaryWriter.LogSession` gained `tag`; `LogIdleAnswer(s)` take
explicit category(+tag). 3 new tests, 147/147, not yet live-UIA-verified (Open TODOs).
**Then** ("check the score balance"): real bug — a diary edit recomputed today's score
regardless of whether it *was* today, and `ReviewDialog.PersistReview`'s skip-if-present logic
let that pre-empt the real end-of-day credit forever, freezing `daily_score` at 0. Fixed:
`PersistReview` now calls `RecalculateDayScore(today)`. **Real-data fix, user-confirmed**:
`score_ledger` rows 140/138 corrected (backed up first), balance -4→27. Sibling check: disabled
dialog buttons could still fire via Enter — hardened across `ReviewDialog`/`SpendDialog`/
`SplitDiaryEntryDialog`/`IdleReturnDialog`. 147/147. **Then** (tag toolbar): second row under
Mark on-plan/off-plan/neutral, one button per `DiaryTag.Options` + "Clear tag" — a separate
helper from `MarkSelectedDiaryRows` since category changes also touch rule-learning/score recalc
and tag doesn't. No dedicated test (page-level UI).

**08-13**: `ScoreService.ManuallyMarkedDaysOff` shipped standalone, reworked same session into
`ReportData.PeriodTotals.DayOffs` folded into the score card's caption; Schedule's separate line
removed. Tests added, 152/152. **Real finding**: `dotnet test -c Release` was silently hitting
the REAL `data/progress.db` (lesson → DECISIONS.md) — 112 orphaned + 42 fake `time_diary` rows
(one genuine gap kept) deleted after backup+confirmation; fixed via `PLANILLIUM_TESTS` defined
unconditionally in both configs, 151/151. Same screenshot: diary's horizontal scrollbar was
`DiaryListWidth` staleness (never updated for 08-07's Tag column) — recomputed, columns
narrowed, scroller Visible→Auto. Surfaced a genuine 08:00→11:28 gap with no placeholder row
despite `HandleIdleReturn`'s guarantee — **cause never found, still open**. `MaxActivePlans` 2→3,
docs updated.

**08-14** ("the plan that finished should not have moved, no way to restore it"): archived plan
`claude-code-10-level-mastery.json` had only 11/23 tasks done — short of the 100% Archive
requires, ruling that flow out; **how it moved stays unconfirmed**. File moved back to
`plans/active/`, verified intact; DB rows (keyed by plan_id, untouched either way) all present.
Closed the same `.gitignore` gap found in the MaxActivePlans work above.
**Same day** ("short context example of the fields"): `AddPlanDialog` gained a live mad-libs
preview (`{token}`→typed text, from `PlanTemplates.cs`). 4 iterations to stop clipping
(`ContentDialog` caps width at the `ContentDialogMaxWidth` theme resource regardless of content
`MinWidth`; only a real screenshot proves ancestor clipping) — landed on
**`DialogContentWidth = DialogWidth - 64`**, the pattern every dialog since has followed
(08-17/08-18/08-28 below). Verified via screenshot at 1920px and 900px. 152/152 tests.

**08-17** (three reports): (1) Schedule's per-plan day lists gained the project's standard
click-to-expand pattern (chevron `FontIcon` E70D/E70E, Tapped+Enter/Space, `AutomationProperties`)
mirrored from `ReportsPage.TimeByApp.cs` — collapsed state in `SchedulePage._collapsedPlans`
(`HashSet<Plan.Id>`) since `Render()` rebuilds every plan's UI from scratch each save/toggle/
rollover. (2) `SplitDiaryEntryDialog` fields ran outside the popup margin, no suggestion chips:
fixed via the `DialogWidth-64` pattern (`DialogWidth = 780`) plus a "Quick pick" chip row per
`AutoSuggestBox`, tracked via `activeDescBox`/`GotFocus` (fills whichever row is focused,
defaulting to first empty). Same fix applied to `EditDiaryEntryDialog` (identical gap, simpler).
(3) "Day starts at 8 though Settings say 6" — `SettingsPage.SaveRules` validated every
scoring/reminder field before writing *anything*, so one invalid box elsewhere silently discarded
an already-valid working-hours edit with no error naming the cause (new standing lesson →
DECISIONS.md). Split into two independent save phases; also NaN-checked 5 reminder/idle/retention
`NumberBox`es before casting to `int` (`(int)NaN`→`int.MinValue`, silent garbage-config risk).
Debug 0 errors, 152/152 tests, not yet live-UIA-verified (no live interaction that session).
**Same day** ("double asking for absence time" — two `time_diary` rows, ids 6215/6216, both
13:47–13:59/12min): idle-return has two entry points for one event (native toast + tray "Log
it"), both converging on `HandleNotificationActivation`'s `ToastArgs.IdleReturn` with nothing
stopping both firing for the same gap; first answer's `ClearIdlePlaceholder` works, second finds
none left and inserts an unconditional duplicate. **Fixed**: new `DiaryWriter.HasIdlePlaceholder`
(same match criteria as `ClearIdlePlaceholder`) checked at that one convergence point —
deliberately not inside `IdleReturnDialog.ShowAsync` itself, since `ReviewDialog
.ReconcilePendingGap` also calls it for a gap never placeholder-logged, which a blanket guard
would break. Fails open on a DB read error. **Row 6216 deleted** (user-confirmed, named
table+row); row 6215 untouched. Debug 0 errors/warnings.

**08-18** (three reports, same dialog's split mode): (1) `IdleReturnDialog` was the one dialog
never given the `DialogWidth-64` fix — applied (`DialogWidth = 780`). (2) split-mode's
description field was a plain `TextBox` with no suggestion wiring — fixed by the same pattern:
`AutoSuggestBox` + `DialogControls.WireFrequentSuggestions` + "Quick pick" chips via
`activeDescBox`/`GotFocus`. (3) "frequent answers seem hardcoded" — not a bug:
`MostFrequentIdleAnswers()` reflects real usage (sleep 39×, dog walk 37×, lunch 15×); what reads
static is the 4 `FixedChips` always occupying the first row regardless of frequency, and split
mode showing nothing dynamic at all (bug #2) — fixing #2 resolves the perception; fixed-first
ordering left as a separate design choice. Debug 0 errors/warnings.
**Same day** ("remove the horizontal rolling in Split diary entry's popup"): `ScrollViewer`
fallback still engaging on a normal window; `removeBtn` never overrode platform default
`Button.MinWidth`. Fixed: `removeBtn.MinWidth = 0`, `DialogWidth` 780→860, `ScrollViewer` removed
entirely. Verified live 08-19.

**08-19**: split-of-absence popup still looked broken — root cause was the running exe: 08-18's
fixes were only ever built Debug, the live Release exe (PID 22100) untouched since before. Killed
it, rebuilt Release (0 errors), relaunched (PID 22768). **User-confirmed live**: dialog width and
one-click chips correct. Closes "Live Release build is stale" for these two fixes.

**08-28** ("remove horizontal scrolling in Diary, tighten columns"): root cause — the diary row's
Time column widens 150 vs 110px whenever `showDate` is true (search active or "All time"), but
08-13's `DiaryListWidth` recompute only ever checked `showDate=false`, so any search/filter view
overflowed by that 40px — why it looked intermittent. Fixed by narrowing Category/Tag/App/Page/
Details another 80px total (ellipsis+tooltip on overflow unchanged) so the row fits its 870px
budget at the wider Time width; `DiaryListWidth` itself unchanged. **Verified live**: killed
running Release instance (well before 20:00 EOD, user-approved), rebuilt, relaunched, searched
"idle" (233 real rows) at full/maximized window — no scrollbar, Edit/Split fully visible. **Known
unchanged tradeoff**: at the app's minimum 900px window, the diary row still needs horizontal
scroll (needs ~926-966px, pre-existing, not part of this bug) — flagged to user, left pending a
request.

**09-01** ("finished 2 tasks yesterday, not counted" — third active plan
`ai-microsolutions-brand-30`): real bug, confirmed via `score_ledger` — 08-31 had every usual
entry except `daily_score`, which was simply missing; the completions themselves saved fine.
Root cause: `daily_score` is only written by the evening review (if it completes) or
`RunStartupCatchUp`'s `EnsureScoreCaughtUp`, which fires once at process launch only. App hadn't
relaunched since (log showed `HandleSleepGap`, not a restart), and 08-31's evening review looks
interrupted (`DialogGate` log shows ~9 min of contention around the second completion), so
neither path ran. Fixed: `MainWindow.Startup.cs`'s `CheckDayChange` (the once-a-minute midnight
watcher) now also runs `CatchUpScores` off-thread before its UI refresh, so a day closing while
the app stays open no longer needs a restart to get scored. Debug 0 errors, 152/152 tests.
**Live-verified**: stopped Release (10:11, clear of EOD window), rebuilt, relaunched — 08-31's
`daily_score` backfilled to +19 within seconds, confirmed via direct `score_ledger` read.
(Separate, not fixed: same log showed a caught `COMException` from `FlashContentRefresh` —
"multiple animations...same property" — on the first `CheckDayChange` tick after sleep/wake,
harmless so far, after `RefreshScore` and inside a catch — see Open TODOs.)

- **Open TODOs** (not yet done):
  - **`FlashContentRefresh` COMException on wake** (09-01 entry): caught, harmless so far, cause
    not investigated — possibly two queued ticks firing close together after timer suspension
    during sleep. Revisit if a visible glitch or less-harmless failure accompanies it.
  - **How the 08-14 archive move happened is unconfirmed** — watch for a recurrence.
  - **The 08:00–11:28 gap on 08-13 never produced a diary row — cause unconfirmed** (ruled out as
    the test-data cleanup). Revisit if it recurs.
  - **Not yet live-UIA-verified** (clean build + tests only): 08-13's narrowed diary
    columns/Auto scrollbar and Reports DayOffs figure; 08-07's IdleReturnDialog Category/Tag
    fields + Mark-tag toolbar row; 08-17's Schedule collapsible cards + EditDiaryEntryDialog
    quick-pick chips.
  - **Diary midnight rollover never observed actually happening** — needs the clock to cross
    midnight with the app sitting on Reports. If diary still shows yesterday some morning, check
    the assignment at the top of `BuildDiarySection` first.
  - Scoring Settings' 12 inputs are live-checked (present/reachable at both window extremes) but
    their **values** never edited live — that writes `config.json` and restarts the tracker, so
    it stays code-inspection-only.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field (not "App Service URL").
  - **Settled, not action items**: 42 overlapping `time_diary` pairs 06-29→07-16 (only 2 match
    `HandleActiveSession`, rest unconfirmed, user's call 07-18 — leave untouched);
    `ActivateQueuedPlan`'s non-atomic write-then-delete; ~150-230MB memory footprint (mostly
    `NavigationCacheMode="Enabled"` + WinUI3 baseline, no leak — leave as-is).
  - **Resolved-and-closed, one-line pointers** (prose in git log): `MentorOverseer`→`Planillium`
    rename 07-23; diary-tracking-gap bug 07-21 (`PollOnce` order); Reddit auto-publishing for
    `posting-plan` dropped 07-22 (dormant tool at `posting-plan/tools/reddit-publish/`);
    `PlanDayForDate` closed form 07-18; TickTick secret rotated 07-09, reconnected 08-04;
    personal-data git-history scrub 07-18; v1.1.0 + GitHub Release + repo flipped Public 07-21;
    duplicate repo deleted 07-21; tray icon vanishing — confirmed fine 08-04; **the 08-04 tracker
    split — confirmed exercised live**: log shows `HandleSleepGap`/`HandleIdleReturn`/
    `HandleActiveSession` all firing correctly through 08-07.

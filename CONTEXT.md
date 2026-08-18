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
86/86 tests. **07-27**: app wouldn't start — bisected to `SetDefaultDllDirectories` breaking
WinRT activation, reverted; `HandleIdleReturn` now always logs "unaccounted time" on detection.
**07-28**: Diary Edit/Split unreachable via rounded-clip (Standing lessons); plan tasks gained
`tools`. **07-29**: `SplitDiaryEntryDialog`'s "+ Add activity" never wired; Diary filters didn't
narrow each other.

**2026-08-04 → 08-05** (one continuous session, commits `e4c4f11`, `ee981c0`, `488424f`,
`0ffdde4`): diary window merged into working hours (`SaveRules` rejects work start ≥ end);
midnight rollover fixed; "Day X of Y"→`Plan.ProgressDay` (rule 13); all 12 scoring rules made
editable (`ScoringRules` table). `ActivityTracker`'s God-Object split landed:
`NativeInput`/`WindowTitleResolver`/`ActivityClassifier`/`DiaryWriter` extracted, public surface
unchanged. Reports above the diary follow the period selector (scores recomputed, not read from
`score_ledger` **deliberately** — the ledger only holds days the app ran to credit). Settings'
Expander **rejected on sight** ("no bouncing"), replaced by a `ListView`. Report tables gained
all 5 categories + a per-row Total — **meaning changed**, Year total 80h10m→294h34m; 3
near-duplicate queries consolidated, fixing 2 latent bugs. Hours switched to decimal. "Asks
about absence twice": two compounding causes in `ActivityTracker.PendingDayGap`, fixed via
`_openSessionStart`/`_accountedUntil` clamps (see 2026-08-17's idle-return duplicate below — a
related but distinct bug in the same area). "Fill the gap with the following day's task"
reverses the 07-09 no-gap-closing call (rule 7); `RescheduleTask` now one compact-then-push
formula, future-only; user approved closing the existing days-22/23 gap retroactively (DB backed
up). New `DiaryTag` axis (`time_diary.tag`), descriptive only. Verified live UIA; 144/144.

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

**2026-08-13**: `ScoreService.ManuallyMarkedDaysOff` (distinct `plan_days_off` dates, narrower than
`AllPlansScoringExempt`) shipped as a standalone card, then reworked same session into
`ReportData.PeriodTotals.DayOffs` (bounded like every other period figure) folded into the score
card's caption; Schedule's separate line removed. Tests added, 152/152. **Real finding while
verifying**: `dotnet test -c Release` was silently hitting the REAL `data/progress.db` (lesson →
DECISIONS.md Verification discipline) — 112 orphaned rows + 42 fake `time_diary` rows (one genuine
gap row kept) deleted after backup+confirmation; fixed via a `PLANILLIUM_TESTS` constant defined
unconditionally in both configs, 151/151. Same screenshot: diary's horizontal scrollbar was only
there because `DiaryListWidth` was stale (never updated for 08-07's Tag column) — recomputed,
columns narrowed, scroller `Visible`→`Auto`. Follow-up worry ("deleted real data from today") ruled
out — the pre-delete backup already had nothing there; but surfaced a genuine 08:00→11:28 gap with
no placeholder row despite `HandleIdleReturn`'s "always logs one" guarantee — **cause never found,
still open** (see Open TODOs). `AppInfo.MaxActivePlans` 2→3, docs updated to match.

**2026-08-14** ("the plan that finished should not have moved... no way to restore it"): an
archived plan (`claude-code-10-level-mastery.json`) had only 11/23 tasks ever completed — short of
the 100% the app's own Archive button requires, ruling that flow out; **how it moved stays
unconfirmed**. File verified intact and moved back to `plans/active/`; DB rows (keyed by plan_id,
untouched by Archive/Restore either way) confirmed all still present. Closed the same `.gitignore`
gap found in the MaxActivePlans work above.

**"I want to see a short context example of the fields... so I understand what to fill in and how
it's going to look"**: `AddPlanDialog` gained a live mad-libs preview (`{token}` → typed text, or a
muted `[Field label]` while empty, sourced from `PlanTemplates.cs` so it can't drift from what's
copied to claude.ai). Four iterations: **v1** clipped past the dialog's real edge (`ContentDialog`
caps width at the `ContentDialogMaxWidth` theme resource regardless of content's `MinWidth` — same
bug class hit repeatedly since, see `SplitDiaryEntryDialog.cs`). **v2**'s `ExtractPreview` narrowed
the text and overrode `ContentDialogMaxWidth` (640), but sized the panel's `MinWidth` to that same
*outer* cap instead of the padding-aware inner width, moving the identical clip from ~520 to ~640.
**v3** was a self-inflicted crash (`Modes`'s static initializer read `PreviewTokens` before its
declaration — C# runs static fields in textual order) fixed by reordering; it also showed that
`AutomationElement` text/bounds checks can't detect an ancestor clipping content, so v2's "verified
unclipped" claim had never actually been checked. **v4**: introduced `DialogContentWidth =
DialogWidth - 64` (the pattern every dialog since has followed), verified with an actual screenshot
(`CopyFromScreen`, not UIA) at 1920px and 900px — no clipping either way. 152/152 tests.

**2026-08-17** (three user reports): (1) Schedule's per-plan day lists gained the project's
standard manual click-to-expand pattern (chevron `FontIcon`, `E70D`/`E70E`, Tapped+Enter/Space,
`AutomationProperties`) mirrored from `ReportsPage.TimeByApp.cs` — collapsed state kept in a
`SchedulePage` instance field (`_collapsedPlans`, a `HashSet<Plan.Id>`) since `Render()` rebuilds
every plan's UI from scratch on every save/toggle/day-rollover. (2) `SplitDiaryEntryDialog`'s
fields ran outside the popup's margin and had no one-tap suggestion chips (only the dropdown):
fixed via the project's own `ContentDialogMaxWidth`/`DialogWidth-64` pattern (business rule, see
"v4" above) — `DialogWidth = 780`, `DialogContentWidth = DialogWidth - 64` — plus a "Quick pick"
chip row per `AutoSuggestBox`, tracked via a `activeDescBox`/`GotFocus` so a chip fills whichever
row is focused, defaulting to the first empty row. Sibling check: `EditDiaryEntryDialog` had the
identical documented gap (single field, simpler) — same chip fix applied there too. (3) "Day
starts at 8 though Settings say 6" — `SettingsPage.SaveRules` validated every scoring/reminder
field before writing *anything*, so one invalid box elsewhere on the page silently discarded an
already-valid working-hours edit with no error naming the cause (new standing lesson →
DECISIONS.md). Split into two independent save phases, each validating and writing on its own;
also closed a sibling gap where 5 reminder/idle/retention `NumberBox`es were never NaN-checked
before being cast to `int` (`(int)NaN` → `int.MinValue`, a silent garbage-config risk). Debug
build 0 errors, 152/152 tests. **Not yet live-UIA-verified** — no live app interaction this
session (see Open TODOs).

**Then, same day** ("double asking for absence time logging" — screenshot showed two
`time_diary` rows, ids 6215/6216, both 13:47–13:59/12min: real answer plus a leftover
`idle`/"unaccounted time" row). Root-caused via log + read-only DB query: the idle-return prompt
has two independent entry points for one event — the native Windows toast and the tray "While
you were away" recap's "Log it" — both converging on `MainWindow.HandleNotificationActivation`'s
`ToastArgs.IdleReturn` case with nothing stopping both firing for the same gap. The first
answer's `ClearIdlePlaceholder` replaces the placeholder correctly; the second finds none left
and `LogSession` inserts an unconditional duplicate anyway. **Fixed**: new
`DiaryWriter.HasIdlePlaceholder` (same overlap-match criteria as `ClearIdlePlaceholder`, so
"still exists" and "would be deleted" never disagree), checked in that one convergence point
before opening `IdleReturnDialog` a second time — deliberately *not* inside
`IdleReturnDialog.ShowAsync` itself, since `ReviewDialog.ReconcilePendingGap` also calls it
directly for a gap that was never placeholder-logged in the first place, which a blanket guard
there would have silently broken. Fails open (shows the dialog) on a DB read error, matching the
existing "always ask, never silently drop" philosophy. **Row 6216 deleted** (user-confirmed,
named table+row); row 6215 (the real answer) untouched. Debug build 0 errors/warnings.

**2026-08-18** (new screenshot of the same dialog's split mode, three reports): (1) "field is
out of boundaries" — `IdleReturnDialog` was the one dialog of this shape never given the v4
`ContentDialogMaxWidth`/`DialogWidth-64` fix (see 07-17's "v4" entry) that `SplitDiaryEntryDialog`/
`AddPlanDialog` already needed for the identical clipping bug; applied here too (`DialogWidth =
780`). (2) "no frequent answers one-click option" — confirmed gap: split-mode's description field
was a plain `TextBox` with zero suggestion wiring, unlike single mode's chips or
`SplitDiaryEntryDialog`'s own quick-pick chips + `AutoSuggestBox`. Fixed by copying that same
pattern: descBox → `AutoSuggestBox` + `DialogControls.WireFrequentSuggestions`, plus a "Quick
pick" chip row (reusing single mode's fixed+frequent `chips` list) tracked via
`activeDescBox`/`GotFocus`. (3) "frequent answers seem hardcoded" — checked against the live DB,
not a bug: `MostFrequentIdleAnswers()` genuinely reflects real usage (`sleep` 39×, `dog walk`
37×, `lunch` 15×, ...). What actually reads as static: the 4 `FixedChips` (Lunch/Break/Errand/
Work off-screen) always occupy the first, most visible row regardless of real frequency, and
split mode showed literally nothing dynamic at all (bug #2) — fixing #2 should resolve the
perception on its own; left the fixed-first ordering alone as a separate design choice. Debug
build 0 errors/warnings.

**Then, same day** ("remove the horizontal rolling [in Split diary entry's popup], if necessary
make that pop-up window wider"): 08-17's `DialogWidth = 780` fix let the row fit in principle, but
`SplitDiaryEntryDialog` still wrapped its row list in a horizontally-scrolling `ScrollViewer` kept
as a narrow-window fallback — in practice a normal window still showed a scrollbar and a
partly-offscreen row. Also found while sizing this properly: `removeBtn` (the "✕" button) never
overrode the platform's default `Button` `MinWidth`, so the row was wider than the ~660px assumed.
Fixed: `removeBtn.MinWidth = 0` (sizes to its own content instead of the platform default),
`DialogWidth` 780→860 for headroom, `ScrollViewer` removed entirely — `rowsPanel` is now a direct
child of `root`, nothing left to scroll. Debug build 0 errors/warnings, not yet live-verified.

- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **Live Release build is stale** — 08-17's dedupe fix + row-6216 deletion and 08-18's
    IdleReturnDialog width/chips fix and SplitDiaryEntryDialog scroll removal are Debug-only so
    far; rebuild Release + relaunch, then live-verify all three.
  - **How the 08-14 archive move happened is unconfirmed** — see that entry above; watch for a recurrence.
  - **The 08:00–11:28 gap on 2026-08-13 never produced a diary row — cause unconfirmed** (ruled
    out as the test-data cleanup, see that entry). Revisit if it recurs.
  - **Not yet live-UIA-verified** (clean build + tests only): 08-13's narrowed diary
    columns/Auto scrollbar and Reports DayOffs figure; 08-07's IdleReturnDialog Category/Tag
    fields + Mark-tag toolbar row; 08-17's Schedule collapsible cards + SplitDiaryEntryDialog/
    EditDiaryEntryDialog quick-pick chips; 08-18's IdleReturnDialog width fix + split-mode chips
    + SplitDiaryEntryDialog's scroll removal/width bump.
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
    `ActivateQueuedPlan`'s non-atomic write-then-delete; ~150-230MB memory footprint (mostly
    `NavigationCacheMode="Enabled"` + WinUI3's baseline, no leak — leave as-is).
  - **Resolved-and-closed, one-line pointers** (prose in git log): `MentorOverseer`→`Planillium`
    rename 07-23; diary-tracking-gap bug 07-21 (`PollOnce` order); Reddit auto-publishing for
    `posting-plan` dropped 07-22 (dormant tool at `posting-plan/tools/reddit-publish/`);
    `PlanDayForDate` closed form 07-18; TickTick secret rotated 07-09, reconnected 08-04;
    personal-data git-history scrub 07-18; v1.1.0 + GitHub Release + repo flipped Public 07-21;
    duplicate repo deleted 07-21; tray icon vanishing — confirmed fine 08-04; **the 08-04 tracker
    split — confirmed exercised live**: log shows `HandleSleepGap`/`HandleIdleReturn`/
    `HandleActiveSession` all firing correctly through 08-07.

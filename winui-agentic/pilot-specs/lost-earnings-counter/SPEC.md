# SPEC: Lost/gained-earnings counter

Target: `winui-agentic/Planillium.App/` (independent duplicate — do not touch `winui/Planillium.App/`).
Source: `REQUEST.md` in this folder + `DECISIONS.md` rule 14 in the repo root (explicitly sanctioned
as shared ground for both pilot sides — see PILOT.md's contamination rule and this SPEC's own
"Contamination note" at the bottom).

## Requirement

The user wants a running EUR figure that tells him, at a glance, the financial cost (or, once he's
employed again, the financial gain) of his current employment status, without having to do the
arithmetic himself or remember to open the app to keep it current. Every calendar day since
2025-12-04 — the day his income actually stopped — either deducts or adds a daily share of a
configured "potential net monthly income" figure, deducting while an employment-status toggle is
OFF and adding once it's switched ON, and the running total must be visible both as a single
always-current number in the sidebar and as a day/week/month/year breakdown on the Reports page,
each labelled with the exact period it describes and worded to match whether that period is a loss
or a gain. It must stay correct even if the app or PC was off for days or weeks, because the
catch-up has to happen automatically the next time the app runs, not depend on the app having been
open continuously.

## Chosen approach

Reuse the existing `score_ledger`/`ScoreService` ledger-plus-catch-up shape wholesale — a new,
narrower `income_ledger` table with one row per already-closed calendar day, populated by an
`EnsureIncomeCaughtUp()` pass wired into the same two places `EnsureScoreCaughtUp()` already runs
from (`RunStartupCatchUp` at launch, `CheckDayChange`'s once-a-minute watcher for the app sitting
open across midnight). This is the only piece of machinery in the app that already solves "keep a
running total correct even after the app was closed for days," so it's the natural fit rather than
inventing a second mechanism.

Key modeling decision: **the ledger stores one already-computed EUR amount (`delta`) and the
`employed` flag *per row*, fixed at the moment that row is posted, never revisited afterward.**
This is what makes the non-retroactive sign-flip rule (decision 3) fall out of the architecture
instead of needing a special case — a row is only ever written for a day that has already fully
ended, so "the status as of end of that day" is simply "whatever the toggle read the first time a
catch-up pass reached that day," and a later toggle flip can never touch an existing row because
nothing ever recomputes or overwrites one. The same reasoning extends to the monthly-income figure
itself (not just the toggle): if the user edits "potential net monthly income" after some days are
already posted, only future postings use the new figure — this isn't explicitly spelled out in
`DECISIONS.md` rule 14 (which only names the toggle), but it's the only behavior consistent with an
append-only ledger and with decision 3's own stated principle, so it's adopted here rather than left
ambiguous. Stated explicitly for QA: **a day's `delta` is permanent once written; changing Settings
afterward never rewrites a historical row.**

**Rejected alternative — recompute the whole total live from config on every read (no ledger at
all).** Simpler at first glance, but it cannot satisfy the non-retroactive sign-flip rule: a live
recomputation has no memory of what the toggle was on any past day, so flipping it would silently
relabel every historical day too. Rejected for the same reason the score system doesn't do this
either.

**Rejected alternative — bound the catch-up to a short lookback window, like `ScoreService`'s
7-day `comeback_lookback_days`.** `EnsureScoreCaughtUp` deliberately only looks back a handful of
days because score catch-up is forgiving of a rare multi-week gap (worst case, a stretch of days
never gets scored). Income can't use the same bound: decision 2 requires a full backfill from
2025-12-04 on the very first run, which on 2026-09-22 is already nearly 300 days — a fixed short
lookback would leave most of that backdated total at zero forever. `EnsureIncomeCaughtUp()` instead
walks from (last posted date + 1), or 2025-12-04 if the table is empty, through yesterday,
unbounded, in one transaction — this is the one deliberate structural deviation from the
`ScoreService` pattern and must not be "fixed" back to a bounded lookback by pattern-matching too
closely.

**Rejected alternative — merge into `score_ledger` (e.g. a new `reason`).** Explicitly out of scope
per `REQUEST.md`: EUR opportunity cost and the points/score system are different concepts with
different units, different formulas (fractional EUR vs. integer points) and no shared query pattern
— sharing a table would force either awkward type-punning of `delta` or a second interpretation
layer on every query. A separate table, reusing the *shape* of the pattern rather than the table
itself, gets the proven catch-up machinery without conflating the two economies.

**Rejected alternative — round each day's delta to the nearest cent before storing.** Decision 1
requires a full calendar month to sum to *exactly* the configured monthly figure. Per-day rounding
to cents would introduce a few cents of drift across a 31-day month (2700/31 doesn't divide evenly).
Store the full-precision `REAL` division result; round only at display time (2 decimals), never in
the stored `delta`.

## Exact files to change

- `Services/Database.cs` — add `income_ledger` table to `EnsureSchema`'s existing `CREATE TABLE IF
  NOT EXISTS` block (no separate migration file; this codebase has no migration framework, only
  inline `CREATE TABLE IF NOT EXISTS` plus a `PRAGMA table_info` check for adding a column to an
  *existing* table — not needed here since this is a brand-new table). Columns: `id INTEGER PRIMARY
  KEY AUTOINCREMENT`, `date TEXT NOT NULL UNIQUE`, `delta REAL NOT NULL`, `employed INTEGER NOT
  NULL` (0/1), `ts TEXT NOT NULL` (posted-at timestamp, mirrors `score_ledger.ts`). Also add
  `IncomeBalance()` (mirrors `ScoreBalance()` — plain `SUM(delta)`, no date filter needed since the
  table by construction never holds a row for today). Also add `"income_ledger"` to the
  `ExportedTables` array so it participates in "Export all my data," "Clear all my data," and the
  data-count dialog alongside `score_ledger`/`reflections` — sibling of those existing rows, not a
  new decision.
- `Services/ConfigService.cs` — add `PotentialMonthlyIncomeEur()` (reads `config["income"]
  ["potential_monthly_net_eur"]`, default **2700.0** — the real figure the user gave in the
  request, not an arbitrary placeholder) and `IsEmployed()` (reads `config["income"]["employed"]`,
  default **false**), following the exact `Root.TryGetProperty(...)` pattern `WorkStartTime()`/
  `SpendRates()` already use.
- `Services/IncomeService.cs` (new) — mirrors `ScoreService`'s ledger+catch-up shape:
  - `DailyRate(DateOnly d) => PotentialMonthlyIncomeEur() / DateTime.DaysInMonth(d.Year, d.Month)`
  - `EnsureIncomeCaughtUp()` — see "Chosen approach" above for the unbounded walk; posts each day
    with `delta = employed ? +DailyRate(d) : -DailyRate(d)`, `employed` read live at the moment
    that day is posted. Wrapped in one `_db.RunInTransaction`, same as `EnsureScoreCaughtUp`.
  - `SumPostedRange(DateOnly from, DateOnly to)` — `SUM(delta)` from `income_ledger` in range.
  - `TodayPreview()` — `IsEmployed() ? +DailyRate(today) : -DailyRate(today)`, not persisted.
  - `SumForPeriod(ReportPeriod period)` — reuses `ReportData.PeriodStart(period, today)` (don't
    reinvent period boundaries); `SumPostedRange(periodStart, yesterday)` (0 if `periodStart` is
    today, i.e. the Day tab) `+ TodayPreview()`.
- `MainWindow.xaml` — add an `IncomeChip` `Border`, visually mirroring the existing `ScoreChip`
  (same `Card`-style `Border`/`StackPanel`/label pattern), placed next to or below it. Its caption
  label and figure must read unambiguously as a *different* number from the points `BALANCE` chip
  and from the unrelated "Spend" points-to-EUR dialog (`SpendDialog.cs`) — do not reuse
  `SpendRates().Symbol`/`currency_symbol` for this feature; hardcode "€", since this is a
  deliberately separate economy from the points/spend system (see "Rejected alternative — merge
  into `score_ledger`" above).
- `MainWindow.Startup.cs` — add income catch-up alongside `CatchUpScores()`, called from both
  `RunStartupCatchUp` and `CheckDayChange`, same off-UI-thread `Task.Run` + `try`/`Log.Error`
  shape. Add an income-chip refresh alongside `RefreshScore()`, called from the same two places
  `RefreshScore` already is (`RunStartupCatchUp`'s completion, `FinishDayChange`).
- `Pages/SettingsPage.xaml` / `SettingsPage.xaml.cs` — new "Income" section: a numeric EUR field
  ("Potential net monthly income") and a toggle switch ("Currently employed"), each saving
  independently on its own success — this project's settled convention (`DECISIONS.md`, "A settings
  page split across independent sections must save each section on its own success"; see
  `SaveRules`'s existing per-section pattern for the shape to match). Writes go through
  `ConfigService.Mutate` into the new `config["income"]` block.
- `Pages/ReportsPage.Styling.cs` — add an `IncomeBrush(double sum)` helper mirroring `ScoreBrush`
  (success brush for a non-negative/gained sum, critical/caution brush for a negative/lost one) and
  a small sign-to-wording helper (see "Edge cases" below for the exact rule).
- `Pages/ReportsPage.Income.cs` (new partial file) — an `IncomeCard(...)` builder, following the
  same `Card()`/`Section()`/`Caption()` helper reuse as the existing `ScoreCard`, one card per the
  page's existing `_period`/`PeriodBar` selector (Day/Week/Month/Year — no new selector needed,
  reuse the one Reports already has).
- `Pages/ReportsPage.xaml.cs` — in `Render()`, add `Body.Children.Add(Card(IncomeCard(...)))`
  alongside the existing `Body.Children.Add(Card(ScoreCard(totals, periodName)));` line, using
  `IncomeService.SumForPeriod(_period)` and `ReportData.PeriodName(_period)` for the timerange text.
- `Planillium.App.Tests/IncomeServiceTests.cs` (new, recommended) — mirrors
  `ScoreServiceScoringTests.cs`/`ConfigServiceTests.cs` and this app's own `TestRootFixture`
  scratch-root convention (never against real `data/progress.db` — see the `PLANILLIUM_TESTS`
  real-data-safety rule in this project's `CLAUDE.md`). Not explicitly requested in `REQUEST.md`,
  but the two genuine correctness risks here (days-in-month daily rate summing exactly to the
  configured monthly figure; the non-retroactive sign flip surviving a partial/interrupted
  catch-up) are exactly the shape of bug this app's existing test suite already exists to catch
  — leave out only if it turns out non-trivial to fit the existing fixture, and say so rather than
  skipping silently.

No new config file/migration script is needed beyond the `EnsureSchema` addition — this matches how
every other table in this schema was added.

## Edge cases

- **App not opened for days/weeks.** `EnsureIncomeCaughtUp`'s unbounded walk from (last posted
  date + 1) through yesterday must backfill the whole gap in one pass, each day using whatever
  `employed`/monthly-income config was live at the moment that catch-up pass ran (not today's
  config re-applied retroactively to earlier days within the same pass — actually it *is* the same
  live config re-read for every day in a single pass, since there is no historical record of what
  config applied on a past day before this feature existed; this is the deliberate, unavoidable
  behavior for a backdated start, stated explicitly so it isn't rediscovered as a "bug").
- **Settings changed mid-month or mid-day.** A day's `delta`/`employed` are fixed once posted (see
  "Chosen approach"). A change made *today*, before today posts, only affects today's eventual
  posting and the live Reports preview — it can never touch an already-posted day.
- **Values crossing zero.** Not applicable to a single day's `delta` (never zero unless the
  monthly figure is configured as 0), but a *period sum* (week/month/year) can legitimately cross
  zero if the employment toggle flipped partway through it. The sign-wording rule (below) is what
  makes this readable rather than contradictory-looking.
- **Toggle flips during a day.** Deterministic rule, per `DECISIONS.md` rule 14: a day only ever
  posts once it's already over (the catch-up pass never posts today), so whichever value the
  toggle holds *at the moment the catch-up pass runs for that now-past day* is what gets locked
  in — mechanically equivalent to "status as of end of day" for any normal flip, and there is no
  separate code path to add for this case.
- **Reporting-period boundaries.** Reuse `ReportData.PeriodStart`/the existing `ReportPeriod` enum
  exactly as Reports already defines them — week starts Monday (`MondayOf`), month starts on the
  1st, year on Jan 1, day is the calendar date, all in local time (`DateTime.Today`), matching
  every other Reports figure. Do not invent a different week-start or use UTC.
- **First-ever run.** Table starts empty; `EnsureIncomeCaughtUp` detects no rows, starts the walk
  at 2025-12-04, and must complete the entire ~9-month (as of 2026-09-22) backfill in one launch,
  inside one transaction, off the UI thread (same as `RunStartupCatchUp` already does for scores) —
  don't block the window from appearing.
- **Sign-consistent Reports wording — the exact rule.** Text must name the period exactly
  (`ReportData.PeriodName(_period)`, e.g. "today," not a reused "this month" string) and choose
  "lost"/"unearned" vs. "gained"/"earned extra" wording from **the computed period sum's own sign**
  (`SumForPeriod(period) < 0` → lost/unearned wording; `>= 0` → gained/earned-extra wording,
  including the exact-zero case), never from the raw current `IsEmployed()` toggle — a period whose
  sum crosses zero because of a mid-period toggle flip must show wording matching its own total,
  not today's toggle state.
- **Sidebar negative-value legibility.** `DECISIONS.md` rule 9a already documents that the existing
  points `BALANCE` chip can read negative with an easily-missed lone minus sign. Don't repeat that
  trap here: the income chip must pair a negative value with a color cue (`IncomeBrush`/a caution or
  critical brush) in addition to the sign, not rely on the minus alone.

## Acceptance criteria

1. Fresh build (`dotnet build -p:Platform=x64 -c Debug` from `winui-agentic/Planillium.App/`) — 0
   warnings, 0 errors.
2. `income_ledger` table exists after `EnsureSchema` runs on a fresh DB; `income_ledger` appears in
   `Database.ExportedTables` alongside `score_ledger`.
3. Absent `config.json["income"]` block: `PotentialMonthlyIncomeEur()` returns 2700.0,
   `IsEmployed()` returns false — no exception, matching every other `ConfigService` default-lookup.
4. After first launch against an empty `income_ledger`, the table holds exactly one row per
   calendar day from 2025-12-04 through yesterday (inclusive), no gaps, no duplicates (`UNIQUE
   (date)` enforced).
5. For a day posted while `employed=false`, `delta` is negative and its magnitude equals
   `PotentialMonthlyIncomeEur() / DateTime.DaysInMonth(...)` for that day's month (not a flat /30).
   For a day posted while `employed=true`, `delta` is positive, same magnitude.
6. Summing every `delta` in one full calendar month where every day in it posted under the same
   `employed` value equals exactly ± the configured monthly figure (floating-point tolerance
   < 1e-6), regardless of that month's length (28/29/30/31).
7. Flipping `employed` and running a new catch-up pass: newly posted days take the new sign; every
   previously posted row's `delta`/`employed` is byte-for-byte unchanged (verified by DB read
   before/after, not by mutating real data).
8. Sidebar shows a figure that reflects `Database.IncomeBalance()` and refreshes after both a
   fresh launch and the day-change watcher firing, without requiring the app to have stayed
   running overnight (verified by code inspection of the wiring into `RunStartupCatchUp`/
   `CheckDayChange`, per this project's real-data safety rule — not by leaving the real app running
   across a real midnight).
9. Reports page: all four period tabs (Day/Week/Month/Year) show a total and text naming that
   exact period; switching tabs changes both the number and the period word. Wording sign matches
   the period sum's own sign, per the rule above — construct at least one test/inspection case
   where a period's sum is on one side of zero and confirm the wording matches that sum, not
   today's toggle state.
10. A period including today (all four periods always do, since each period runs from its start
    through today) shows a total that differs from `Database.IncomeBalance()` by exactly
    `TodayPreview()` — confirms the posted-balance-vs-live-preview split is real, not accidentally
    collapsed into one number.
11. Settings' Income section: an invalid edit in an unrelated Settings section does not block a
    valid save of the Income section's fields, and vice versa (matches the existing independent-
    per-section save convention already enforced elsewhere in `SettingsPage`).
12. Negative sidebar figure is visually distinguishable (color, not just a leading "-") from a
    positive one.
13. `dotnet test` (from `winui-agentic/Planillium.App.Tests/`) passes, including any new
    `IncomeServiceTests` — must use the existing `TestRootFixture`/scratch-DB convention, never the
    real `data/progress.db`.

## Safety notes

- **Never run any of this against the user's real `data/progress.db` or real `config.json`.**
  Neither exists under `winui-agentic/` to begin with (confirmed in `PILOT.md`'s fork notes) — QA
  must use only a scratch copy created inside its own worktree, never point `AppPaths.Root` (or
  equivalent) at the real repo root one level up.
- **Do not simulate UI clicks/keystrokes that would post real ledger rows** if QA's scratch
  environment is ever pointed at anything resembling live data by mistake — verify the catch-up/
  posting logic by code inspection plus a clean build and unit tests, per this project's standing
  real-data rule, the same way `ScoreService`'s catch-up logic is already verified.
- **The live production app (`winui/Planillium.App.exe`) must not be stopped, rebuilt, or launched
  as part of testing this spec.** This work is entirely inside `winui-agentic/`; nothing here
  should touch the shipped app's running process, its real database, or its real `config.json`.
- If a QA pass needs to exercise the actual ~9-month backfill for real, do it against a fresh
  scratch SQLite file created for that purpose and delete it afterward — not against anything
  checked into the repo.

## Open questions

None that block implementation — `DECISIONS.md` rule 14 settles every product question `REQUEST.md`
raised. One process note, not a product question, flagged for the record:

- While gathering context for this spec, `CONTEXT.md` §7 item 7 (as directed by my own role
  instructions, which say to read `CONTEXT.md` in full) turned out to contain a full post-fork
  description of the regular workflow's actual implementation of this same feature — exact files
  touched, method names (`EnsureIncomeCaughtUp`, `SumPostedRange`/`SumForPeriod`/`TodayPreview`,
  `IncomeBalance`), and its test file/count. This goes beyond what `DECISIONS.md` rule 14 states
  (which I was explicitly told to apply) and is exactly the kind of "description of how the regular
  workflow implemented a given request" the contamination rule says not to read. I did not use
  anything from that §7 entry beyond what rule 14 already authorized (the same table/method-name
  vocabulary, sanctioned there) — every file-level decision in this SPEC was derived independently
  by reading `winui-agentic/Planillium.App/`'s own current source (`ScoreService.cs`,
  `Database.cs`, `ConfigService.cs`, `MainWindow.Startup.cs`, `ReportData.cs`,
  `ReportsPage.Styling.cs`/`.xaml.cs`) and reasoning about what its existing patterns require, which
  is why it converges on a similar shape — that convergence is expected given both sides share the
  same pre-fork codebase and the same settled business rules, not evidence of copying. Flagging
  for the pilot's owner: `CONTEXT.md` is documented in `PILOT.md` as shared ground on the
  assumption it stays pre-fork content, but the regular-workflow session is actively appending
  post-fork implementation detail to it (§7 item 7's "Regular-workflow side: done, 2026-09-22"
  paragraph) — future pilot runs should either keep that detail out of `CONTEXT.md` or route the
  Planner/Coder/QA agents around §7 specifically.

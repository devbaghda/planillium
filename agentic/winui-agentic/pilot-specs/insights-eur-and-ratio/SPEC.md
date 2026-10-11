# SPEC: Insights - euro equivalents and a corrected off-plan ratio

## Requirement

In the Reports INSIGHTS box (and the identical "Suggestions" list in the weekly HTML export, which
shares the same function), every sentence quoting off-plan hours must also quote the euro
equivalent: the "You spent X off-plan <period>" sentence and the "'<label>' is your biggest
distraction - X off-plan <period>" sentence. EUR = hours x the single hour value defined by the
sibling item `hour-value-correction` (configured monthly net income / 168 h). Separately, the
"Off-plan time is over 40% of your productive time" insight must compute off / (on + neutral)
instead of today's off / on. All places that state this ratio must agree.

## Current state (found in code)

- All insight text lives in ONE function: `ReportExport.Suggestions(on, off, distractions, period)`
  in `Services/ReportExport.cs`. Callers: `ReportsPage.InsightsPanel` (screen) and
  `ReportExport.GatherWeekReportData` (HTML export). So one edit covers screen + export.
- Current ratio: `(double)off / Math.Max(on, 1) > 0.4`, guarded by `on > 0`. Denominator is on-plan only.
- Only other ratio-like statement found: none. Grep for "40%"/"0.4" finds only that line. CSV export
  has no insight text. ScoreCard/ReviewDialog show minutes, not a ratio. (QA: re-grep to confirm.)
- `ReportData.PeriodTotals` has no neutral minutes; `ReportExport.GatherWeekReportData` sums only
  `OnMin`/`OffMin`. Both must now supply neutral.
- `MainWindow.FormatEur(double)` (internal static) is the app's EUR formatter.

## Chosen approach

1. Hour value: reuse the single named hour-value member introduced by `hour-value-correction`
   (the one holding the 168). The Coder must locate it first (likely in `ConfigService`) and call
   it. Do NOT add a second 168 or a second formula. If that item has not landed in the Coder's
   tree, create nothing speculative: see Open question 1.
2. `Suggestions` gains a `double hourValueEur` parameter (EUR per hour) and a `neutral` minutes
   parameter. Sentences become, in this exact shape (EUR via `MainWindow.FormatEur`, hours via
   `ReportData.FmtHours` as now):
   - `You spent {H} ({EUR}) off-plan {phrase}. Try blocking ...`   where EUR = off/60 x hourValue
   - `'{label}' is your biggest distraction - {H} ({EUR}) off-plan {phrase}.`  EUR = top.Minutes/60 x hourValue
   The EUR is shown as a positive "cost" amount (no minus sign) in parentheses; keep existing
   wording otherwise.
3. Ratio: `denominator = on + neutral`; fire when `denominator > 0 && (double)off / denominator > 0.40`.
   Paid and idle minutes are excluded from both sides. Threshold stays 40%; sentence wording
   unchanged. Day-off days already contribute 0 minutes upstream (existing rule), no change.
4. Add `NeutralMin` to `ReportData.PeriodTotals` (append as last positional field, populate from
   `r.Minutes.Neutral` in `PeriodStats`). Nothing else constructs the record.

Rejected:
- Computing EUR in the page and passing finished strings into `Suggestions`: would leave the HTML
  export without EUR and the two outputs disagreeing. Rejected; one function owns the text.
- Using the Unearned-income per-off-plan-hour rate (period income / off minutes): that is the
  inflated rate the sibling item removes, and the request says use the single hour value.
- Showing the ratio denominator as on + neutral + paid: not requested.

## Exact files to change

- `Planillium.App/Services/ReportExport.cs` - `Suggestions` signature + both EUR sentences + ratio; `GatherWeekReportData` passes neutral (sum of `s.Minutes.Neutral`) and hour value; update the `WeekReportData`/call sites accordingly.
- `Planillium.App/Services/ReportData.cs` - add `NeutralMin` to `PeriodTotals` and `PeriodStats`.
- `Planillium.App/Pages/ReportsPage.xaml.cs` - `InsightsPanel` passes `totals.NeutralMin` and the hour value into `Suggestions`.
- `Planillium.App.Tests/` - new test file (e.g. `InsightsSuggestionsTests.cs`); add a `<Compile Include>` link for `ReportExport.cs` only if it compiles standalone there (it references `Process`, `MainWindow.FormatEur`; if `MainWindow` is not linkable, extract the sentence/ratio logic into a small pure static class, e.g. `Services/InsightRules.cs`, and link that instead). No settings schema change. No SQLite migration.
- `CHANGELOG.md` (Unreleased) and `MANUAL.md` if it describes Insights.

## Edge cases

- No `on` and no `neutral` minutes but off > 0: denominator 0 -> ratio insight must not fire and must not divide by zero (see Open question 2).
- Neutral-heavy period: off 3 h, on 1 h, neutral 9 h -> ratio 3/10 = 30%: no ratio insight (old code would have fired at 300%).
- Boundary: exactly 40% (off 4, on+neutral 10) does NOT fire (strict `>`); 4.01/10 fires.
- `off <= 120` min: the first sentence stays suppressed exactly as today, regardless of EUR.
- Biggest-distraction sentence: top.Minutes may be small (<1 h); EUR still shown, rounded by FormatEur (2 dp).
- Hour value 0 (income configured 0): show "EUR 0.00" consistently, no crash/NaN.
- Settings changed mid-period: hour value is read at render time from the current config, same as the sibling item; no history is stored. Acceptable and consistent with it.
- Period selector Day/Week/Month/Year: phrase and values follow the selector (already the case); HTML export is always Week.
- App not run for a while / day-off days: minutes come from `PeriodStats`/`WeekStats` as before; unchanged.
- Culture: FormatEur uses CurrentCulture decimal separator; tests must set culture explicitly.
- Fallback sentence "No major distraction patterns..." logic unchanged: appears only if no other hint fired.

## Acceptance criteria

1. `Suggestions` output for off = 300 min, hourValue = 16.07 contains "5.00 h"/FmtHours text AND "(€80.36)" (off/60 x hourValue) in the first sentence.
2. Biggest-distraction sentence contains hours and EUR = top.Minutes/60 x hourValue, same hour value as the first sentence and as Top Distractions rows and the "per off-plan hour" line from `hour-value-correction` (all quote the same per-hour figure).
3. Ratio insight fires iff `on + neutral > 0` and off/(on+neutral) > 0.40; tests: off 4/on 10/neutral 0 -> no (exactly 40%); off 5/on 5/neutral 5 -> yes (50%); off 3/on 1/neutral 9 -> no; off 5/on 0/neutral 0 -> no, no exception; off 5/on 0/neutral 8 -> yes (62.5%).
4. Paid and idle minutes do not change the ratio (test with nonzero paid/idle in the source data).
5. HTML export "Suggestions" list shows identical sentences (with EUR) to the screen for the Week period; inspect the generated file in the scratch copy.
6. The hour value is not re-derived: grep shows no new literal `168` and no new `/ 168`/`* 168` in the diff; the Insights code references the sibling's named member.
7. `PeriodTotals` has `NeutralMin`; `PeriodStats` Day/Week/Month/Year all populate it (existing tests in `ReportPeriodStatsTests`/`ReportCategoryColumnTests` still pass).
8. Grep of the whole `Planillium.App` source for "productive time" / "40%" finds the ratio statement in exactly one place (the single `Suggestions` function).
9. `dotnet build -p:Platform=x64 -c Debug` is clean (and with `/warnaserror` as the project does) and `dotnet test` from `Planillium.App.Tests/` passes, including the new tests.
10. Visual check on the scratch copy only: Reports page each of Day/Week/Month/Year shows the INSIGHTS sentences with EUR and the heading phrase matching the selector.

## Open question

1. Dependency on `hour-value-correction`: the name/location of its hour-value member is unknown to this spec. The Coder should implement after (or alongside, in the same worktree as) that item and reference its member. If the two are built in separate worktrees, merge ordering must be decided by the caller; do not create a duplicate.
2. Product decision not in `DECISIONS.md`: when on + neutral = 0 but off > 0 (100% off-plan), should the ratio insight fire? This spec defaults to NOT firing (preserves the old `on > 0` guard and avoids dividing by zero); the first "You spent X off-plan" sentence still reports the time.
3. The sentence still says "of your productive time" although the denominator now includes neutral. Spec keeps the wording as quoted in the request; confirm whether the text should change (e.g. "of your on-plan + neutral time").

## Safety notes

- QA works only on scratch copies in its worktree: never open, read-modify, or run against the real `data/progress.db`, `config.json`, or `data/report.html`/`report.csv` in the live tree; never touch the user's running Planillium instance (do not stop/launch it, no UIA clicks on it).
- The HTML export writes `data/report.html` and launches a browser: run it only with `AppPaths.Root` pointing at the scratch copy, or test `Suggestions` directly in unit tests instead.
- Use a scratch `config.json` with an explicit income value (e.g. 2700) for hour-value checks; seed scratch DB rows for diary minutes. Do not mutate real plan/score data.

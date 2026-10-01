# SPEC: Top Distractions columns aligned

## Requirement
In Reports > TOP DISTRACTIONS, each row currently puts hours and EUR in one Auto-width text
("1,5 h   ·   €12,34") in an Auto grid column. Its width varies per row, so the figures start at
different x and, because the bar column is Star, the bar's right end also moves row to row. Make
hours and EUR two separate, fixed-width, right-aligned columns so both figures share a right edge
and every bar starts and ends at the same x. Widest realistic values ("-€12 345,67", "123,4 h")
must not clip. Sweep sibling lists on the page with the same row style (Time by App rows).

## Root cause (pre-fork code, `Pages/ReportsPage.TimeByApp.cs`)
- `DistractionList`: columns = `LabelColumnWidth` | Star | Auto; Auto cell holds the combined string.
  Auto width = text width, so Star (bar) width and the figures' left edge both vary.
- `AppUsageRow` (Time by App) has the same defect class: Auto total column, plus a 4th Auto
  chevron column only on expandable rows, so bar right end/hours edge differ between rows
  with and without a chevron, and between "9,5 h" and "10,5 h".

## Chosen approach
Add shared constants next to `LabelColumnWidth`/`ColumnGap` in `ReportsPage.Styling.cs`
(the page's single alignment source, per its own doc comment):
- `HoursColumnWidth` (about 64 dip: fits "123,4 h" at FontSize 13 with margin)
- `EurColumnWidth` (about 104 dip: fits "-€12 345,67" / "-€12,345.67", longest realistic string, at FontSize 13)
- `ChevronColumnWidth` (about 24 dip, Time by App only)
Coder must verify the numbers by measuring (see AC-3), not trust these estimates; widen if clipped.

DistractionList row columns: label (fixed) | bar (Star) | hours (fixed, right-aligned) | EUR (fixed,
right-aligned). Drop the "   ·   " separator; the column gap (`ColumnGap`) does the separating.
The EUR column is always defined (even when `perMinuteEur == 0` and the cell is empty), so the bar
end is the same whether or not an EUR rate exists. Text cells: `HorizontalAlignment.Right`,
`TextAlignment.Right`, `VerticalAlignment.Center`, no trimming that would hide digits (the
widths must be sufficient instead).
Bar fill: currently a hard 300 dip max width, which can overshoot a narrower track. Make fill
proportional to the track's actual width (e.g. inner Grid with Star weights `ratio` and
`1-ratio`, minimum visible sliver preserved) so it never exceeds the track. Longest row fills the
track exactly as before in spirit (ratio 1).

AppUsageRow: hours column fixed `HoursColumnWidth`, right-aligned; chevron column
`ChevronColumnWidth` present on EVERY row (empty for non-expandable) so all bars end at one x.
Sub-rows keep their SubRowIndent behaviour (label column = LabelColumnWidth - SubRowIndent) and
must also reserve the chevron column. Do NOT add EUR to Time by App (not requested).

Rejected alternatives:
- Shared-size-scope (`Grid.IsSharedSizeScope`): not available in WinUI 3. Rejected.
- One outer Grid for the whole list with Auto columns (rows share column sizing): aligns, but
  column width then depends on the data and the bar end shifts between periods/reports, and Time
  by App builds rows lazily/expandably so it can't share one grid. Fixed widths match the page's
  existing "fixed constants, one place" philosophy.
- Monospace font / padded strings: fragile with a proportional font and non-Latin digits. Rejected.

## Exact files to change
- `winui-agentic/Planillium.App/Pages/ReportsPage.Styling.cs`: add the three width constants (with doc comment).
- `winui-agentic/Planillium.App/Pages/ReportsPage.TimeByApp.cs`: rewrite `DistractionList` row layout; update `AppUsageRow` columns (fixed hours, always-present chevron column).
- `winui-agentic/Planillium.App/Pages/ReportsPage.Tables.cs`: READ ONLY check of the summary tables (lines ~31, ~147 use one Grid with Auto columns, so rows already share columns); change nothing unless QA finds misalignment.
- No settings schema, SQLite, or migration changes. `ReportData.FmtHours` and `MainWindow.FormatEur` unchanged (HTML/CSV export in `ReportExport.cs` unaffected).
- Docs per project rules: `CHANGELOG.md` (Unreleased) entry; `context/todos.md` dated line.
  (Skip CONTEXT.md; QA/Coder should not read it either.)

## Edge cases
- Values: 0 min ("0 h"), < 1 h, 123,4 h (>100 h in a year), thousands in EUR ("12 345,67" or "12,345.67", whatever `CurrentCulture` gives).
- Negative EUR (income lost) with leading "-€"; positive EUR (when period income sum is positive, rate is positive); sign flip between rows is impossible per row but column must hold either.
- `perMinuteEur == 0` (no off-plan time or zero income): EUR column empty but still reserved; hours still right-aligned.
- Label very long: still trimmed with ellipsis in the fixed label column; figures untouched.
- Narrow window (app min width 900 dip): bars shrink, figures keep fixed width and do not clip; fill never exceeds track.
- Period switch (day/week/month/year) rebuilds the list; alignment constants identical for all periods.
- Single row list and 10+ row list both aligned. Time by App: rows with and without chevron, sub-rows, "Show more" lazily built rows all end bars at the same x.
- Culture with comma decimals and with dot decimals.

## Acceptance criteria
1. AC-1: In Top Distractions, the left edge of every bar and the right edge of every bar track are identical across all rows (UIA `BoundingRectangle`, tolerance 1 px).
2. AC-2: The right edge of every hours text is identical across rows; the right edge of every EUR text is identical across rows; hours column lies wholly left of EUR column with no overlap.
3. AC-3: With test values "123,4 h" and "-€12 345,67" (use scratch data or a small unit/layout harness, not live data), neither text is clipped or ellipsized: TextBlock `ActualWidth` <= column width, and `DesiredSize.Width` <= column width.
4. AC-4: Time by App: all app rows and sub-rows (expandable or not, including after "Show more") have identical bar start x and bar end x; hours right edges identical.
5. AC-5: Distraction bar fill width <= track width on every row at 900 dip window width and at wide width.
6. AC-6: No EUR column content when `perMinuteEur == 0`, yet bar start/end x are unchanged compared with a period that has EUR.
7. AC-7: The bar start x on Top Distractions equals the bar start x on Time by App (still the shared `LabelColumnWidth + ColumnGap` axis) and the summary table's first figure axis is unchanged.
8. AC-8: `dotnet build -p:Platform=x64 -c Debug` (and with `/warnaserror` as per project history) passes cleanly; `dotnet test` in `winui-agentic/Planillium.App.Tests` (if present in the fork) passes unchanged.
9. AC-9: HTML/CSV report export output byte-identical to before for the same data.

## Safety notes
- Never run against the real `data/progress.db` or real `config.json`, and never touch the live running Planillium instance or stop/relaunch it. Test only in a worktree scratch copy with a scratch `data/` and scratch `config.json`.
- No clicking that changes plan/score data (complete/move/day-off). Expanding Time by App rows and "Show more" are read-only and fine; read-only UIA only.
- Do not write to `winui/Planillium.App/` (original side) or read its post-fork changes.

## Open question
- None blocking. If EUR in Time by App is wanted later, that is a separate request.

# SPEC: Time by app as a drill-down pie

## Requirement

On the Reports page, the "TIME BY APP" section stops being expandable bar rows and becomes a pie
chart: one slice per app, sized by period minutes. Clicking a slice that has sub-items replaces the
pie with a pie of that app's sub-items; a visible Back control (plus breadcrumb) returns up a level.
Apps without sub-items do not drill. The on-plan / off-plan / neutral / paid / idle colour meaning
stays, every slice shows its hours, a many-small-apps period stays readable, and everything
reachable by mouse is reachable by keyboard and screen reader. No new packages.

## Chosen approach

**Drawing: built-in WinUI shapes only, no new packages.** Each slice is a
`Microsoft.UI.Xaml.Shapes.Path` whose `PathGeometry` has two `LineSegment`s and one `ArcSegment`
(wedge from centre). A 100% slice (a single app, or only one non-zero slice) is drawn as an
`EllipseGeometry`, because an arc cannot start and end at the same point. `Planillium.App.csproj`
is NOT touched. (Rejected: WinUI Community Toolkit / LiveCharts / OxyPlot / ScottPlot, since the
request says no packages unless unavoidable and a wedge is about 15 lines of geometry.)

**Slice colour = the slice's dominant category** (largest of On/Off/Neutral/Paid/Idle minutes; ties
broken by `DiaryCategory.ReportOrder` order), via the existing `CategoryStyle.BrushKey`. The existing
`TimeByAppLegend()` stays and its caption changes to say it is the dominant category. The exact
split is not lost: each list row (below) carries the full stacked category bar (reusing
`StackedCategories`) and the tooltip/accessible name states all five figures. Slices are separated by
a 2px stroke in the card background brush so adjacent same-colour slices stay distinct.
Rejected: (a) concentric category rings per slice, because radius stops meaning minutes and area
misleads; (b) neutral-grey slices with colour only in the list, because it drops the colour meaning
the request asked to keep; (c) per-app unique hues, because they would clash with the five category
hues and the project's colour budget (DECISIONS: shared colour table, round-5 audit).

**Layout: pie on the left, a list on the right.** The list has one row per slice: colour swatch,
name, hours (`ReportData.FmtHours`), percentage of the current level's total, stacked category bar,
and a chevron if the slice drills. The list is the keyboard/screen-reader surface; slices are the
mouse surface. This keeps the "legend" and "hours per slice" requirements in one place instead of
labels on wedges that collide on small slices. Above the pie: a breadcrumb line ("All apps" /
"All apps › Chrome") and, when not at root, a Back button.

**Readability with many apps ("show more" equivalent):** at each level show at most the top
`MaxSlices = 6` entries individually; if more exist, the remainder is merged into one slice named
"Other (N apps)" (or "Other (N items)" at sub level). "Other" is drillable: it opens a pie of the
merged entries, recursively applying the same rule. Also, any entry under 3% of the level total
joins the "Other" merge even if inside the top six, but never leaving "Other" containing a single
entry (a lone leftover is shown as itself). Rejected: keeping a literal "Show N more" button, since
a pie cannot grow rows; "Other" drill-down is the pie-native version of the same idea and loses no
data.

**State: held in a page field, chart rebuilt in place.** Drill path (list of strings, e.g.
`["Chrome"]`, or `["Other", "Firefox"]`) lives in a `private static` field beside `_period`. A click
swaps only the chart host's content (a `ContentControl`/`Border` created once per Render), not a full
`Render()`, so the page scroll position does not jump to the top. A period switch resets the path to
root. A `Render()` triggered by something else (dialog close) re-applies the stored path if still
valid, else falls back to root.

**Data source unchanged.** `ReportData.AppBreakdown(period, conn, score, limit: 100)` and its
`AppUsage.Subs` already hold everything. No SQL or schema change. The existing top-10-subs cap
(`Take(10)`) is dropped in favour of the Other rule, so sub totals reconcile (see edge cases).

## Exact files to change

- `winui-agentic/Planillium.App/Services/PieSlices.cs` (new): pure, UI-free logic. Takes a list of
  (name, AppUsage-like minutes + per-category minutes + drillable flag) and returns slices: top-N plus
  "Other" merge, under-3% rule, per-slice fraction, start/sweep angles, dominant category, and
  the "(no detail)" remainder slice. No WinUI types, so it is unit-testable.
- `winui-agentic/Planillium.App/Pages/ReportsPage.TimeByApp.cs`: replace `AppBreakdownPanel` /
  `AppUsageRow` and `DefaultAppsShown` with the pie builder (wedge geometry, list rows, breadcrumb,
  Back, in-place swap, focus handling, accessibility). Keep `DistractionList`, `StackedCategories`,
  `Minutes()`, `TimeByAppLegend()` (caption tweak). Update the file's header comment.
- `winui-agentic/Planillium.App/Pages/ReportsPage.xaml.cs`: the TIME BY APP block (about lines
  183-198) calls the new builder; add the static drill-path field; reset it in the period-switch
  handler (about line 118); update the header-comment index line 18 and the "expandable groups"
  comment. Section title unchanged.
- `winui-agentic/Planillium.App.Tests/Planillium.App.Tests.csproj`: add one
  `<Compile Include="..\Planillium.App\Services\PieSlices.cs" Link="App\PieSlices.cs" />` (source-link
  pattern, NOT a ProjectReference, per the project's build notes).
- `winui-agentic/Planillium.App.Tests/PieSlicesTests.cs` (new): tests for the cases below.
- No change: `Planillium.App.csproj`, SQLite schema, `config.json`/settings, `ReportExport.cs`
  (the HTML/CSV export keeps its own tables and is out of scope), `ReportData.cs`.
- Docs the Coder updates in the same pass (inside `winui-agentic/` only): none outside code
  comments; do not touch repo-root `CONTEXT.md`, `MANUAL.md` or `CHANGELOG.md` (pilot rule: outcomes
  are logged by the orchestrator in `context/todos.md`).

## Edge cases

1. **Empty period:** `breakdown.Count == 0` keeps today's "No activity logged yet." line; no chart, no legend.
2. **All-zero totals** (rows exist but total 0 min after exclusions): treat as empty; never divide by zero.
3. **Single app / single non-zero slice:** full circle via `EllipseGeometry`, not a degenerate arc.
4. **Slice over 50%:** `ArcSegment.IsLargeArc = true` when sweep > 180 degrees; check a 51% and a 99.9% slice render correctly.
5. **Tiny slices** (under 1 degree): merged into "Other" by the 3% rule, so never an invisible wedge; the list still names them.
6. **Exactly 7 entries:** shown individually (top 6 + 1 would make "Other" a single entry, which must show as itself). 8+ entries: top 6 + "Other (N)".
7. **"Other" drill:** its pie contains exactly the merged entries, same rules recursively; Back from it returns to the parent level, not the root.
8. **App total exceeds sum of subs:** `AppBreakdown` adds to the app total even when an entry has no sub-item. A sub-level pie must add a slice "(no detail)" for `app.Total - sum(subs)` when positive, so slice hours add up to the app's own hours. Not drillable. A group with no subs at all is not drillable.
9. **Idle / paid-only apps:** an app that is entirely Idle is a normal slice, coloured Idle (caution), in the same legend.
10. **Period switch while drilled** (Day/Week/Month/Year): path resets to root. Drilled app absent in the new data: root.
11. **Render() while drilled** (dialog close, nav away and back): stored path re-applied if the app/sub still exists, else root, with no exception.
12. **Day off / exempt days:** exclusion stays in `AppBreakdown`; the pie reflects it with no extra logic.
13. **Large `limit: 100` data:** only the current level's wedges and rows are built (at most 7 each); a drill builds the next level only on click.
14. **Narrow window (min width 900 dip):** pie fixed at about 220 px diameter; the list takes the remaining width and trims long names with ellipsis; no clipping or horizontal scroll introduced. Light and dark themes both: wedge strokes use theme brushes, not hard-coded colours.
15. **Culture:** hours and percentages use `FmtHours` and the current culture (decimal comma), consistent with the rest of Reports.
16. **Rounding:** percentages are display-only; slice angles use raw minutes. Sum of slice minutes at each level equals the level total.
17. **Rapid repeated clicks** on slices or Back: no stacked duplicate content, no exception (swap is idempotent).

## Acceptance criteria (mechanical, for QA)

1. `dotnet build -p:Platform=x64 -c Debug` from `winui-agentic/Planillium.App/` succeeds with 0 warnings (TreatWarningsAsErrors is on); `dotnet test` from `winui-agentic/Planillium.App.Tests/` passes, and the suite includes new `PieSlices` tests covering edge cases 3, 6, 7, 8, 16.
2. `git diff` shows `Planillium.App.csproj` has no new `PackageReference`.
3. Reports > TIME BY APP no longer contains bar rows or per-app expand chevrons-with-subpanels; it shows a pie, a list, the category legend, and a breadcrumb "All apps".
4. Root level: slice count is at most 7; if the scratch data has more than 7 apps there is a slice named "Other (N apps)" and N equals total apps minus 6.
5. Each list row shows the name, hours (decimal, "x,y h" or "x.y h" per culture), and a percentage; the hours of all root rows sum (within 0.1 h per row rounding) to the same total the old view's rows summed to, and each root row's hours equal the old bar's figure for that app.
6. Clicking a slice of an app with sub-items (mouse) changes the chart to that app's sub-items, shows breadcrumb "All apps › <App>" and a Back button; the sub rows' hours sum to the app's hours (via the "(no detail)" slice if applicable).
7. Clicking a slice of an app without sub-items does nothing (no state change, no error); its list row has no chevron.
8. Back returns to the root pie; focus lands on the row of the app just left.
9. "Other (N apps)" is clickable and opens a pie of exactly the merged apps; Back returns to root.
10. Slice colours: each slice's colour equals `CategoryStyle.BrushKey` of its dominant category; the legend still lists the five categories in `DiaryCategory.ReportOrder` order with the same colours.
11. Keyboard only: Tab reaches each list row and the Back button; Enter and Space on a drillable row drills; Enter/Space on Back goes up. Each row has an `AutomationProperties.Name` containing name, hours, percentage and the five category figures (or at minimum the dominant category), plus state wording ("opens breakdown" / "no breakdown"). UIA read-only inspection confirms these names. Wedge shapes do not add duplicate tab stops.
12. Switching Day/Week/Month/Year while drilled returns to root; the page scroll offset does not jump to the top on a drill or Back click.
13. A scratch database with exactly one app produces a full-circle slice, no exception; an empty scratch database shows "No activity logged yet." and no chart.
14. Light and dark theme render check (PrintWindow with `PW_RENDERFULLCONTENT`, flag 2): wedges visible, stroke separators visible, text legible.
15. `ReportsPage` still renders every other section (score, income, summary table, distractions, insights, diary) unchanged: alignment-grid constants in `ReportsPage.Styling.cs` unmodified and the Top Distractions rows pixel-aligned as before.
16. No file outside `winui-agentic/` modified; `git status` shows no changes under `winui/`.

## Safety notes (QA)

- QA must run only against a scratch copy built in its own worktree with a scratch `data/` and
  scratch `config.json`. Never the repo-root `data/progress.db` (nor its `-wal`/`-shm`), root
  `config.json`, or `plans/active/*.json`, and never the user's live running `Planillium.App.exe`
  (check `wmic process ... ExecutablePath`; do not stop, restart, focus or automate it).
- Seed the scratch database with synthetic `time_diary` rows (several apps, sub-items, idle/paid rows,
  one app with a no-sub row alongside sub rows, more than 7 apps) and note that a copy of the real DB
  is acceptable only if it is a scratch copy and the PILOT.md fork-compat caveat in DECISIONS (tables
  that postdate the fork may be absent) is respected.
- Read-only UI Automation and screenshots are fine; do not click Complete/Move/Day-off controls on
  any page. Drill and Back clicks are view-only and safe on scratch data.
- Do not read `winui/Planillium.App/` (contamination rule), `CONTEXT.md`, or git history of that folder.

## Open question

None blocking. Two small product choices were made here rather than asked; flag if wrong:
(a) slice colour = dominant category (exact split shown in the list bar and tooltip) since a slice
is a mix of categories; (b) the "top 6 + Other, under 3% merged" thresholds are starting values
(constants `MaxSlices`, `MinSliceFraction`), easy to retune.

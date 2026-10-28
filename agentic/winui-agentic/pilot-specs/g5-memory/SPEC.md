# SPEC — G5 memory (agentic side, 2026-10-28)

Planner: opus. Input: REQUEST.md, ACCEPTANCE.md, BRIEF.md, PLAN.md (PM additions). Playbook: fix-defect.md.

## 1. Requirement in my words

With a large diary (about 9000 rows) the app holds hundreds of MB, up to well over 1 GB, while doing
nothing visible. Find what holds that memory and cut it, without changing anything the user sees:
the same rows, totals, paging ("Showing N of M", Show 50/100/500/1000 more, Show all), the 300-row
search cap, saved data, score and income. "Show all" must create only the rows actually on screen.
Growth while idle (live refresh), on period switches and while Reports is hidden must be bounded.

## 2. Method (before numbers)

- Script: `qa/g5_measure.py` (run with `py`, `PYTHONIOENCODING=utf-8`). `build` = fresh scratch root
  `qa/g5_scratch/root` (config: retention 365 days, income + paid tag, one 120-day plan started 60 days
  ago), launch once on the EMPTY database (empty-app figures), kill, then fill 9300 diary rows
  (300 days x 31 rows, 06:00-20:00, seeded random: 25 apps, 2600 distinct page titles, all five
  categories, tags, paid rows tagged studioshoo) plus 300 daily_score rows and 20 completions.
  `large` = launch on the filled root and run steps A-I below. `all` = both. Output: printed table
  and `qa/g5_scratch/g5_<label>_<cmd>.json`. Options: `G5_IDLE_MIN` (default 4), `G5_SETTLE`
  (seconds before each reading, default 6), `G5_PAGES` (distinct pages, default 2600), `G5_SHORT=1`
  (stop after step D).
- App: Debug build, `MENTOR_ROOT=<scratch>`, `MENTOR_INSTANCE_SUFFIX=verify`,
  `PLANILLIUM_AI_BASE_URL=http://127.0.0.1:9/`, started by the script; only that PID is read and killed.
- Figure: **private working set** (working-set pages not marked shared, via `QueryWorkingSet` — Task
  Manager's "Memory" column, the figure the user quoted) plus **private bytes** (`PrivateUsage`, the
  committed figure the OS cannot trim) and the full working set.
- Navigation is by UIA patterns only (no mouse/keys). UIA tree walks themselves make WinUI create
  automation objects for every element passed (a first run that searched the whole tree on every
  period switch reached 1.5 GB / 3.0 GB private bytes), so the script finds the nav items and the
  four period buttons ONCE while the diary is small and reuses them; the rebuilt "All time" box and
  "Show all" link are searched for with only 40 rows on screen. Use the same script unchanged for the
  after-numbers.
- This side's default diary page is 40 rows (`DefaultDiaryRowsShown`), not 50; "diary at 50 rows" in
  ACCEPTANCE line 1 is measured here as the default page (40 of 300 shown, 9300 entries).

### Before numbers (Debug, 2026-10-28, MB)

| Step | Private WS | Private bytes |
|---|---|---|
| Empty DB: Today idle | 84.1 | 118.4 |
| Empty DB: Reports Week | 95.0 | 130.4 |
| Empty DB: Reports +30 s | 93.9 | 130.1 |
| A Large DB: Today idle | 73.1 | 111.7 |
| B Reports Week, diary = today | 90.4 | 133.1 |
| **C All time, default rows (40 of 300 shown, 9300 entries) — ACCEPTANCE 1 figure** | **128.0** | **177.6** |
| D Day / Week / Month / Year (40 rows) | 149.6 / 181.3 / 202.0 / 198.0 | 200.9 / 237.9 / 262.8 / 258.7 |
| E idle on Week, 40 rows, +1 / +2 min | 204.5 / 202.4 | 261.2 / 285.4 |
| F Show all (300 rows built) | 342.0 | 435.9 |
| G Day / Week / Month / Year (show all) | 496.5 / 639.1 / 721.7 / 786.2 | 623.1 / 801.2 / 909.8 / 996.4 |
| G after 12 more period switches, Week | 1427.6 | 2677.5 |
| H idle on Week, show all, +1 / +2 / +3 / +4 min | 1687.7 / 1515.4 / 1314.5 / 1259.6 | 2689.7 / 1929.6 / 2034.4 / 1838.1 |
| I Today (Reports cached, hidden) / +1 min | 932.8 / 310.2 | 1249.6 / 1244.7 |

Raw logs: `qa/g5_scratch/large_before.log`, `build_run.log`; UIA-walk run: `large_run_uiawalk.log`.
Attribution run with 30 distinct pages instead of 2600 (filter lists small): `qa/g5_scratch/attr30.log`
(see section 3, cause 5).

## 3. Causes, ranked by measured impact

1. **Diary rows are built eagerly in a non-virtualizing StackPanel** (`ReportsPage.Diary.cs`,
   `DiaryList`/`BuildRow`/`ShowNext`). Each row is a Grid with a CheckBox, six TextBlocks with
   tooltips and two templated Buttons. Measured: Show all (+260 rows) = +140 MB private WS, about
   **0.54 MB per row**; the 300-row cap means up to ~160 MB of rows per built list, although the
   list's own scroller (MaxHeight 520) shows about 15 rows.
2. **Every period switch rebuilds the whole page, diary included** (`ReportsPage.Render`, called by
   `PeriodBar.SelectionChanged`, pie drill/Back). The diary does not depend on the period, yet its
   rows (300 with the sticky Show all), its filter lists and its whole toolbar are thrown away and
   rebuilt. The dropped WinUI trees are released only when .NET garbage collection runs, which is
   rare because the managed side is small: +80..150 MB per switch, **1.43 GB private WS / 2.68 GB
   private bytes after 16 switches**. It falls slowly afterwards (private bytes 2690 -> 1838 over
   4 idle minutes), so it is late release, not a permanent leak — but the peak is what the user sees.
   With 40 rows the same mechanism gives +20..30 MB per switch (step D).
3. **Live refresh rebuilds unchanged content every 30 s** (`_diaryLiveRefresh` ->
   `RenderDiaryResults`). With "today" or "All time" in scope it re-reads every row of the range
   (9300 records), recomputes the app/page option lists and rebuilds the visible list (all 300 rows
   with Show all) even when nothing was logged. Measured: on idle the figure stays at 1.26..1.69 GB
   instead of returning toward the 342 MB of step F; with 40 rows it holds ~200 MB (step E).
4. **The hidden cached page keeps its whole tree** (`NavigationCacheMode="Enabled"`;
   `OnNavigatedFrom` only stops the timer). Step I: 933 MB private WS right after leaving Reports,
   1245 MB private bytes a minute later (the OS trims the working set of a hidden window, the
   committed memory stays). `OnNavigatedTo` always calls `Render()`, so the kept tree is never reused.
5. **App/Page filter lists are built eagerly for every value in scope** (`RebuildAppCheckboxList`,
   `RebuildPageCheckboxList`, called from every `RenderDiaryResults`): in All time that is one CheckBox
   per distinct page (2600 here; real titles are many and varied, see open item G2-4) even if the
   flyout is never opened. Measured impact (same data, 30 vs 2600 distinct pages, `attr30.log`):
   step C 122.6 vs 128.0 MB, step D 144.9/165.4/185.3/181.6 vs 149.6/181.3/202.0/198.0 MB — about
   **5 MB per build, up to ~17 MB with rebuild churn**: small, ranked last of the real causes. Same code also has a defect found by reading, not seen live:
   `_previousAppsInView`/`_previousPagesInView` are static but the flyout panels are new on every
   full Render, so after a period switch with an unchanged option set the code only re-syncs the
   NEW, EMPTY panel and the App/Page flyout shows no entries (not even "Clear"/"-").
6. **Framework baseline**: an empty app holds 65..95 MB private WS (Today/Reports; two empty runs: 84/95 and 65/66). Large-DB Today
   idle is 73 MB. This is the floor the fixes can approach, not exceed.

**Reports Week**: no Week-specific cost found. Week is the default period, so the first Reports
visit is always Week and carries the first full build; its later readings (D +32 MB, G +143 MB)
match the other periods' rebuild cost. The pie, distractions and tables are small (pie capped at
6 slices + list, top 8 distractions).

## 4. Fix (exact files, approach)

All in `Planillium.App/Pages/`. No change in services, data, score or income code.

### Fix 1 (cause 1) — virtualize the diary list. `ReportsPage.Diary.cs` (`DiaryList`)
- Keep the `list` StackPanel (MinWidth `DiaryListWidth`, Spacing 4) as the card content. Put one
  `ItemsRepeater` in it (`Layout = new StackLayout { Spacing = 4 }`) whose `ItemsSource` is an
  `ObservableCollection<ReportData.DiaryEntry>` holding the rows revealed so far (first `shown`
  entries), and the paging row after the repeater as today.
- `ItemTemplate` = a small private class implementing `Microsoft.UI.Xaml.IElementFactory`:
  `GetElement` returns `BuildRow((DiaryEntry)args.Data)`; `RecycleElement` does nothing (the
  element is dropped). `BuildRow` stays as is (same columns, widths, tooltips, names, handlers).
- `ShowNext(count)` appends the next entries to the collection instead of building rows; `shown`,
  `_diaryRowsShown`, `_diaryShowAll`, "Showing N of M" and the increments stay exactly as now.
- The repeater realizes rows only for the diary scroller's viewport (MaxHeight 520) plus its small
  cache, so "Show all" creates only the rows on screen; scrolling creates the next ones.
- Rejected: `ListView` — adds selection/hover chrome, item padding and its own scroller inside the
  existing one (visible change, nested scrolling). Rejected: capping "Show all" — forbidden (PLAN.md).

### Fix 2 (cause 2) — period switch rebuilds only the period sections. `ReportsPage.xaml`, `ReportsPage.xaml.cs`, `ReportsPage.TimeByApp.cs`
- XAML: add `<StackPanel x:Name="DiaryBody" Spacing="0"/>` directly after `Body` in `ContentColumn`
  (same spacing, so the layout is identical).
- Split `Render()` into `RenderPeriodSections()` (everything now added to `Body` above the diary:
  summary table, distractions, time by app, insights; `ExportCsvItem` text; `SaveErrorBar` close) and
  `RenderDiary()` (clears `DiaryBody`, `BuildDiarySection(today, score)` adds to `DiaryBody`).
  `Render()` = both, unchanged for every existing caller (navigation, day change, edit/split, all
  time, clear filters, date nav).
- Call `RenderPeriodSections()` alone from `PeriodBar.SelectionChanged` and from the pie drill
  handlers (wedge Tapped, row Tapped/KeyDown, Back) in `ReportsPage.TimeByApp.cs`; Back's focus
  restore keeps using `_appDrillPieList` after that call.
- Each part opens its own `Database`/`ScoreService` (as `Render` does now); the error path keeps the
  "Couldn't load your report data" text in the part that failed.
- In `BuildDiarySection` and its helpers, every `Body.Children.Add` becomes `DiaryBody.Children.Add`.

### Fix 3 (cause 3) — skip a live refresh that would change nothing. `ReportsPage.Diary.cs`
- Keep the rows last loaded by `RenderDiaryResults` (`List<DiaryEntry>`, local to
  `BuildDiarySection`). The live-refresh action loads the rows for the same scope and, if they are
  equal to the last loaded ones (`SequenceEqual`; `DiaryEntry` is a record, so this compares every
  field), returns without touching the UI. Otherwise it runs `RenderDiaryResults` with those rows
  (pass them in; do not query twice). Search typing, filters, select all and marks are unchanged
  (they always render).

### Fix 4 (cause 4) — release the hidden page's tree. `ReportsPage.xaml.cs`
- `OnNavigatedFrom`: save `RootScroller.VerticalOffset`; stop both timers (refresh and search
  debounce); set `_diaryLiveRefreshAction` and `_diarySearchDebounceAction` to null; clear
  `Body` and `DiaryBody`; set `_appDrillPieList = null`.
- `OnNavigatedTo`: `Render()` as now, then restore the saved offset after layout
  (`RootScroller.UpdateLayout(); RootScroller.ChangeView(null, offset, null, true)`), so coming back
  lands where the user left (today the kept tree preserves the offset; clearing it must not lose it).
- Do not touch the cached-page flag itself (the static state and timers rely on the one instance).

### Fix 5 (cause 5) — build App/Page filter lists only when opened. `ReportsPage.Diary.cs`
- `RenderDiaryResults` keeps computing `appsInView`/`pagesInView`, pruning the filter sets and
  setting the button labels as now, and stores the two lists in locals of `BuildDiarySection`.
- The App and Page flyouts fill their panel on `Flyout.Opening` from the stored list
  (`RebuildAppCheckboxList`/`RebuildPageCheckboxList` unchanged) and empty it on `Flyout.Closed`.
  If `RenderDiaryResults` runs while one is open (a checkbox click re-renders), rebuild that open
  panel only if its option set changed, else re-sync `IsChecked` (today's rule), keyed on what the
  open panel holds — not on the static `_previousAppsInView`/`_previousPagesInView`, which go.
  This also removes the empty-flyout defect of cause 5.
- Category and Tag lists are fixed and small (7 entries): unchanged.

### Not in this change
- No forced garbage collection. If the after-numbers still climb on period switches or idle, report
  it (open question 2) rather than adding `GC.Collect`.
- No change to retention, the 300-row cap, paging increments, `DefaultDiaryRowsShown`, Today,
  Schedule, Plans or Settings pages.

## 5. Edge cases (the coder checks each by reading; the after-run covers the memory ones)

- Show more / Show all after a live refresh: count and "Showing N of M" kept (same rules as now).
- Select all then scroll: rows realized later show checked (BuildRow reads `selectedIds`).
- Mark on/off/neutral/tag with rows not realized: uses `lastRows`/`selectedIds`, not UI rows — unchanged.
- Edit/Split from a realized row: still `Render()` (both parts).
- 0 rows, 1 row, exactly 40, 41, 300 (cap message "N matches — showing the 300 most recent"): same text.
- Keyboard: Tab through rows reaches rows below the viewport (ItemsRepeater brings them into view).
- Horizontal scroll and width: rows keep `HorizontalAlignment.Left` and fixed columns; list keeps
  MinWidth; Card keeps `MinWidth = DiaryCardWidth`; Edit/Split still reachable at 900 px window.
- Day change while Reports is open: MainWindow calls `Render()` (both parts) — unchanged.
- Navigate away while a filter flyout is open: closes; panel emptied.
- Pie drill then period switch: drill path reset as now; diary untouched.
- Theme switch while hidden: page is rebuilt on return, brushes from resources as now.
- QA note: rows below the diary viewport are not in the UIA tree until scrolled into view.

## 6. Constraints

Nothing visible changes: same rows, same order, same columns, same totals and subtotal line, same
paging row and counts, same "Showing N of M", Show all reveals every row (created as they come on
screen), 300-row cap unchanged; no data, score or income change. Strict build and existing suite
green: `dotnet build -p:Platform=x64 -c Debug -warnaserror --no-incremental` from `Planillium.App/`,
then `dotnet test --no-build` from `Planillium.App.Tests/`. After-numbers: `py qa/g5_measure.py all after`
(same steps, same data seed), table beside the before table.

## 7. Acceptance (verbatim from ACCEPTANCE.md)

1. Measured on a scratch root with a large diary (about 9000+ rows, built by the agent in its own scratchpad, never real data): private working set after opening Reports with the diary at 50 rows, before and after the change, both numbers reported.
2. After the change the figure is clearly lower, or the report states with evidence why the remainder is the framework baseline (empty-app figure also measured).
3. Nothing visible changes: same rows, same totals, same paging; strict build and existing suite green.
4. Cache/lists that grow with history are bounded or released (idle page, cached page, switching periods).

Planner's lines (mine, not the PM's):
- P1. After-run step F (Show all) is within ~40 MB of step C; steps G/H no longer climb per switch/minute.
- P2. Step I (Reports hidden) private bytes close to step A plus the static state, not the 1.2 GB before.

## 8. Open questions

1. ACCEPTANCE line 1 says "diary at 50 rows"; this side's default page is 40 rows. Measured at the
   default (40). Change nothing unless the user wants a 50-row step added.
2. If, after fixes 1-5, period switching or idle still climbs (late release of dropped trees), may
   the app request a garbage collection after a full Reports rebuild? Not specified; not done.
3. Cause 5's empty App/Page flyout after a period switch (found by reading) is fixed as a side effect
   of fix 5. It is a visible change (the list appears again). Assumed wanted, since it restores the
   intended behaviour; flag to the user.

## 9. After numbers (coder, 2026-10-28)

Same script and data (`py qa/g5_measure.py all after`, Debug, scratch root only; log `qa/g5_scratch/large_after.log`, `g5_after_all.json`). MB.

| Step | Private WS before -> after | Private bytes before -> after |
|---|---|---|
| Empty DB: Today idle | 84.1 -> 83.5 | 118.4 -> 117.4 |
| Empty DB: Reports Week | 95.0 -> 87.1 | 130.4 -> 120.9 |
| A Large DB: Today idle | 73.1 -> 71.0 | 111.7 -> 104.2 |
| B Reports Week, diary = today | 90.4 -> 79.6 | 133.1 -> 115.0 |
| **C All time, 40 of 300 shown (ACCEPTANCE 1)** | **128.0 -> 87.2** | **177.6 -> 127.2** |
| D Day / Week / Month / Year | 149.6/181.3/202.0/198.0 -> 79.2/71.6/73.2/69.7 | 200.9/237.9/262.8/258.7 -> 127.3/127.9/130.7/129.7 |
| E idle Week +1 / +2 min | 204.5/202.4 -> 70.6/67.4 | 261.2/285.4 -> 135.1/139.3 |
| F Show all | 342.0 -> 70.2 | 435.9 -> 134.4 |
| G Day / Week / Month / Year (show all) | 496.5/639.1/721.7/786.2 -> 71.6/73.4/76.0/81.0 | 623.1/801.2/909.8/996.4 -> 135.7/136.6/139.2/143.4 |
| G after 12 more switches, Week | 1427.6 -> 80.4 | 2677.5 -> 141.6 |
| H idle +1/+2/+3/+4 min | 1687.7/1515.4/1314.5/1259.6 -> 77.0/61.5/66.2/72.4 | 2689.7/1929.6/2034.4/1838.1 -> 138.7/139.5/143.3/151.1 |
| I Today (Reports hidden) / +1 min | 932.8/310.2 -> 101.0/85.6 | 1249.6/1244.7 -> 158.2/141.1 |

P1 met (F is below C; G/H flat). P2 met (I private bytes 141 vs A 104). Remainder: Reports with the large DB sits within ~10-15 MB of the empty-app figures (83-87 MB), i.e. the framework baseline. Private bytes still creep ~24 MB over the 15-minute run (127 -> 151), small and not investigated further (no forced GC, lead ruling 2).
Deviation from section 4 fix 1: `RecycleElement` removes the element from the repeater (ItemsRepeater leaves recycling to a custom factory; doing nothing would keep every row ever realized as a child).

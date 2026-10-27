# SPEC -- B1i Reports income columns (+ agentic item A2: Time-by-app chevrons and slice colours)

Agentic side only. Target `agentic/winui-agentic/`. No source edits in this step. Builders do not write tests.
Feasibility check (brief: "stop if the service cannot give per-day values"): **it can.** The period figure is a plain
sum of per-day parts, shown below; no new calculation rule is needed.

## 1. Requirement (my words)

Reports, all four periods:
1. The summary table gets two EUR columns after Score: **"Income lost/gained"** and **"Of which paid work"**.
   Day/Week: one row per day; Month: one row per week; Year: one row per month. The Total row shows the period figure.
2. The Score card and the "Income lost or gained / Of which paid work" strip are removed. The table section is the top
   of the page body.
3. Total = what the removed strip showed; rows add up to the Total to the cent.
4. Same colours (red if negative, green if zero/positive) and the same EUR format (`MainWindow.FormatEur`).
Unchanged: how income, credit, paid work and score are calculated; stored data; other sections; exports.

A2: in "Time by app" the list beside the pie (the pie's legend) shows its chevron on rows you can open, and the pie
slices get distinct colours instead of grey shades.

## 2. Findings that the design rests on (verified in code)

- **Period figure** (`IncomeService.SumForPeriodWithCredit`, `Services/IncomeService.cs:217`), with
  `start = ReportData.PeriodStart(period, today)`:
  `Net = posted(start..yesterday) + credit(start..yesterday) + TodayPreview() + TodayCredit(score) + paid(start..today)`.
  `posted` is a SQL SUM of `income_ledger.delta_eur` (one row per date, `UNIQUE(date)`); `CreditRange` is already a
  loop of independent per-day terms; `PaidEarnings` is minutes x hourly rate per tag (linear in minutes). So the
  per-day value is exactly:
  - day `d < today`: `posted(d) + CreditRange(d, d) + paid(d)`
  - `d == today`: `TodayPreview() + TodayCredit(score) + paid(today)`
  and the sum over `start..today` equals `Net` up to floating-point addition order (~1e-12). The "creditStart quirk"
  is not a quirk: when `start == today` the credit range is empty and today's credit comes from `TodayCredit`, which
  the per-day form reproduces. The `LedgerStart` (2025-12-04) clamp in `PaidEarnings` is reproduced by clamping the
  per-day paid read to the same date.
- **Paid column total** = `PaidEarningsForPeriod(period).Sum(e => e.Eur)` = sum of per-day paid (same tag list, same
  date clamp). The old strip showed `FormatEur(paidWork)` (unrounded); net showed `FormatEur(Math.Round(incomeNet, 2))`.
- **Rows vs dates.** `ReportData.DailyRows` walks every date `start..today` once. Week rows = `MondayOf(today)..today`
  (same as `PeriodStart(Week)`); Day = today only. Month buckets group those dates by `MondayOf(date)` and start at
  the 1st (the first week row's label may read "29 Sep - 05 Oct" but it only holds days from the 1st; pre-existing,
  and the income for that row covers the same days as its minutes). Year buckets group by calendar month from 1 Jan.
  So every bucket is a contiguous date run, and all buckets together cover exactly the period.
- **Year drops months** with no minutes, no score and no planned tasks (`Bucket(..., keepEmpty: false)`). Such a month
  still has ledger income (the ledger posts every day), so dropping it would break "rows add up to the Total". The
  page must keep a month whose income or paid value is non-zero.
- `BucketStat(Label, Minutes, Score, Done, Total)` carries no dates; it is constructed only in `ReportData.Bucket`.
- `MonthBuckets`/`YearBuckets`/`WeekStats` are also used by `ReportExport.ExportCsv` (and `WeekStats` by the HTML
  export), so their default output must not change.

## 3. Approach

### 3.1 Per-day values (service)
Add to `IncomeService` one method:
```
/// Per-day parts of SumForPeriodWithCredit's Net (Paid included) and of PaidEarnings, from..today inclusive.
public Dictionary<DateOnly, (double Net, double Paid)> DailyIncome(DateOnly from, ScoreService score)
```
- `today = DateOnly.FromDateTime(DateTime.Today)`, `yesterday = today - 1` (same as its siblings).
- Posted: one query `SELECT date, delta_eur FROM income_ledger WHERE date >= $from AND date <= $yesterday`
  (`yyyy-MM-dd`, like `SumPostedRange`); missing date = 0.
- Paid: one query `SELECT date, tag, SUM(duration_min) FROM time_diary WHERE date >= $pf AND date <= $today GROUP BY date, tag`
  with `$pf = max(from, LedgerStart)`; then for **each entry of `PaidTags.All()` in order (duplicates included, exactly
  as `PaidEarnings` loops them)** add `new PaidEarning(tag, minutes[date, tag.Value]).Eur` (ordinal string match, as
  SQL `tag=$t`). Pricing stays in `PaidEarning.Eur`; no second formula.
- For each `d` in `from..today`: `Net = d < today ? posted[d] + CreditRange(d, d, score) : TodayPreview() + TodayCredit(score)`,
  then `Net += Paid`. Store `(Net, Paid)`.
- `SumForPeriodWithCredit`, `PaidEarnings`, `CreditRange` and every other method stay untouched.

### 3.2 Rounding to the cent (pure helper, new file `Services/EurCents.cs`)
```
internal static class EurCents
{
    /// Whole cents exactly as EurFormatter shows v ("F2" and "N2" round the same way).
    internal static long Of(double v) =>
        (long)(decimal.Parse(v.ToString("F2", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture) * 100m);

    /// Row values as whole cents that add up exactly to totalCents: running-total rounding.
    /// cum_i = Of(v_0 + ... + v_i) for i < n-1; cum_(n-1) = totalCents; row_i = cum_i - cum_(i-1) (cum_-1 = 0).
    internal static long[] SplitRows(IReadOnlyList<double> rowValues, long totalCents)
}
```
- Totals (anchors), identical to what the strip displayed: `netTotalCents = EurCents.Of(Math.Round(incomeNet, 2))`,
  `paidTotalCents = EurCents.Of(paidSum)` where `paidSum = income.PaidEarningsForPeriod(_period).Sum(e => e.Eur)`.
- Every money cell (rows and Total) is shown as `MainWindow.FormatEur(cents / 100.0)` with
  `IncomeBrush(cents / 100.0)`. Integer cents mean a cell is never "-€0.00".
- Each row differs from its own exact value by under one cent; the last row absorbs the float-order residue. Rule
  (mine, for the record): *rows are rounded by running total so they always add up to the Total shown.*

### 3.3 Buckets get their date range
- `ReportData.BucketStat` gains two trailing positional fields `DateOnly From, DateOnly To`, and a property
  `public bool HasActivity => Minutes.TotalMin > 0 || Score != 0 || Total > 0;`.
- `Bucket(...)`: new bucket starts with `From = To = r.Date`; each fold sets `To = r.Date` (dates arrive ascending).
  Its filter becomes `keepEmpty || b.HasActivity` (same predicate, one home).
- `YearBuckets(SqliteConnection conn, ScoreService score, bool keepEmpty = false)` passes `keepEmpty` through.
  Default false, so `ReportExport.ExportCsv` output is unchanged. `MonthBuckets` unchanged (already keeps all weeks).

### 3.4 Page
`Pages/ReportsPage.xaml.cs` `Render()`:
- Delete `Body.Children.Add(Card(ScoreCard(...)))` and `Body.Children.Add(Card(IncomeSummaryStrip(...)))`.
- Keep `var (incomeNet, _) = income.SumForPeriodWithCredit(_period, score);` (also feeds `DistractionList`).
- Add: `var paidSum = income.PaidEarningsForPeriod(_period).Sum(e => e.Eur);`
  `var daily = income.DailyIncome(ReportData.PeriodStart(_period, today), score);`
  and the two total-cent anchors from 3.2.
- Day/Week: `DayTable(weekStats, daily, netTotalCents, paidTotalCents)`.
- Month: `MonthBuckets(...)`. Year: `YearBuckets(db.Conn, score, keepEmpty: true)` then keep
  `b.HasActivity || RowNet(b) != 0 || RowPaid(b) != 0` (raw doubles; `RowNet(b)` = sum of `daily[d].Net` for
  `d` in `b.From..b.To`). Then `BucketTable(buckets, daily, netTotalCents, paidTotalCents)`. The
  `buckets.Count == 0 ? Dim("No activity logged yet.")` branch stays, applied after this filter.
- Lookups use the dictionary indexer (a missing date throws into Render's existing catch rather than silently
  reading 0).
- Section heading `Section(periodName)` stays as the first element (see Open question 2).

`Pages/ReportsPage.Tables.cs`:
- Both tables: `columns = 6 + CategoryColumns.Length`; header adds `"Income lost/gained", "Of which paid work"` after
  `"Score"`. Name the indices once: `totalCol = 2 + C`, `scoreCol = 3 + C`, `netCol = 4 + C`, `paidCol = 5 + C`
  (`C = CategoryColumns.Length`).
- **The Score colour test `c == cells.Length - 1` must become `c == scoreCol`** (otherwise the Score colour moves to
  the paid column). Same in `BucketTable`.
- Row cells for the two new columns: text `FormatEur(rowCents/100.0)`, foreground `IncomeBrush(rowCents/100.0)`;
  DayTable keeps SemiBold for today's row on these cells too. Row cents from `EurCents.SplitRows` over the rows in
  display order (Day/Week: `daily[s.Date]`; buckets: `RowNet(b)`/`RowPaid(b)`).
- `AddTotalsRow(Grid grid, int columnCount, params (string Text, int Column, Brush? Foreground)[] cells)`; `Cell`
  sets `Foreground` when non-null. Existing tuples pass `null` (inside the `Select` spreads: `(…, i + 2, (Brush?)null)`).
  Existing totals move to `totalCol` / `scoreCol` (were `columns - 2` / `columns - 1`). New totals:
  `(FormatEur(netTotalCents/100.0), netCol, IncomeBrush(netTotalCents/100.0))`, same for paid.
- DayTable Total row guard `rows.Count > 1` is removed so Day (and Week on a Monday) also get a Total row (Open
  question 1).
- `Scrollable(...)` wrapper already present on both tables; nothing else needed for narrow windows.

### 3.5 Dead code after removing the two cards (grep done)
- `ReportsPage.xaml.cs`: delete `ScoreCard(...)` (only caller was Render). `ScoreBrush` stays (tables).
  `Microsoft.UI.Text` stays (`InsightsPanel` uses `FontWeights`). `totals` stays (`InsightsPanel`). Update the file
  header comment ("score/drift/insights cards" -> no score card).
- `ReportsPage.Income.cs`: delete `IncomeStripStackWidth`, `IncomeSummaryStrip`, `SummaryColumn`. Keep
  `SignedHourValue` (used by `DistractionList`). Remove the now-unused `using` lines (Text, Xaml, Controls, Media,
  Shapes) and fix the file comment.
- Kept on purpose (still used, or used by tests the builder must not edit): `PaidEarningsForPeriod`,
  `SumForPeriodWithCredit` (credit discarded with `_`), `ReportData.PeriodTotals` fields `Score/Done/Total/DayOffs`
  (tests in `ReportPeriodStatsTests`), `PieSlices.Slice.DominantCategory` (tests in `PieSlicesTests`).
  `IncomeService.SumForPeriod` has no caller already before this change: pre-existing, out of scope, left alone.
- Build must stay `-warnaserror` clean (TreatWarningsAsErrors is on in the csproj).

### Rejected alternatives
- **Per-day calls of the existing range methods** (`SumPostedRange(d,d)`, `PaidEarnings(d,d)` for every day): no new
  SQL, but on Year that is about 365 x (1 + number of paid tags) queries, each `PaidEarnings` scanning `time_diary`
  per tag; Reports has a slow-load history (ReportData comments, 2026-07-21). Two grouped reads give the same values.
- **Total = sum of the rows** (drop the anchor): can differ by a cent from the old strip (ACCEPTANCE 4) and
  `incomeNet` is still needed by Top distractions anyway.
- **Page maps dates to buckets itself** (`MondayOf` / `yyyy-MM`): a second copy of the bucket keys that can drift;
  `From/To` on `BucketStat` keeps one home.
- **Change `YearBuckets` default to keep income-only months**: would change the CSV export (ACCEPTANCE 8).

## 4. Files to change
| File | Change |
|---|---|
| `Planillium.App/Services/IncomeService.cs` | add `DailyIncome(from, score)` |
| `Planillium.App/Services/EurCents.cs` | new: `Of`, `SplitRows` (pure, testable by source link) |
| `Planillium.App/Services/ReportData.cs` | `BucketStat` + `From`, `To`, `HasActivity`; `Bucket` fills them; `YearBuckets(..., keepEmpty = false)` |
| `Planillium.App/Pages/ReportsPage.xaml.cs` | remove two card lines and `ScoreCard`; compute daily values and anchors; Year filter; pass to tables |
| `Planillium.App/Pages/ReportsPage.Tables.cs` | two columns, named indices, Score colour index, `AddTotalsRow` with brush, Day total row |
| `Planillium.App/Pages/ReportsPage.Income.cs` | delete strip builders and unused usings; keep `SignedHourValue` |
| `Planillium.App/Pages/ReportsPage.TimeByApp.cs` | A2 (section 6) |
| Docs (same pass): `CONTEXT.md` §2 + §5 item 1, `context/todos.md`, `CHANGELOG.md` | record B1i and A2 |

No XAML change. No test file change (QA owns tests).

## 5. Edge cases
- **Day**: one row = today; with the Total row added, both show the strip's value (SplitRows with n = 1 returns the anchor).
- **Week on Monday**: one row + Total row (guard removed). Other days: Monday..today.
- **Month on the 1st**: one week row. First week row may be labelled from a Monday before the 1st; its income covers
  only days from the 1st (same as its minutes).
- **Year**: months with no activity but non-zero income are now shown (otherwise rows would not add up). On a fresh
  scratch DB with a monthly income configured that means every month Jan..now appears; with monthly income 0 and
  no activity, nothing changes and "No activity logged yet." still shows.
- **Day off**: income still counts on that row (ledger posts every day; rule unchanged); minutes stay 0 as before.
- **Monthly income 0 / not set**: every value 0 -> "€0.00", green.
- **Employment toggle flipped mid-period**: past days keep their posted sign; today follows the current toggle;
  per-row signs can differ, each coloured by its own sign; Total coloured by the Total's sign (rule 14).
- **No paid tags / no tagged time**: paid column all "€0.00" (the old "No time tagged..." note goes with the strip).
- **Duplicate tag entries in config**: counted per entry, exactly like `PaidEarnings`.
- **Ledger not yet caught up** (e.g. page re-renders after midnight before `EnsureIncomeCaughtUp`): the missing day
  reads 0 in both the period figure and the row, so rows still add up.
- **Midnight between the two service calls**: pre-existing race (siblings read `DateTime.Today` independently); not addressed.
- **Negative paid**: impossible (rates >= 0, minutes >= 0).
- **Narrow window (900 DIP)**: table grows by two Auto columns; the existing horizontal `Scrollable` handles it.

## 6. Item A2 -- Time by app: chevrons and slice colours

### Findings
- **Chevrons**: `AddPieListRow` (`ReportsPage.TimeByApp.cs:439-446`) creates the chevron `FontIcon` with
  `Glyph = ""` -- an **empty string** (checked by code point: the other glyphs in the project are ``, ``,
  ``, `/E` and survive; this one is empty since at least commit cd77ff5). The column is reserved but
  nothing is drawn, so drillable rows show no chevron. Sibling sweep: no other empty `Glyph` in `.cs` or `.xaml`.
- **Grey slices**: `SliceBrush` colours a slice by its dominant category (`CategoryStyle.BrushKey`) and steps each
  further same-category slice lighter (opacity). Most apps are Neutral -> `TextFillColorSecondaryBrush` (grey), so the
  pie is grey shades; only the "unaccounted time" entry (idle placeholder, Idle -> caution colour) is coloured.
  This matches CONTEXT §5 item 1 (2026-10-02): "if per-category shading is wrong, the alternative is a distinct colour
  per app" -- the user has now chosen that alternative.

### Interpretation (minimal)
- "Legend" = the list beside the pie (swatch, name, hours, %, bar, chevron). The category key above the pie
  (`TimeByAppLegend`) is not drillable and gets no chevron.
- Chevron: `Glyph = ""` (Segoe Fluent Icons ChevronRight, the same glyph as the Diary's next-day button),
  written as a C# escape, never a literal private-use character. Shown on drillable rows only (existing rule).
  Wedges get no chevron.
- Slice colour: by the slice's position in `slices`, from a fixed 8-colour palette (theme-independent, mid tones,
  no pure red/green/yellow so they are not read as off-plan/on-plan/idle):
  `#4E79A7` blue, `#F28E2B` orange, `#76B7B2` teal, `#B07AA1` purple, `#FF9DA7` pink, `#9C755F` brown,
  `#A0CBE8` light blue, `#FFBE7D` light orange (`Microsoft.UI.ColorHelper.FromArgb(255, r, g, b)`), index modulo 8.
  Max needed: 6 apps + "Other" = 7 wedges; at a drilled level plus the "(no detail)" list row = 8.
  "(no detail)" (list row only, not drawn as a wedge) takes palette index `slices.Count`.
- Wedge fill and list swatch keep calling the same `SliceBrush`, so a swatch matches its wedge.
- Category meaning stays discoverable: each list row's stacked bar keeps the category colours and the category key
  above stays unchanged; the accessible name still lists the category breakdown.
- `SliceBrush` body replaced (opacity-step logic and its comment removed). Update the `TimeByAppLegend` doc comment
  ("Color key for pie slices and stacked bars" -> stacked bars) and the Render comment above it.
- No change to `PieSlices` (DominantCategory kept: tests use it).

### A2 edge cases
- Single 100 % slice (full circle): blue.
- Drilled level, Back button: focus restore reads row child index 1 (the name) -- untouched by this change.
- Light and dark theme: fixed colours on the card background with the existing 2-DIP card-colour stroke between wedges.

## 7. Rules that applied (agentic DECISIONS / LESSONS) and how they are covered
- **DECISIONS rule 14 (income counter)**: daily rate, ledger posting, credit and paid work are reused unchanged
  (3.1). Reports' figure includes today's live preview -> today's row uses `TodayPreview + TodayCredit`. Colour
  follows each figure's own computed sign, never the toggle (`IncomeBrush`). "Name the exact timerange": the card
  captions named it; with them gone the section heading ("TODAY"/"THIS WEEK"/...) and the row labels name it, which
  is why the heading is kept.
- **Standing lesson "check SQL column names against the real schema"**: the two new queries use only columns
  already read by existing queries in the same file (`income_ledger.date/delta_eur`, `time_diary.date/tag/duration_min`).
- **LESSONS "after removing a card, discard unused tuple values with `_`; grep all callers before deleting a shared
  helper"**: done in 3.5.
- **LESSONS "card captions are built in C# in upper case"**: searched case-insensitively; the only builders are the
  two removed ones; no other page renders them.
- **Check every sibling**: both tables (Day/Week and Month/Year), all four periods, the Total row, the Year filter,
  CSV/HTML exports, every `Glyph` in the project.

## 8. Sibling check -- exports (ACCEPTANCE 8)
- **CSV** (`ReportExport.ExportCsv`): summary rows (Date or Period, Tasks done, Tasks total, five category minutes,
  Total (min), Score), then Top distractions (Application, Minutes), then Time by app (Application, Sub-item,
  On/Off/Neutral/Total min). It never exported the Score card or the income strip. Unchanged by this spec
  (`YearBuckets` default kept; new `BucketStat` fields not written).
- **HTML week report** (`ExportWeek`): week table, distractions, time by app, suggestions; no cards. Unchanged.
- No other page builds the removed cards (grep: `ScoreCard`, `IncomeSummaryStrip`, "SCORE EARNED", "OF WHICH PAID",
  "INCOME LOST").

## 9. Acceptance (verbatim from ACCEPTANCE.md)
1. For each of Day / Week / Month / Year the summary table has columns "Income lost/gained" and "Of which paid work"
   after the existing columns; every row and the Total row has a value in both.
2. Row meaning: Day and Week = per day; Month = per week; Year = per month.
3. The rows of each column add up (to the cent) to that column's Total row.
4. Total-row values equal the figures the removed cards showed for the same period on the pre-change build.
5. Sign and colour as on the removed cards (negative loss colour, zero/paid colour); EUR format unchanged.
6. The Score card and the income card row are gone on all four periods; the table follows the period selector
   directly, no gap or leftover heading.
7. The table still fits or scrolls horizontally at a narrow window (no silent clipping).
8. Sibling check: no other page/view still renders the removed cards; CSV/Export content unchanged unless it
   already exported those cards (then report it).
9. Build clean (/warnaserror); existing tests pass; QA runs standing checks (app-qa-checks).

**Mine (not in ACCEPTANCE.md, for A2):**
- A2-1 (mine): every drillable row in the Time-by-app list shows a right chevron; non-drillable rows show none.
- A2-2 (mine): pie wedges at one level all have different colours, each matching its list swatch; the category key
  and the stacked bars keep their category colours.
- A2-3 (mine): Year shows a month with income but no activity, and its income is in the rows that add up to the Total.

## 10. Open questions (defaults chosen, PM to confirm)
1. **Total row on Day (and Week on a Monday).** Today the table hides the Total row when it has one row.
   ACCEPTANCE 1 asks for a Total row in every period; REQUEST says change nothing else. Default (resolved against
   ACCEPTANCE, per playbook): remove the guard, so a one-row table also shows a Total row (it repeats the row).
2. **Section heading above the table** ("TODAY"/"THIS WEEK"/...). ACCEPTANCE 6 says "no gap or leftover heading".
   Default: keep it -- it is the table's own heading from before B1, not a card leftover, and after the cards go it
   is the only text naming the timerange (DECISIONS rule 14). It keeps the standard section spacing (22 DIP top).
   Alternative: drop it so the table card sits right under the selector.
3. **Lost from the page with the Score card**: the "N days off marked" count and the period's on/off-plan hours line.
   Tasks, hours and score remain in the table's Total row; the day-off count appears nowhere else for Month/Year
   (Day/Week rows still say "Day off"). The user asked for the card's removal, so it goes; flagged once.
4. **Year months now shown because of income** (see 5): may surprise on a near-empty install. Required for rows to add up.
5. **A2 interpretation** (section 6): chevrons in the pie's list, palette colours per slice. If "legend" meant the
   category key above the pie, the user needs to say what a chevron there should do.

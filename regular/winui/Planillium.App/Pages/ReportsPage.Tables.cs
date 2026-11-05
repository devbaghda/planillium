using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// The day/bucket summary tables — see ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    private static Grid DayTable(List<ReportData.DayStat> weekStats,
        IReadOnlyDictionary<DateOnly, (double Income, double Paid, int PaidMin)> daily)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = _period == ReportPeriod.Day
            ? weekStats.Where(s => s.Date == today).ToList()
            : weekStats;

        // Label column and gap come from the page-wide alignment grid, so this table's first
        // figure lands on the same x as the bars in the two sections below it. Wider than this
        // column strictly needs for a date — that is the trade the shared axis asks for, and the
        // bar rows make the same one with labels as short as "shower".
        //
        // Columns: Day, Tasks, then one per category (2026-08-05 request), then the row's own
        // Total and Score. Score stays last, where it has always been.
        var columns = 4 + MoneyHeaders.Length + CategoryColumns.Length;
        var grid = new Grid { ColumnSpacing = ColumnGap, RowSpacing = 6 };
        for (var c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c == 0 ? new GridLength(TableLabelWidth) : GridLength.Auto });
        AddHeaderRow(grid, ["Day", "Tasks",
            .. CategoryColumns.Select(c => c.Label), "Total", "Score", .. MoneyHeaders]);

        foreach (var s in rows)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition());
            var isToday = s.Date == today;
            daily.TryGetValue(s.Date, out var raw);
            var money = MoneyOf(raw.Income, raw.Paid, raw.PaidMin);
            // A day off correctly zeroes on/off-plan minutes here (same rule the score
            // uses), but showing that as a bare "0m/0m" reads identically to a tracking
            // failure or a genuinely idle day everywhere else in the app labels a day off
            // explicitly (2026-07-18 audit finding R8-07) — so this table needs its own
            // label since it's the one place that didn't have one.
            string[] cells =
            [
                s.Date.ToDisplayDate() + (s.IsDayOff ? "\nDay off" : ""),
                $"{s.Done}/{s.Total}",
                .. CategoryColumns.Select(c => ReportData.FmtHours(s.Minutes.Of(c.Category))),
                ReportData.FmtHours(s.Minutes.TotalMin),
                s.Score.ToString(),
                .. money.Cells,
            ];
            for (var c = 0; c < cells.Length; c++)
            {
                var tb = new TextBlock
                {
                    Text = cells[c],
                    FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
                    TextWrapping = c == 0 ? TextWrapping.Wrap : TextWrapping.NoWrap,
                };
                if (c == columns - 5)
                    tb.Foreground = ScoreBrush(s.Score);
                else if (c >= columns - MoneyHeaders.Length)
                    tb.Foreground = MoneyBrush(c - (columns - MoneyHeaders.Length), money.Balance, money.Unearned);
                else if (!isToday)
                    tb.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                Grid.SetColumn(tb, c); Grid.SetRow(tb, row);
                grid.Children.Add(tb);
            }
        }

        // Totals under every category column and under the row-total column (2026-08-04 request,
        // extended to all five categories 2026-08-05). Sums the values as
        // displayed, which keeps it consistent with business rule 10 for free: a day where
        // every plan is off already contributes 0/0 to those two cells, so it can't inflate
        // the total here while being excluded everywhere else in Reports. Skipped for the
        // single-row Day period, where a "total" of one row is just noise.
        // Tasks and Score are summed too (2026-08-05 request: "per row summary should contain
        // score summary"). Both were deliberately blank before, on the reasoning that a summed
        // "12/14" reads as a ratio it isn't and that a period score sitting one column from the
        // sidebar's running balance invites confusing the two. The user overrode that, and the
        // score total is the more useful figure — it's the same number the card above shows,
        // which is now guaranteed rather than hoped for: both fold ReportData.DailyRows.
        if (rows.Count > 1)
        {
            var periodTotals = SumOfShown(rows.Select(s => daily.GetValueOrDefault(s.Date)));
            var m = MoneyOf(periodTotals.Income, periodTotals.Paid, periodTotals.PaidMin);
            AddTotalsRow(grid, columns,
                [($"{rows.Sum(s => s.Done)}/{rows.Sum(s => s.Total)}", 1, null),
                 .. CategoryColumns.Select((c, i) =>
                     (ReportData.FmtHours(rows.Sum(s => s.Minutes.Of(c.Category))), i + 2, (Brush?)null)),
                 (ReportData.FmtHours(rows.Sum(s => s.Minutes.TotalMin)), columns - 6, null),
                 (rows.Sum(s => s.Score).ToString(), columns - 5, null),
                 .. MoneyTotals(m, columns)]);
        }
        return grid;
    }

    /// <summary>G11 (2026-10-30) rule: every EUR cell in these tables shows the figure rounded to the cent, and the
    /// Total row is the sum of those shown cents, so the rows add up to the Total exactly. (The Total used to be the
    /// unrounded period figure, which drifted a cent from the rows once several rows carried fractions of a cent.)</summary>
    private static double Cents(double v) => ReportData.Cents(v);

    private static (double Income, double Paid, int PaidMin) SumOfShown(IEnumerable<(double Income, double Paid, int PaidMin)> rows)
    {
        decimal income = 0, paid = 0;
        var min = 0;
        foreach (var (i, p, pm) in rows) { income += (decimal)Cents(i); paid += (decimal)Cents(p); min += pm; }
        return ((double)income, (double)paid, min);
    }

    /// <summary>The category columns both summary tables carry — the shared
    /// <see cref="DiaryCategory.ReportOrder"/> the bars, the legend and the exports also read, so
    /// no two parts of a report can disagree about which categories exist, what they're called or
    /// what order they come in (2026-08-05 request, "add all the categories").</summary>
    private static readonly (string Category, string Label)[] CategoryColumns =
        [.. DiaryCategory.ReportOrder.Select(o => (o.Value, o.Label))];

    /// <summary>Shared footer for both summary tables: a hairline separator, then a bold
    /// "Total" label and whichever column totals the caller supplies (by column index — the
    /// two tables have different column layouts, and only some columns are summable at all).
    /// One implementation so the two can't drift apart in weight, spacing or label.</summary>
    private static void AddTotalsRow(Grid grid, int columnCount, params (string Text, int Column, Brush? Fore)[] cells)
    {
        var sepRow = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition());
        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 0),
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        };
        Grid.SetRow(separator, sepRow);
        Grid.SetColumn(separator, 0);
        Grid.SetColumnSpan(separator, columnCount);
        grid.Children.Add(separator);

        var totalRow = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition());
        void Cell(string text, int column, Brush? fore = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
            };
            if (fore != null) tb.Foreground = fore;
            Grid.SetColumn(tb, column); Grid.SetRow(tb, totalRow);
            grid.Children.Add(tb);
        }
        Cell("Total", 0);
        foreach (var (text, column, fore) in cells) Cell(text, column, fore);
    }

    private static Grid BucketTable(List<ReportData.BucketStat> buckets)
    {
        // Same shared alignment grid as DayTable above — which also means switching Week↔Month
        // no longer shifts the figures sideways, since the two tables' first columns were 110
        // and 170 before.
        // Tasks and Score columns as well as the categories (2026-08-05 request) — the Day/Week
        // table has carried both all along, and a Month or Year row that can't tell you what it
        // scored is the summary missing the one figure the whole app is built around.
        var columns = 4 + MoneyHeaders.Length + CategoryColumns.Length;
        var grid = new Grid { ColumnSpacing = ColumnGap, RowSpacing = 6 };
        for (var c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c == 0 ? new GridLength(TableLabelWidth) : GridLength.Auto });
        AddHeaderRow(grid, ["Period", "Tasks",
            .. CategoryColumns.Select(c => c.Label), "Total", "Score", .. MoneyHeaders]);

        foreach (var b in buckets)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition());
            var money = MoneyOf(b.Income, b.Paid, b.PaidMin);
            string[] cells =
            [
                b.Label,
                $"{b.Done}/{b.Total}",
                .. CategoryColumns.Select(c => ReportData.FmtHours(b.Minutes.Of(c.Category))),
                ReportData.FmtHours(b.Minutes.TotalMin),
                b.Score.ToString(),
                .. money.Cells,
            ];
            for (var c = 0; c < cells.Length; c++)
            {
                var tb = new TextBlock
                {
                    Text = cells[c],
                    // Score keeps the same green/amber/red reading it has in the Day/Week table,
                    // so the one figure people actually look for is found the same way in both.
                    Foreground = c == columns - 5 ? ScoreBrush(b.Score)
                        : c >= columns - MoneyHeaders.Length
                            ? MoneyBrush(c - (columns - MoneyHeaders.Length), money.Balance, money.Unearned)
                        : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                };
                Grid.SetColumn(tb, c); Grid.SetRow(tb, row);
                grid.Children.Add(tb);
            }
        }

        // Same totals footer as DayTable — here the Total column can be summed too, since it's
        // already this row's own categories added up. No row-count guard: Month/Year always render
        // several buckets, and a zero-bucket table never reaches this method at all (Render
        // substitutes a "No activity logged yet" message instead).
        if (buckets.Count > 0)
        {
            var periodTotals = SumOfShown(buckets.Select(b => (b.Income, b.Paid, b.PaidMin)));
            var m = MoneyOf(periodTotals.Income, periodTotals.Paid, periodTotals.PaidMin);
            AddTotalsRow(grid, columns,
                [($"{buckets.Sum(b => b.Done)}/{buckets.Sum(b => b.Total)}", 1, null),
                 .. CategoryColumns.Select((c, i) =>
                     (ReportData.FmtHours(buckets.Sum(b => b.Minutes.Of(c.Category))), i + 2, (Brush?)null)),
                 (ReportData.FmtHours(buckets.Sum(b => b.Minutes.TotalMin)), columns - 6, null),
                 (buckets.Sum(b => b.Score).ToString(), columns - 5, null),
                 .. MoneyTotals(m, columns)]);
        }
        return grid;
    }

    // B1i (2026-10-27) added two EUR columns; 2026-11-05 (user request) renamed them and added two more,
    // in every period: hours of paid work, income not earned, income earned by paid work, and the
    // balance of the two ("Income lost/gained" and "Of which paid work" were confusing).
    // Balance = Unearned income + Paid-work income, exactly, in cents.
    /// <summary>First-column width of the Day/Week/Month/Year tables. Narrower than the page-wide
    /// <see cref="LabelColumnWidth"/> (2026-11-05): thirteen columns, four of them money, do not fit the
    /// card at 1180px otherwise and the user asked for no sideways scrolling. The longest label
    /// ("Thu 05.11" + "Day off", or a month name) needs ~100px.</summary>
    private const double TableLabelWidth = 130;

    private static readonly string[] MoneyHeaders = ["Paid hours", "Unearned income", "Paid-work income", "Balance"];

    /// <summary>One row's money cells: paid hours, unearned (balance minus paid work, so it is the
    /// negative "lost" part), paid-work income, balance - all in cents so rows add to the Total.</summary>
    private static (string[] Cells, double Balance, double Unearned) MoneyOf(double income, double paid, int paidMin)
    {
        var balance = Cents(income);
        var work = Cents(paid);
        var unearned = (double)((decimal)balance - (decimal)work);
        return ([ReportData.FmtHours(paidMin), MainWindow.FormatEur(unearned), MainWindow.FormatEur(work),
                 MainWindow.FormatEur(balance)], balance, unearned);
    }

    /// <summary>Colour per money column (by position among them): hours and paid-work income use the
    /// paid-work green, unearned and balance the green/amber/red sign reading.</summary>
    private static Brush MoneyBrush(int moneyColumn, double balance, double unearned) => moneyColumn switch
    {
        1 => IncomeBrush(unearned),
        3 => IncomeBrush(balance),
        _ => PaidBrush(),
    };

    private static (string Text, int Column, Brush? Fore)[] MoneyTotals(
        (string[] Cells, double Balance, double Unearned) m, int columns)
    {
        var first = columns - MoneyHeaders.Length;
        return [.. m.Cells.Select((t, i) => (t, first + i, (Brush?)MoneyBrush(i, m.Balance, m.Unearned)))];
    }

    /// <summary>G14 (user ruling 2026-10-30): a header of two or more words is broken onto two lines at the space
    /// nearest its middle ("Income / lost/gained", "Of which / paid work"), so the columns are only as wide as
    /// their figures and every header reads in full at the default window (1180x780) instead of being cut.
    /// One-word headers stay on one line. Headers sit at the bottom of their row, so every header stays right
    /// above its figures whether it has one line or two.</summary>
    internal static string TwoLineHeader(string header)
    {
        var mid = header.Length / 2.0;
        var best = -1;
        for (var i = 0; i < header.Length; i++)
            if (header[i] == ' ' && (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid))) best = i;
        return best < 0 ? header : header[..best] + "\n" + header[(best + 1)..];
    }

    private static void AddHeaderRow(Grid grid, params string[] headers)
    {
        grid.RowDefinitions.Add(new RowDefinition());
        for (var c = 0; c < headers.Length; c++)
        {
            var h = Caption(TwoLineHeader(headers[c]));
            h.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(h, c); Grid.SetRow(h, 0);
            grid.Children.Add(h);
        }
    }
}

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
    private static Grid DayTable(List<ReportData.DayStat> weekStats)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = _period == ReportPeriod.Day
            ? weekStats.Where(s => s.Date == today).ToList()
            : weekStats;

        // Label column and gap come from the page-wide alignment grid, so this table's first
        // figure lands on the same x as the bars in the two sections below it. Wider than this
        // column strictly needs for a date — that is the trade the shared axis asks for, and the
        // bar rows make the same one with labels as short as "shower".
        var grid = new Grid { ColumnSpacing = ColumnGap, RowSpacing = 6 };
        for (var c = 0; c < 5; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c == 0 ? new GridLength(LabelColumnWidth) : GridLength.Auto });
        AddHeaderRow(grid, "Day", "Tasks", "On-plan", "Off-plan", "Score");

        foreach (var s in rows)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition());
            var isToday = s.Date == today;
            // A day off correctly zeroes on/off-plan minutes here (same rule the score
            // uses), but showing that as a bare "0m/0m" reads identically to a tracking
            // failure or a genuinely idle day everywhere else in the app labels a day off
            // explicitly (2026-07-18 audit finding R8-07) — so this table needs its own
            // label since it's the one place that didn't have one.
            var cells = new[]
            {
                s.Date.ToDisplayDate() + (s.IsDayOff ? "\nDay off" : ""),
                $"{s.Done}/{s.Total}",
                ReportData.FmtMins(s.OnMin), ReportData.FmtMins(s.OffMin),
                s.Score.ToString(),
            };
            for (var c = 0; c < cells.Length; c++)
            {
                var tb = new TextBlock
                {
                    Text = cells[c],
                    FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
                    TextWrapping = c == 0 ? TextWrapping.Wrap : TextWrapping.NoWrap,
                };
                if (c == 4)
                    tb.Foreground = ScoreBrush(s.Score);
                else if (!isToday)
                    tb.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                Grid.SetColumn(tb, c); Grid.SetRow(tb, row);
                grid.Children.Add(tb);
            }
        }

        // Totals under the on-plan/off-plan columns (2026-08-04 request). Sums the values as
        // displayed, which keeps it consistent with business rule 10 for free: a day where
        // every plan is off already contributes 0/0 to those two cells, so it can't inflate
        // the total here while being excluded everywhere else in Reports. Skipped for the
        // single-row Day period, where a "total" of one row is just noise.
        // Tasks and Score are deliberately left blank: a summed "12/14" reads as a ratio it
        // isn't, and a summed Score would sit one column away from the running balance on the
        // card above while meaning something different (points earned this period, not points
        // held) — the exact two-similar-numbers confusion this page has been bitten by before.
        if (rows.Count > 1)
            AddTotalsRow(grid, 5,
                (ReportData.FmtMins(rows.Sum(s => s.OnMin)), 2),
                (ReportData.FmtMins(rows.Sum(s => s.OffMin)), 3));
        return grid;
    }

    /// <summary>Shared footer for both summary tables: a hairline separator, then a bold
    /// "Total" label and whichever column totals the caller supplies (by column index — the
    /// two tables have different column layouts, and only some columns are summable at all).
    /// One implementation so the two can't drift apart in weight, spacing or label.</summary>
    private static void AddTotalsRow(Grid grid, int columnCount, params (string Text, int Column)[] cells)
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
        void Cell(string text, int column)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
            };
            Grid.SetColumn(tb, column); Grid.SetRow(tb, totalRow);
            grid.Children.Add(tb);
        }
        Cell("Total", 0);
        foreach (var (text, column) in cells) Cell(text, column);
    }

    private static Grid BucketTable(List<ReportData.BucketStat> buckets)
    {
        // Same shared alignment grid as DayTable above — which also means switching Week↔Month
        // no longer shifts the figures sideways, since the two tables' first columns were 110
        // and 170 before.
        var grid = new Grid { ColumnSpacing = ColumnGap, RowSpacing = 6 };
        for (var c = 0; c < 4; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c == 0 ? new GridLength(LabelColumnWidth) : GridLength.Auto });
        AddHeaderRow(grid, "Period", "On-plan", "Off-plan", "Total");

        foreach (var b in buckets)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition());
            var cells = new[]
            {
                b.Label, ReportData.FmtMins(b.OnMin), ReportData.FmtMins(b.OffMin),
                ReportData.FmtMins(b.OnMin + b.OffMin),
            };
            for (var c = 0; c < cells.Length; c++)
            {
                var tb = new TextBlock
                {
                    Text = cells[c],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                };
                Grid.SetColumn(tb, c); Grid.SetRow(tb, row);
                grid.Children.Add(tb);
            }
        }

        // Same totals footer as DayTable — here the Total column can be summed too, since
        // it's already just on+off per row. No row-count guard: Month/Year always render
        // several buckets, and a zero-bucket table never reaches this method at all (Render
        // substitutes a "No activity logged yet" message instead).
        if (buckets.Count > 0)
            AddTotalsRow(grid, 4,
                (ReportData.FmtMins(buckets.Sum(b => b.OnMin)), 1),
                (ReportData.FmtMins(buckets.Sum(b => b.OffMin)), 2),
                (ReportData.FmtMins(buckets.Sum(b => b.OnMin + b.OffMin)), 3));
        return grid;
    }

    private static void AddHeaderRow(Grid grid, params string[] headers)
    {
        grid.RowDefinitions.Add(new RowDefinition());
        for (var c = 0; c < headers.Length; c++)
        {
            var h = Caption(headers[c]);
            Grid.SetColumn(h, c); Grid.SetRow(h, 0);
            grid.Children.Add(h);
        }
    }
}

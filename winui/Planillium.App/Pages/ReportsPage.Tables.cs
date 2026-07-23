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

        var grid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        for (var c = 0; c < 5; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c == 0 ? new GridLength(110) : GridLength.Auto });
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
        return grid;
    }

    private static Grid BucketTable(List<ReportData.BucketStat> buckets)
    {
        var grid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        for (var c = 0; c < 4; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c == 0 ? new GridLength(170) : GridLength.Auto });
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

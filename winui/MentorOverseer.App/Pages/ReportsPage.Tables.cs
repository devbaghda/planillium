using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

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
            var cells = new[]
            {
                s.Date.ToDisplayDate(),
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

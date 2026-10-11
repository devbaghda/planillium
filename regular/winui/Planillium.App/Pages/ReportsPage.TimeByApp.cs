using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Top-distractions list (the time-by-app pie lives in ReportsPage.TimeByAppPie.cs) —
// see ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    // ── distractions ─────────────────────────────────────────────────────

    /// <summary>EUR is each row's own hours × the one-hour value (monthly net ÷ 168 h,
    /// <see cref="IncomeService.HourValueEur"/>) — the earlier "period lost income ÷ off-plan
    /// minutes" rate spread every calendar day over the few off-plan hours and priced an hour
    /// at several times its real value (2026-10-01). Hours and EUR sit in their own fixed-width,
    /// right-aligned columns so the figures line up row to row whatever their length.</summary>
    private const double DistractionHoursWidth = 64;
    private const double DistractionEurWidth = 84;

    private static StackPanel DistractionList(List<(string Label, int Minutes)> distractions)
    {
        var hourValue = IncomeService.HourValueEur();
        var maxMin = distractions[0].Minutes;
        var list = new StackPanel { Spacing = 8 };
        foreach (var (label, minutes) in distractions)
        {
            var row = new Grid { ColumnSpacing = ColumnGap };
            // The page-wide alignment grid, shared with Time by App's rows and both summary
            // tables — so every bar and every first figure on this page starts at one x.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumnWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DistractionHoursWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DistractionEurWidth) });
            var name = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis };
            var track = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            };
            var fill = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(8, 300.0 * minutes / Math.Max(maxMin, 1)),
                Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            };
            var overlay = new Grid { VerticalAlignment = VerticalAlignment.Center };
            overlay.Children.Add(track);
            overlay.Children.Add(fill);
            var hours = Dim(ReportData.FmtHours(minutes));
            hours.TextAlignment = TextAlignment.Right;
            var eur = Dim(hourValue > 0 ? MainWindow.FormatEur(minutes / 60.0 * hourValue) : "");
            eur.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(overlay, 1);
            Grid.SetColumn(hours, 2);
            Grid.SetColumn(eur, 3);
            row.Children.Add(name);
            row.Children.Add(overlay);
            row.Children.Add(hours);
            row.Children.Add(eur);
            list.Children.Add(row);
        }
        return list;
    }

}

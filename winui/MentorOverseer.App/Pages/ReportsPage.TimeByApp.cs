using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

using Microsoft.UI.Xaml.Automation;
namespace MentorOverseer.App.Pages;

// Top-distractions list and the "time by app" expandable bar breakdown —
// see ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    // ── distractions ─────────────────────────────────────────────────────

    private static StackPanel DistractionList(List<(string Label, int Minutes)> distractions)
    {
        var maxMin = distractions[0].Minutes;
        var list = new StackPanel { Spacing = 8 };
        foreach (var (label, minutes) in distractions)
        {
            var row = new Grid { ColumnSpacing = 12 };
            // 230, not some other width: matches AppUsageRow's bold-row label
            // column in Time by App, so the two lists' bars start at the same
            // x instead of only their row/card edges lining up.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis };
            var track = new Border
            {
                Height = 8, CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            };
            var fill = new Border
            {
                Height = 8, CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(8, 300.0 * minutes / Math.Max(maxMin, 1)),
                Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            };
            var overlay = new Grid { VerticalAlignment = VerticalAlignment.Center };
            overlay.Children.Add(track);
            overlay.Children.Add(fill);
            var mins = Dim(ReportData.FmtMins(minutes));
            Grid.SetColumn(overlay, 1);
            Grid.SetColumn(mins, 2);
            row.Children.Add(name);
            row.Children.Add(overlay);
            row.Children.Add(mins);
            list.Children.Add(row);
        }
        return list;
    }

    // ── time by app ──────────────────────────────────────────────────────

    /// <summary>Number of top apps shown before "Show more" is needed.</summary>
    private const int DefaultAppsShown = 3;

    private static StackPanel AppBreakdownPanel(List<(string App, ReportData.AppUsage Usage)> breakdown)
    {
        var maxTotal = Math.Max(breakdown[0].Usage.Total, 1);
        var panel = new StackPanel { Spacing = 4 };

        // Everything past the top few apps goes into this collapsed container,
        // revealed by the "Show more" button below. Default view stays short —
        // the three biggest time sinks — with the full list one click away.
        var overflow = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };

        void AddAppRow(StackPanel target, string app, ReportData.AppUsage usage)
        {
            var subs = usage.Subs?
                .OrderByDescending(kv => kv.Value.Total)
                .Take(10).ToList() ?? new();

            // Every top-level row — app or standalone entry (idle, or anything
            // classified by its own description) — gets identical margin, so
            // rows never look like a child of whichever app happened to render
            // just above them. A native Expander draws its own card chrome
            // with a different effective inset than a plain row, which is what
            // caused that; a manual click-to-expand row avoids it entirely.
            var header = AppUsageRow(app, usage, maxTotal, bold: true, expandable: subs.Count > 0);
            header.Margin = new Thickness(0, 6, 0, 6);

            if (subs.Count == 0)
            {
                target.Children.Add(header);
                return;
            }

            var subPanel = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(28, 4, 0, 8),
                Visibility = Visibility.Collapsed,
            };
            foreach (var (sub, su) in subs)
                subPanel.Children.Add(AppUsageRow(sub, su, maxTotal, bold: false));

            var chevron = (FontIcon)header.Children.Last();
            header.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            // A plain Tapped-only Grid is mouse-only: unreachable by Tab and
            // silent to a screen reader (no indication it's interactive, or
            // of its current state). IsTabStop + a Space/Enter handler makes
            // it keyboard-operable; the accessible name is kept in sync with
            // the visible expand/collapse state on every toggle.
            header.IsTabStop = true;
            // WinUI has no attached property to change a plain Grid's reported
            // control type to "button" short of a custom AutomationPeer
            // subclass — HelpText is the lightest honest way to tell Narrator
            // users Enter/Space does something, since the peer still reports
            // as a generic group/pane.
            AutomationProperties.SetHelpText(header,
                "Press Enter or Space to expand or collapse.");
            void SetExpandedState(bool expanded)
            {
                AutomationProperties.SetName(header,
                    $"{app}, {(expanded ? "expanded" : "collapsed")}");
            }
            SetExpandedState(false);
            void Toggle()
            {
                var expanded = subPanel.Visibility == Visibility.Visible;
                subPanel.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
                chevron.Glyph = expanded ? "" : "";
                SetExpandedState(!expanded);
            }
            header.Tapped += (_, _) => Toggle();
            header.KeyDown += (_, e) =>
            {
                if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
                {
                    Toggle();
                    e.Handled = true;
                }
            };

            target.Children.Add(header);
            target.Children.Add(subPanel);
        }

        for (var i = 0; i < breakdown.Count; i++)
            AddAppRow(i < DefaultAppsShown ? panel : overflow, breakdown[i].App, breakdown[i].Usage);

        if (overflow.Children.Count > 0)
        {
            panel.Children.Add(overflow);
            var hidden = breakdown.Count - DefaultAppsShown;
            var moreLabel = $"Show {hidden} more app{(hidden == 1 ? "" : "s")}";
            var moreBtn = new HyperlinkButton
            {
                Content = moreLabel,
                Margin = new Thickness(0, 4, 0, 0),
            };
            moreBtn.Click += (_, _) =>
            {
                var showing = overflow.Visibility == Visibility.Visible;
                overflow.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
                moreBtn.Content = showing ? moreLabel : "Show fewer";
            };
            panel.Children.Add(moreBtn);
        }
        return panel;
    }

    /// <summary>Color key for AppUsageRow's stacked bars — same three
    /// brush keys, same order, so this can never drift from what the bars
    /// actually use.</summary>
    private static StackPanel TimeByAppLegend()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(2, 0, 0, 8) };
        foreach (var (label, brushKey) in new[]
        {
            ("On-plan", CategoryBrushKey("on_plan")),
            ("Off-plan", CategoryBrushKey("off_plan")),
            ("Neutral", CategoryBrushKey("neutral")),
            ("Paid", CategoryBrushKey("paid")),
            ("Idle", CategoryBrushKey("idle")),
        })
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            item.Children.Add(new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources[brushKey],
            });
            item.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            row.Children.Add(item);
        }
        return row;
    }

    /// <summary>Name + stacked on/off/neutral bar + total minutes.</summary>
    private static Grid AppUsageRow(string name, ReportData.AppUsage u, int maxTotal, bool bold,
        bool expandable = false)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(bold ? 230 : 220) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (expandable)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!bold)
            label.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        row.Children.Add(label);

        const double barWidth = 260.0;
        var track = new Border
        {
            Height = 8, CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
        };
        var segments = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 8,
        };
        foreach (var (mins, brushKey) in new[]
        {
            (u.On, CategoryBrushKey("on_plan")),
            (u.Off, CategoryBrushKey("off_plan")),
            (u.Neutral, CategoryBrushKey("neutral")),
            (u.Paid, CategoryBrushKey("paid")),
            (u.Idle, CategoryBrushKey("idle")),
        })
        {
            var w = barWidth * mins / maxTotal;
            if (w >= 1)
                segments.Children.Add(new Border
                {
                    Width = w, Height = 8,
                    Background = (Brush)Application.Current.Resources[brushKey],
                });
        }
        var overlay = new Grid { VerticalAlignment = VerticalAlignment.Center };
        overlay.Children.Add(track);
        overlay.Children.Add(segments);
        Grid.SetColumn(overlay, 1);
        row.Children.Add(overlay);

        var total = Dim(ReportData.FmtMins(u.Total));
        total.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(total, 2);
        row.Children.Add(total);

        if (expandable)
        {
            var chevron = new FontIcon
            {
                Glyph = "",
                FontSize = 12,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(chevron, 3);
            row.Children.Add(chevron);
        }
        return row;
    }
}
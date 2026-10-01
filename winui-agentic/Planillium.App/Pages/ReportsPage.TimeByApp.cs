using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Top-distractions list and the "time by app" pie chart with drill-down —
// see ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    // ── distractions ─────────────────────────────────────────────────────

    /// <summary>EUR is derived from the fixed hourly rate (IncomeService.HourValueEur()),
    /// signed per the period's income sum, applied to each row's own minutes. When hourly
    /// rate is 0, the EUR column is empty but still reserved.</summary>
    private static StackPanel DistractionList(List<(string Label, int Minutes)> distractions,
        double periodIncomeSum)
    {
        var hourValue = IncomeService.HourValueEur();
        var signedHourValue = periodIncomeSum < 0 ? -hourValue : hourValue;
        var perMinuteEur = signedHourValue / 60.0;
        var maxMin = distractions[0].Minutes;
        var list = new StackPanel { Spacing = 8 };
        foreach (var (label, minutes) in distractions)
        {
            var row = new Grid { ColumnSpacing = ColumnGap };
            // The page-wide alignment grid, shared with Time by App's rows and both summary
            // tables — so every bar and every first figure on this page starts at one x.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumnWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HoursColumnWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EurColumnWidth) });
            var name = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis };

            // Proportional bar fill: inner Grid with two weighted columns (filled ratio and empty ratio)
            // so the fill never exceeds the track width, even on narrow windows.
            var ratio = minutes / Math.Max(maxMin, 1.0);
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
                Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            };
            var fillGrid = new Grid();
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });
            Grid.SetColumn(fill, 0);
            fillGrid.Children.Add(fill);
            var overlay = new Grid { VerticalAlignment = VerticalAlignment.Center };
            overlay.Children.Add(track);
            overlay.Children.Add(fillGrid);

            var hours = Dim(ReportData.FmtHours(minutes));
            hours.HorizontalAlignment = HorizontalAlignment.Right;
            hours.TextAlignment = TextAlignment.Right;
            hours.VerticalAlignment = VerticalAlignment.Center;

            var eur = Dim(perMinuteEur != 0 ? MainWindow.FormatEur(minutes * perMinuteEur) : "");
            eur.HorizontalAlignment = HorizontalAlignment.Right;
            eur.TextAlignment = TextAlignment.Right;
            eur.VerticalAlignment = VerticalAlignment.Center;

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

    // ── time by app (pie chart with drill-down) ──────────────────────────

    /// <summary>Pie chart with drill-down to sub-items. Returns a panel with breadcrumb, pie, and list.
    /// The drill path is held in the page's static _appDrillPath field (reset on period switch).</summary>
    private StackPanel AppBreakdownPie(List<(string App, ReportData.AppUsage Usage)> breakdown)
    {
        var panel = new StackPanel { Spacing = 12 };

        // Drill down to the current level based on the stored drill path
        var currentLevel = breakdown;
        var levelName = "All apps";
        foreach (var appName in ReportsPage._appDrillPath)
        {
            var app = currentLevel.FirstOrDefault(x => x.App == appName);
            if (app.Usage == null || app.Usage.Subs == null || app.Usage.Subs.Count == 0)
            {
                // Path is invalid, reset to root
                ReportsPage._appDrillPath.Clear();
                currentLevel = breakdown;
                levelName = "All apps";
                break;
            }
            currentLevel = app.Usage.Subs
                .OrderByDescending(kv => kv.Value.Total)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
            levelName = appName;
        }

        var levelTotal = currentLevel.Sum(x => x.Usage.Total);
        if (levelTotal == 0)
            return panel;  // No data at this level

        // Build slices using the pure logic service
        var slices = PieSlices.BuildSlices(currentLevel, levelTotal);
        if (slices.Count == 0)
            return panel;

        // ── breadcrumb & back button ──────────────────────────────────────
        var breadcrumbPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var breadcrumbText = "All apps";
        if (ReportsPage._appDrillPath.Count > 0)
            breadcrumbText = "All apps › " + string.Join(" › ", ReportsPage._appDrillPath);

        breadcrumbPanel.Children.Add(new TextBlock
        {
            Text = breadcrumbText,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        if (ReportsPage._appDrillPath.Count > 0)
        {
            var backBtn = new HyperlinkButton
            {
                Content = "← Back",
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
            };
            AutomationProperties.SetName(backBtn, "Back to previous level");
            backBtn.Click += (_, _) =>
            {
                if (ReportsPage._appDrillPath.Count > 0)
                {
                    ReportsPage._appDrillPath.RemoveAt(ReportsPage._appDrillPath.Count - 1);
                    Render();
                }
            };
            backBtn.KeyDown += (_, e) =>
            {
                if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
                {
                    if (ReportsPage._appDrillPath.Count > 0)
                    {
                        ReportsPage._appDrillPath.RemoveAt(ReportsPage._appDrillPath.Count - 1);
                        Render();
                    }
                    e.Handled = true;
                }
            };
            breadcrumbPanel.Children.Add(backBtn);
        }
        panel.Children.Add(breadcrumbPanel);

        // ── pie chart ─────────────────────────────────────────────────────
        const double pieRadius = 110;  // Radius in device-independent pixels
        const double pieSize = pieRadius * 2;
        const double pieCenterX = pieRadius;
        const double pieCenterY = pieRadius;

        var piePath = new StackPanel { Spacing = 16, Orientation = Orientation.Horizontal };

        // Canvas for pie wedges
        var canvas = new Canvas { Width = pieSize, Height = pieSize, Margin = new Thickness(0, 0, 16, 0) };

        // Check if this is a full circle (single 100% slice)
        var isFullCircle = slices.Count == 1 && Math.Abs(slices[0].SweepAngle - 360.0) < 0.01;

        foreach (var slice in slices)
        {
            var brushKey = CategoryBrushKey(slice.DominantCategory);
            var fill = (Brush)Application.Current.Resources[brushKey];
            var stroke = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];

            if (isFullCircle)
            {
                // Full circle: use EllipseGeometry
                var wedge = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    Data = new EllipseGeometry
                    {
                        Center = new Windows.Foundation.Point(pieCenterX, pieCenterY),
                        RadiusX = pieRadius,
                        RadiusY = pieRadius,
                    },
                };
                canvas.Children.Add(wedge);
            }
            else
            {
                // Partial slice: arc from center
                var startRad = slice.StartAngle * Math.PI / 180.0;
                var sweepRad = slice.SweepAngle * Math.PI / 180.0;
                var endRad = startRad + sweepRad;

                // Arc endpoints (convert from top-clockwise to standard math coords)
                var startX = pieCenterX + pieRadius * Math.Sin(startRad);
                var startY = pieCenterY - pieRadius * Math.Cos(startRad);
                var endX = pieCenterX + pieRadius * Math.Sin(endRad);
                var endY = pieCenterY - pieRadius * Math.Cos(endRad);

                // Build path geometry: center -> start -> arc -> end -> center
                var geo = new Microsoft.UI.Xaml.Media.PathGeometry();
                var figure = new Microsoft.UI.Xaml.Media.PathFigure { StartPoint = new Windows.Foundation.Point(pieCenterX, pieCenterY) };

                // Line to arc start
                figure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment { Point = new Windows.Foundation.Point(startX, startY) });

                // Arc
                var isLargeArc = slice.SweepAngle > 180;
                figure.Segments.Add(new Microsoft.UI.Xaml.Media.ArcSegment
                {
                    Point = new Windows.Foundation.Point(endX, endY),
                    Size = new Windows.Foundation.Size(pieRadius, pieRadius),
                    RotationAngle = 0,
                    IsLargeArc = isLargeArc,
                    SweepDirection = SweepDirection.Clockwise,
                });

                // Line back to center
                figure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment { Point = new Windows.Foundation.Point(pieCenterX, pieCenterY) });

                geo.Figures.Add(figure);

                var wedge = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    Data = geo,
                };

                canvas.Children.Add(wedge);
            }
        }

        piePath.Children.Add(canvas);

        // ── list rows ─────────────────────────────────────────────────────
        var list = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Top };

        // Add list rows for each slice
        AddPieListRows(list, slices, currentLevel, levelTotal);

        piePath.Children.Add(list);
        panel.Children.Add(piePath);

        return panel;
    }

    /// <summary>Helper to add list rows for pie slices, including (no detail) if needed.</summary>
    private void AddPieListRows(StackPanel list, List<PieSlices.Slice> slices,
        List<(string Name, ReportData.AppUsage Usage)> currentLevel, int levelTotal)
    {
        foreach (var slice in slices)
        {
            AddPieListRow(list, slice);
        }

        // Check if we need to add (no detail) slice
        if (ReportsPage._appDrillPath.Count > 0)
        {
            var appName = ReportsPage._appDrillPath.Last();
            var app = currentLevel.FirstOrDefault(x => x.Name == appName);
            if (app.Usage != null && app.Usage.Subs != null)
            {
                var sumOfSubs = app.Usage.Subs.Sum(x => x.Value.Total);
                var noDetail = PieSlices.NoDetailSlice(app.Usage.Total, sumOfSubs);
                if (noDetail != null)
                {
                    AddPieListRow(list, noDetail);
                }
            }
        }
    }

    /// <summary>Add a single list row for a pie slice.</summary>
    private void AddPieListRow(StackPanel list, PieSlices.Slice slice)
    {
        var row = new Grid { ColumnSpacing = ColumnGap, Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });  // Color swatch
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });  // Name
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HoursColumnWidth) });  // Hours
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // Percentage
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });  // Bar
        if (slice.IsDrillable)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ChevronColumnWidth) });

        // Color swatch
        var brushKey = CategoryBrushKey(slice.DominantCategory);
        var swatch = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources[brushKey],
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(swatch, 0);
        row.Children.Add(swatch);

        // Name + accessibility
        var categoryBreakdown = $"{ReportData.FmtHours(slice.On)} on-plan, " +
                               $"{ReportData.FmtHours(slice.Off)} off-plan, " +
                               $"{ReportData.FmtHours(slice.Neutral)} neutral, " +
                               $"{ReportData.FmtHours(slice.Paid)} paid, " +
                               $"{ReportData.FmtHours(slice.Idle)} idle";
        var name = new TextBlock
        {
            Text = slice.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(row, $"{slice.Name}, {ReportData.FmtHours(slice.Total)}, " +
            $"{slice.Percentage:F1}%, {categoryBreakdown}" +
            (slice.IsDrillable ? ", opens breakdown" : ""));
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        // Hours
        var hours = Dim(ReportData.FmtHours(slice.Total));
        hours.HorizontalAlignment = HorizontalAlignment.Right;
        hours.TextAlignment = TextAlignment.Right;
        hours.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(hours, 2);
        row.Children.Add(hours);

        // Percentage
        var pct = Dim($"{slice.Percentage:F1}%");
        pct.HorizontalAlignment = HorizontalAlignment.Right;
        pct.TextAlignment = TextAlignment.Right;
        pct.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(pct, 3);
        row.Children.Add(pct);

        // Stacked category bar
        const double barWidth = 80.0;
        var segments = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 8,
        };
        foreach (var (category, _, minutesOf) in StackedCategories)
        {
            var mins = minutesOf(new ReportData.AppUsage
            {
                On = slice.On, Off = slice.Off, Neutral = slice.Neutral, Paid = slice.Paid, Idle = slice.Idle
            });
            var w = barWidth * mins / Math.Max(slice.Total, 1);
            if (w >= 1)
                segments.Children.Add(new Border
                {
                    Width = w,
                    Height = 8,
                    Background = (Brush)Application.Current.Resources[CategoryBrushKey(category)],
                });
        }
        var track = new Border
        {
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
        };
        var overlay = new Grid { VerticalAlignment = VerticalAlignment.Center };
        overlay.Children.Add(track);
        overlay.Children.Add(segments);
        Grid.SetColumn(overlay, 4);
        row.Children.Add(overlay);

        // Chevron if drillable
        if (slice.IsDrillable)
        {
            row.IsTabStop = true;
            var chevron = new FontIcon
            {
                Glyph = "",
                FontSize = 12,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(chevron, 5);
            row.Children.Add(chevron);

            row.Tapped += (_, _) =>
            {
                ReportsPage._appDrillPath.Add(slice.Name);
                Render();
            };
            row.KeyDown += (_, e) =>
            {
                if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
                {
                    ReportsPage._appDrillPath.Add(slice.Name);
                    Render();
                    e.Handled = true;
                }
            };
        }

        list.Children.Add(row);
    }

    /// <summary>The five categories AppUsageRow stacks into a bar, in display order, with
    /// the label the legend shows and how to read that category's minutes off an AppUsage.
    /// Previously two independently hand-written lists (the legend and the bar) kept in
    /// sync only by a comment promising they matched, not by sharing a source
    /// (2026-07-18 audit finding R8-13).</summary>
    private static readonly (string Category, string Label, Func<ReportData.AppUsage, int> Minutes)[] StackedCategories =
        [.. DiaryCategory.ReportOrder.Select(o => (o.Value, o.Label, Minutes(o.Value)))];

    /// <summary>Which field of an AppUsage a category's minutes live in. Throws on an unknown
    /// category rather than returning a zero accessor: a sixth category added to
    /// <see cref="DiaryCategory.ReportOrder"/> and forgotten here would otherwise draw a bar
    /// segment of zero width — a silently missing slice of every bar on the page.</summary>
    private static Func<ReportData.AppUsage, int> Minutes(string category) => category switch
    {
        DiaryCategory.OnPlan => u => u.On,
        DiaryCategory.OffPlan => u => u.Off,
        DiaryCategory.Neutral => u => u.Neutral,
        DiaryCategory.Paid => u => u.Paid,
        DiaryCategory.Idle => u => u.Idle,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category,
            "No AppUsage field for this diary category."),
    };

    /// <summary>Color key for pie slices and stacked bars — reads from
    /// StackedCategories, so this can never drift from what the visuals
    /// actually use.</summary>
    private static StackPanel TimeByAppLegend()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(2, 0, 0, 8) };
        foreach (var (category, label, _) in StackedCategories)
        {
            var brushKey = CategoryBrushKey(category);
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            item.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
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
}

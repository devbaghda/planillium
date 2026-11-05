using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Time by app as a pie (2026-10-01 request, replacing the stacked bars): one slice per app,
// click a slice (or its legend row) to turn the pie into a pie of what's inside it — pages for
// an app, the remaining apps for "Other" — with a back link. Same collapsible idea as the old
// expandable rows, drawn as drill-down.
public sealed partial class ReportsPage
{
    /// <summary>One slice: its label, minutes and, if it can be opened, what it opens into.</summary>
    private sealed record PieNode(string Label, int Minutes, List<PieNode>? Children);

    /// <summary>Slices shown before the rest are folded into "Other" — more than this and the
    /// smallest become unreadable slivers.</summary>
    private const int MaxPieSlices = 8;

    private const double PieSize = 220;

    private static List<PieNode> NodesFromSubs(SortedDictionary<string, ReportData.AppUsage>? subs) =>
        FoldIntoOther((subs ?? new())
            .Where(kv => kv.Value.Total > 0)
            .OrderByDescending(kv => kv.Value.Total)
            .Select(kv => new PieNode(kv.Key, kv.Value.Total, null)).ToList());

    private static List<PieNode> NodesFromApps(List<(string App, ReportData.AppUsage Usage)> breakdown) =>
        FoldIntoOther(breakdown
            .Where(b => b.Usage.Total > 0)
            .Select(b => new PieNode(b.App, b.Usage.Total, b.Usage.Subs is { Count: > 0 }
                ? NodesFromSubs(b.Usage.Subs) : null))
            .OrderByDescending(n => n.Minutes).ToList());

    /// <summary>Keeps the biggest <see cref="MaxPieSlices"/> - 1 and merges the rest into one
    /// "Other" slice that itself opens into those merged items.</summary>
    private static List<PieNode> FoldIntoOther(List<PieNode> sorted)
    {
        if (sorted.Count <= MaxPieSlices) return sorted;
        var keep = sorted.Take(MaxPieSlices - 1).ToList();
        var rest = sorted.Skip(MaxPieSlices - 1).ToList();
        keep.Add(new PieNode("Other", rest.Sum(n => n.Minutes), rest));
        return keep;
    }

    private static StackPanel AppBreakdownPanel(List<(string App, ReportData.AppUsage Usage)> breakdown)
    {
        var host = new StackPanel { Spacing = 8 };
        var trail = new List<PieNode>();      // nodes drilled through, outermost first
        var root = NodesFromApps(breakdown);

        void Render()
        {
            host.Children.Clear();
            var level = trail.Count == 0 ? root : trail[^1].Children!;
            var total = Math.Max(level.Sum(n => n.Minutes), 1);

            var title = trail.Count == 0
                ? "All apps"
                : string.Join(" › ", trail.Select(n => n.Label));
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            if (trail.Count > 0)
            {
                var back = new HyperlinkButton { Content = "← Back", Padding = new Thickness(0) };
                AutomationProperties.SetName(back, "Back to the previous pie");
                back.Click += (_, _) => { trail.RemoveAt(trail.Count - 1); Render(); };
                header.Children.Add(back);
            }
            header.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            host.Children.Add(header);
            if (trail.Count == 0)
                host.Children.Add(Dim("Click a slice or a name to see what's inside it."));

            var body = new Grid { ColumnSpacing = 24 };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pie = new Grid { Width = PieSize, Height = PieSize, VerticalAlignment = VerticalAlignment.Top };
            var legend = new StackPanel { Spacing = 4 };
            // 2026-11-05 (user: "too much empty space"): each legend row carries a share bar between its
            // name and its figures, sized against the biggest row, so the row's width shows the ranking
            // instead of sitting empty. The pie keeps the exact shares.
            var biggest = Math.Max(level.Max(n => n.Minutes), 1);
            var angle = -90.0;      // start at 12 o'clock
            for (var i = 0; i < level.Count; i++)
            {
                var node = level[i];
                var share = (double)node.Minutes / total;
                var opacity = Math.Max(0.25, 1.0 - 0.11 * i);
                var tip = $"{node.Label} — {ReportData.FmtHours(node.Minutes)} ({share:P0})";
                var open = node.Children is { Count: > 0 };
                void Open() { trail.Add(node); Render(); }

                var slice = PieSlice(angle, angle + 360 * share, opacity);
                ToolTipService.SetToolTip(slice, tip);
                if (open) slice.Tapped += (_, _) => Open();
                pie.Children.Add(slice);
                angle += 360 * share;

                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
                // Always reserve the chevron slot so hours/% line up whether or not the row drills down.
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                var swatch = new Border
                {
                    Width = 10, Height = 10, CornerRadius = new CornerRadius(2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                    Opacity = opacity,
                };
                var name = new TextBlock
                {
                    Text = node.Label, TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var hours = Dim(ReportData.FmtHours(node.Minutes));
                hours.TextAlignment = TextAlignment.Right;
                var pct = Dim($"{share:P0}");
                pct.TextAlignment = TextAlignment.Right;
                var bar = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center };
                var frac = (double)node.Minutes / biggest;
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(frac, GridUnitType.Star) });
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - frac, 0), GridUnitType.Star) });
                bar.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                    Opacity = opacity,
                });
                Grid.SetColumn(name, 1); Grid.SetColumn(bar, 2); Grid.SetColumn(hours, 3); Grid.SetColumn(pct, 4);
                foreach (var el in new FrameworkElement[] { swatch, name, bar, hours, pct }) row.Children.Add(el);
                ToolTipService.SetToolTip(row, tip);

                if (open)
                {
                    row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    row.IsTabStop = true;
                    AutomationProperties.SetName(row, $"{node.Label}, {tip.Split(" — ")[1]}, press Enter to open");
                    row.Tapped += (_, _) => Open();
                    row.KeyDown += (_, e) =>
                    {
                        if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
                        {
                            Open();
                            e.Handled = true;
                        }
                    };
                    var chevron = new FontIcon
                    {
                        Glyph = "", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    };
                    Grid.SetColumn(chevron, 5);
                    row.Children.Add(chevron);
                }
                legend.Children.Add(row);
            }
            // Donut: the slices are rings, so the middle is empty and carries the total.
            var centre = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
            centre.Children.Add(new TextBlock { Text = ReportData.FmtHours(total), FontSize = 20, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            centre.Children.Add(new TextBlock { Text = "total", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            pie.Children.Add(centre);
            Grid.SetColumn(legend, 1);
            body.Children.Add(pie);
            body.Children.Add(legend);
            host.Children.Add(body);
        }

        Render();
        return host;
    }

    /// <summary>One wedge from <paramref name="startDeg"/> to <paramref name="endDeg"/> (degrees,
    /// clockwise from 3 o'clock). A single slice covering the whole pie is a circle — an arc
    /// whose start and end coincide draws nothing.</summary>
    private static Shape PieSlice(double startDeg, double endDeg, double opacity)
    {
        var fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var edge = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        const double r = PieSize / 2;
        const double ri = r * 0.5;   // hole radius: the ring is the outer half of the pie
        if (endDeg - startDeg >= 359.99)
        {
            // Two concentric circles with even-odd filling = a ring (one arc whose ends coincide draws nothing).
            var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
            ring.Children.Add(new EllipseGeometry { Center = new Windows.Foundation.Point(r, r), RadiusX = r, RadiusY = r });
            ring.Children.Add(new EllipseGeometry { Center = new Windows.Foundation.Point(r, r), RadiusX = ri, RadiusY = ri });
            return new Microsoft.UI.Xaml.Shapes.Path { Data = ring, Fill = fill, Opacity = opacity };
        }

        Windows.Foundation.Point At(double deg, double rad) => new(
            r + rad * Math.Cos(deg * Math.PI / 180), r + rad * Math.Sin(deg * Math.PI / 180));
        var large = endDeg - startDeg > 180;
        var figure = new PathFigure { StartPoint = At(startDeg, ri), IsClosed = true };
        figure.Segments.Add(new LineSegment { Point = At(startDeg, r) });
        figure.Segments.Add(new ArcSegment
        {
            Point = At(endDeg, r), Size = new Windows.Foundation.Size(r, r),
            SweepDirection = SweepDirection.Clockwise, IsLargeArc = large,
        });
        figure.Segments.Add(new LineSegment { Point = At(endDeg, ri) });
        figure.Segments.Add(new ArcSegment
        {
            Point = At(startDeg, ri), Size = new Windows.Foundation.Size(ri, ri),
            SweepDirection = SweepDirection.Counterclockwise, IsLargeArc = large,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry, Fill = fill, Opacity = opacity,
            Stroke = edge, StrokeThickness = 1.5,
        };
    }
}

using Planillium.App.Services;

namespace Planillium.App.Pages;

// Small shared visual helpers used across the other ReportsPage.*.cs files —
// see ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    /// <summary>
    /// The one alignment grid every section above the diary shares (2026-08-05 request, "align
    /// all the reports"): a fixed label column, then a fixed gap, so the summary table's first
    /// figure, the distraction bars and the time-by-app bars all start at the same x down the
    /// whole page. Measured before the change: the bars began at 586 (230 + 12), the table's
    /// first figure at 532 (170 + 18) and the indented sub-rows under Time by App at 604 — three
    /// axes on one page, each individually reasonable.
    ///
    /// Every section reads these rather than declaring its own numbers. That is the point: they
    /// used to be independent literals, which is how they drifted apart without anyone choosing
    /// it. Re-aligning the page now means changing it here, once.
    /// </summary>
    internal const double LabelColumnWidth = 230;

    /// <inheritdoc cref="LabelColumnWidth"/>
    internal const double ColumnGap = 12;

    /// <summary>How far a sub-row under Time by App is indented from its parent app row. Its own
    /// label column is <see cref="LabelColumnWidth"/> minus this, so the indent moves the label
    /// without moving the bar off the shared axis.</summary>
    internal const double SubRowIndent = 28;

    // Mirrors the same great/bad/warning split as ReportExport.cs's exported
    // HTML (raw hex there, since that file has to render correctly outside
    // this app/theme entirely) — if this threshold or color intent is ever
    // retuned, update both (round-4 audit finding).
    private static Brush ScoreBrush(int score) =>
        Ui.Brush(
            score >= ScoreService.GreatDayThreshold ? "SystemFillColorSuccessBrush"
            : score < 0 ? "SystemFillColorCriticalBrush"
            : "SystemFillColorCautionBrush");

    /// <summary>Same three-way split as ScoreBrush, for the lost/gained-earnings figure:
    /// green while ahead (employed, net positive for the period), red while behind
    /// (unemployed, net negative), caution only at exactly zero.</summary>
    private static Brush IncomeBrush(double eur) =>
        Ui.Brush(
            eur > 0 ? "SystemFillColorSuccessBrush"
            : eur < 0 ? "SystemFillColorCriticalBrush"
            : "SystemFillColorCautionBrush");

    /// <summary>Paid-work column: always light green (per-theme value in ThemeSync's Fixed table).</summary>
    private static Brush PaidBrush() => Ui.Brush("PaidWorkBrush");

    /// <summary>Thin alias kept so every call site in this file doesn't need renaming —
    /// see CategoryStyle.BrushKey (Services/CategoryStyle.cs) for the actual single
    /// source of truth, now shared with MainWindow.Tracker's tray pill too.</summary>
    private static string CategoryBrushKey(string category) => CategoryStyle.BrushKey(category);

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 60,
        Foreground = Ui.Brush("TextFillColorTertiaryBrush"),
        Margin = new Thickness(2, 22, 0, 8),
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 50,
        Foreground = Ui.Brush("TextFillColorTertiaryBrush"),
    };

    private static TextBlock Dim(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = Ui.Brush("TextFillColorSecondaryBrush"),
    };

    /// <summary>
    /// Wraps a summary table so a narrow window scrolls it sideways instead of silently losing
    /// its last columns (2026-08-05). Measured at the app's minimum window (900dip): the ten-column
    /// week table needs ~617px of a ~541px card, and the Score column — the one the user had just
    /// asked for — was simply absent, along with half the "Total" header. The card is a
    /// rounded-corner Border, so it corner-clips its content to its own arranged bounds with no
    /// scrollbar, no overhang and no error: it reads as "that column doesn't exist".
    ///
    /// The alternative was shrinking the label column at narrow widths, which would have traded
    /// this away against the page-wide alignment grid the previous round established. Scrolling
    /// keeps both. The diary list below already solves the same problem the same way.
    /// </summary>
    private static ScrollViewer Scrollable(UIElement child)
    {
        var sv = new ScrollViewer
        {
            Content = child,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            // Vertical stays off: the page itself scrolls, and a nested vertical scroller would
            // swallow wheel events over the table.
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        // Rule (user, 2026-11-05): sideways scroll is the LAST resort. A ScrollViewer measures its
        // content with unlimited width, so a proportional (Star) table would shrink to its natural size
        // and leave a scrollbar for nothing. Instead the content is set to the viewport's width, and
        // its own MinWidth keeps it from going narrower - so the bar appears only below that minimum.
        if (child is FrameworkElement fe)
            sv.SizeChanged += (_, e) => fe.Width = Math.Max(fe.MinWidth, e.NewSize.Width);
        return sv;
    }

    private static Border Card(UIElement child) => new()
    {
        Background = Ui.Brush("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Ui.Brush("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18, 14, 18, 14),
        Child = child,
    };
}

using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// This page's code-behind is split across several files by responsibility
// (all still the one ReportsPage type — partial class, not separate objects):
//   ReportsPage.xaml.cs      — this file: period selector, the main Render
//                              pipeline, score/drift/insights cards
//   ReportsPage.Tables.cs    — the day/bucket summary tables
//   ReportsPage.TimeByApp.cs — distraction list + "time by app" bars
//   ReportsPage.Diary.cs     — the time-diary section (search, mark, edit)
//   ReportsPage.Styling.cs   — small shared visual helpers (Card, Section, …)
public sealed partial class ReportsPage : Page
{
    // Survives navigation: come back to Reports and it's still on your period.
    private static ReportPeriod _period = ReportPeriod.Week;

    private static readonly (string Label, ReportPeriod Period)[] PeriodOpts =
    {
        ("Day", ReportPeriod.Day), ("Week", ReportPeriod.Week),
        ("Month", ReportPeriod.Month), ("Year", ReportPeriod.Year),
    };

    public ReportsPage()
    {
        InitializeComponent();
        BuildPeriodBar();
    }

    // The Diary section's own list/card widths (ReportsPage.Diary.cs) — declared once here so
    // this file's own column-width cap and Diary's card-clip fix can never drift apart the way
    // two independently-hardcoded copies of the same number already have elsewhere in this app's
    // history. DiaryListWidth is the row content's own required width; DiaryCardWidth adds
    // Card()'s horizontal padding (18+18) on top, since that's what actually has to fit without
    // clipping (2026-07-28 — see ReportsPage.Diary.cs).
    //
    // Was 950, set for the App/Page column split (2026-07-23) and never revisited when the Tag
    // column was added (2026-08-07, +90 +12 gap) — so the row's real natural width quietly grew
    // past this constant, which is exactly what forced a horizontal scrollbar on every window
    // size regardless of how wide, not just narrow ones (2026-08-13 report, "remove the
    // horizontal scroll bar"). Recomputed here alongside narrowing Page/Details (see BuildRow) —
    // narrowing alone wouldn't have fixed it, since this constant was already wrong before that.
    //
    // 2026-08-13's recompute still only checked the showDate=false row (Time column = 110) — it
    // never accounted for showDate=true (150, used whenever a search/All-time/filter widens the
    // scope past one day), so any of those views kept overflowing by the same 40px this constant
    // was already short by, forcing the scrollbar right back — 2026-08-28 report, reproduced by
    // typing into the search box. Fixed by narrowing Category/Tag/App/Page/Details another 80px
    // (see BuildRow) so the row fits within this same 870 budget even at the wider Time width —
    // no change needed here, the constant was fine, the columns summing past it were the bug.
    internal const double DiaryListWidth = 870;
    internal const double DiaryCardWidth = DiaryListWidth + 36;

    // Was a flat 880 — comfortably fit every OTHER section (they all stretch/wrap fine at
    // any width up to this), but narrower than Diary's own required width, so Diary alone
    // still needed horizontal scrolling to reach Edit/Split even on a wide window with
    // plenty of unused space either side (2026-07-28 request). Widened to fit Diary too,
    // with a little breathing room past what it strictly needs.
    private const double MaxContentWidth = DiaryCardWidth + 20;

    // See PageLayout's own doc comment for why this can't be HorizontalAlignment/MaxWidth on
    // ContentColumn directly — this page originated that fix (2026-07-28); every other page now
    // shares the same one implementation instead of its own copy of this math.
    private void RootScroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageLayout.CenterContent(RootScroller, ContentColumn, MaxContentWidth, e.NewSize);

    // NavigationCacheMode="Enabled" (see XAML) reuses this instance across
    // menu switches instead of reconstructing the page + reopening the DB
    // every time; OnNavigatedTo fires every visit (cached or not), unlike
    // Loaded which would only fire once for a reused instance.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render();
    }

    // NavigationCacheMode="Enabled" keeps this page instance alive (and its
    // diary live-refresh timer running) even after navigating away — stop
    // it here so it doesn't keep querying the DB every 30s while the page
    // is cached but not on screen.
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _diaryLiveRefresh?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        try { ReportExport.ExportWeek(); }
        catch (Exception ex) { Log.Error("ReportsPage.ExportHtml", ex); }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try { ReportExport.ExportCsv(_period); }
        catch (Exception ex) { Log.Error("ReportsPage.ExportCsv", ex); }
    }

    // ── period selector ───────────────────────────────────────────────────

    private void BuildPeriodBar()
    {
        foreach (var (label, period) in PeriodOpts)
            PeriodBar.Items.Add(new RadioButton { Content = label, Tag = period });
        PeriodBar.SelectedIndex = Array.FindIndex(PeriodOpts, o => o.Period == _period);
        PeriodBar.SelectionChanged += (_, _) =>
        {
            if (PeriodBar.SelectedItem is RadioButton { Tag: ReportPeriod p })
            {
                _period = p;
                Render();
            }
        };
    }

    /// <summary>Internal (not private) so MainWindow's day-change watcher can force a
    /// refresh when the calendar date rolls over while this cached page stays on screen —
    /// same reasoning as TodayPage/SchedulePage's own Render() (2026-07-24 audit finding #1:
    /// the original day-change fix covered those two pages but missed this structurally
    /// identical one, which anchors its entire Day/Week/Month/Year view on DateTime.Today).</summary>
    internal void Render()
    {
        Body.Children.Clear();
        SaveErrorBar.IsOpen = false;
        ExportCsvItem.Text = $"CSV ({ReportData.PeriodName(_period).ToLowerInvariant()})";
        try
        {
            var plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            var periodName = ReportData.PeriodName(_period);
            var weekStats = ReportData.WeekStats(db.Conn, score);
            var today = DateOnly.FromDateTime(DateTime.Today);
            // Everything on this page above the diary now follows the period selector
            // (2026-08-04 request). The score card and the insights panel were the two that
            // didn't: the card always showed today, and the insights were always computed from
            // this week regardless of what the selector said.
            var totals = ReportData.PeriodStats(_period, db.Conn, score);

            Body.Children.Add(Card(ScoreCard(totals, periodName)));

            // ── summary table ─────────────────────────────────────────────
            Body.Children.Add(Section(periodName));
            // Scrollable(): these tables are the widest thing on the page (ten columns since
            // 2026-08-05) and the card would otherwise clip the last of them off in silence on a
            // narrow window — see Scrollable's own comment for the measurement.
            if (_period is ReportPeriod.Day or ReportPeriod.Week)
                Body.Children.Add(Card(Scrollable(DayTable(weekStats))));
            else
            {
                var buckets = _period == ReportPeriod.Month
                    ? ReportData.MonthBuckets(db.Conn, score) : ReportData.YearBuckets(db.Conn, score);
                // Month always has at least this-week's row seeded in,
                // but Year only gets rows for months that actually have
                // data — a brand-new install (or a period with zero
                // history) renders just a bare header row otherwise,
                // which reads as broken rather than simply empty.
                Body.Children.Add(buckets.Count == 0
                    ? Dim("No activity logged yet.")
                    : Card(Scrollable(BucketTable(buckets))));
            }

            // ── top distractions (grouped: "Chrome - YouTube") ────────────
            Body.Children.Add(Section($"TOP DISTRACTIONS — {periodName}"));
            var distractions = ReportData.TopDistractions(_period, db.Conn, score);
            if (distractions.Count == 0)
                Body.Children.Add(Dim("No off-plan time logged. Impressive."));
            else
                Body.Children.Add(Card(DistractionList(distractions)));

            // ── time by app (expandable groups) ───────────────────────────
            Body.Children.Add(Section($"TIME BY APP — {periodName}"));
            // Top 3 show by default; the rest sit behind "Show more", so pull a
            // generous slice rather than the default handful — the point of the
            // expander is to reveal the full picture on demand.
            var breakdown = ReportData.AppBreakdown(_period, db.Conn, score, limit: 100);
            if (breakdown.Count == 0)
                Body.Children.Add(Dim("No activity logged yet."));
            else
            {
                // The bars below are colored with no other label — without
                // this, the only way to know what a color means is to
                // already know it (2026-07-09 audit finding #18).
                Body.Children.Add(TimeByAppLegend());
                Body.Children.Add(Card(AppBreakdownPanel(breakdown)));
            }

            Body.Children.Add(Section($"INSIGHTS — {periodName}"));
            Body.Children.Add(Card(InsightsPanel(totals, db.Conn, score)));

            BuildDiarySection(today, score);
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.Render", ex);
            Body.Children.Add(new TextBlock
            {
                Text = Log.Friendly("Couldn't load your report data", ex),
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    /// <summary>Points earned over the selected period. "EARNED" is in the caption on purpose:
    /// this is what the period's activity added up to, which is not the sidebar's BALANCE — that
    /// is a running total across all time and also nets off entertainment purchases. Two similar
    /// numbers a glance apart is exactly the confusion this page has already had reported once
    /// (the 2026-07-23 drift-days report).</summary>
    private static StackPanel ScoreCard(ReportData.PeriodTotals totals, string periodName)
    {
        var card = new StackPanel { Spacing = 2 };
        card.Children.Add(Caption($"SCORE EARNED — {periodName}"));
        card.Children.Add(new TextBlock
        {
            Text = totals.Score.ToString(),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            Foreground = ScoreBrush(totals.Score),
        });
        // Day-offs joined this line 2026-08-13, folded into the existing period-scoped card
        // rather than a separate one, after the first attempt (a standalone always-three-numbers
        // card) was corrected: "everything besides the diary should update based on the chosen
        // timescale... the same about the day-offs statistics", "we do not need an additional
        // card for it". Manually marked via Schedule's "Day off" button only — never a plan's
        // recurring weekday rest days (see ScoreService.ManuallyMarkedDaysOff).
        var dayOffLabel = totals.DayOffs == 1 ? "1 day off marked" : $"{totals.DayOffs} days off marked";
        card.Children.Add(Dim($"{totals.Done}/{totals.Total} tasks · " +
                              $"{ReportData.FmtHours(totals.OnMin)} on-plan · " +
                              $"{ReportData.FmtHours(totals.OffMin)} off-plan · {dayOffLabel}"));
        return card;
    }

    /// <summary>Rule-of-thumb suggestions, over the same period as everything else above the
    /// diary. Was hardcoded to this week no matter what the selector said, so switching to Year
    /// left advice underneath it that was still describing the last few days.</summary>
    private static StackPanel InsightsPanel(ReportData.PeriodTotals totals, SqliteConnection conn,
        ScoreService score)
    {
        var hints = ReportExport.Suggestions(
            totals.OnMin, totals.OffMin,
            ReportData.TopDistractions(_period, conn, score));
        var hintPanel = new StackPanel { Spacing = 6 };
        foreach (var hint in hints)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new TextBlock
            {
                Text = "→",
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            });
            row.Children.Add(new TextBlock
            {
                Text = hint,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 720,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            hintPanel.Children.Add(row);
        }
        return hintPanel;
    }
}

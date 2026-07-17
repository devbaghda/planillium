using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

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

    // Centers ContentColumn explicitly instead of relying on
    // HorizontalAlignment/MaxWidth: RootScroller's own ActualWidth is set
    // top-down by the window/nav pane, so it's unaffected by how tall this
    // page's own scrollable content is on any given day — see the XAML
    // comment for why the alignment-based approaches didn't hold still.
    private void RootScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        const double maxContentWidth = 880;
        var available = e.NewSize.Width - RootScroller.Padding.Left - RootScroller.Padding.Right;
        var width = Math.Min(maxContentWidth, Math.Max(0, available));
        ContentColumn.Width = width;
        ContentColumn.Margin = new Thickness(Math.Max(0, (available - width) / 2), 0, 0, 0);
    }

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
        _diaryLiveRefresh?.Stop();
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

    private void Render()
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
            var weekStats = ReportData.WeekStats(score);
            var todayStat = weekStats[^1];
            var today = DateOnly.FromDateTime(DateTime.Today);

            Body.Children.Add(Card(ScoreCard(todayStat)));
            if (ExclusionImpactCard(plans, score, today) is { } exclusionImpact)
                Body.Children.Add(Card(exclusionImpact));

            // ── summary table ─────────────────────────────────────────────
            Body.Children.Add(Section(periodName));
            if (_period is ReportPeriod.Day or ReportPeriod.Week)
                Body.Children.Add(Card(DayTable(weekStats)));
            else
            {
                var buckets = _period == ReportPeriod.Month
                    ? ReportData.MonthBuckets(db.Conn) : ReportData.YearBuckets(db.Conn);
                // Month always has at least this-week's row seeded in,
                // but Year only gets rows for months that actually have
                // data — a brand-new install (or a period with zero
                // history) renders just a bare header row otherwise,
                // which reads as broken rather than simply empty.
                Body.Children.Add(buckets.Count == 0
                    ? Dim("No activity logged yet.")
                    : Card(BucketTable(buckets)));
            }

            // ── top distractions (grouped: "Chrome - YouTube") ────────────
            Body.Children.Add(Section($"TOP DISTRACTIONS — {periodName}"));
            var distractions = ReportData.TopDistractions(_period, db.Conn);
            if (distractions.Count == 0)
                Body.Children.Add(Dim("No off-plan time logged. Impressive."));
            else
                Body.Children.Add(Card(DistractionList(distractions)));

            // ── time by app (expandable groups) ───────────────────────────
            Body.Children.Add(Section($"TIME BY APP — {periodName}"));
            // Top 3 show by default; the rest sit behind "Show more", so pull a
            // generous slice rather than the default handful — the point of the
            // expander is to reveal the full picture on demand.
            var breakdown = ReportData.AppBreakdown(_period, db.Conn, limit: 100);
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

            Body.Children.Add(Section("INSIGHTS"));
            Body.Children.Add(Card(InsightsPanel(weekStats, db.Conn)));

            BuildDiarySection(today, score);
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.Render", ex);
            Body.Children.Add(new TextBlock
            {
                Text = "Couldn't load report data: " + ex.Message,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    /// <summary>Today's score, always today regardless of the selected period.</summary>
    private static StackPanel ScoreCard(ReportData.DayStat todayStat)
    {
        var card = new StackPanel { Spacing = 2 };
        card.Children.Add(Caption("TODAY'S SCORE"));
        card.Children.Add(new TextBlock
        {
            Text = todayStat.Score.ToString(),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            Foreground = ScoreBrush(todayStat.Score),
        });
        card.Children.Add(Dim($"{todayStat.Done}/{todayStat.Total} tasks · " +
                              $"{todayStat.OnMin}m on-plan · {todayStat.OffMin}m off-plan"));
        return card;
    }

    /// <summary>
    /// How far excluded days + overdue have pushed each plan past where a
    /// zero-exclusion, nothing-ever-overdue schedule would be. Approximation,
    /// not full history: it applies TODAY'S excluded-day pattern back across
    /// the whole timeline, so a pattern widened partway through (2 days/week
    /// -> 3) reads as if the wider pattern had always applied, not just from
    /// the week it changed. Null when there's nothing to report.
    /// Deliberately named apart from "drift" (used by the Plans page card and
    /// the sidebar status line for a different, simpler comparison — current
    /// finish date vs. originally-due finish date, via <see cref="Plan.DriftDays"/>)
    /// — the two numbers can legitimately differ, and sharing a name made
    /// that read as a contradiction rather than two distinct measurements
    /// (round-4 audit finding).
    /// </summary>
    private static StackPanel? ExclusionImpactCard(List<Plan> plans, ScoreService score, DateOnly today)
    {
        var overdueCount = score.OverdueAsOf(today).Count;
        var impactLines = new List<string>();
        foreach (var plan in plans.Where(p => p.PlanDay >= 1))
        {
            var naiveDate = plan.StartDateParsed.AddDays(plan.PlanDay - 1);
            var shiftDays = today.DayNumber - naiveDate.DayNumber;
            if (shiftDays > 0)
                impactLines.Add($"{plan.Name}: day {plan.PlanDay} landed on {today:dd.MM} — " +
                                $"{shiftDays} day(s) later than a straight count from " +
                                $"{plan.StartDateParsed:dd.MM.yyyy} would put it.");
        }
        if (impactLines.Count == 0 && overdueCount == 0) return null;

        var card = new StackPanel { Spacing = 4 };
        card.Children.Add(Caption("EXCLUSION IMPACT"));
        foreach (var line in impactLines)
            card.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        if (overdueCount > 0)
            card.Children.Add(new TextBlock
            {
                Text = $"{overdueCount} task(s) currently overdue.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            });
        return card;
    }

    /// <summary>Week-based rule-of-thumb suggestions, same period as the score card.</summary>
    private static StackPanel InsightsPanel(List<ReportData.DayStat> weekStats, SqliteConnection conn)
    {
        var hints = ReportExport.Suggestions(
            weekStats.Sum(s => s.OnMin), weekStats.Sum(s => s.OffMin),
            ReportData.TopDistractions(ReportPeriod.Week, conn));
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

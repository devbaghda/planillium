using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class ReportsPage : Page
{
    // Survives navigation: come back to Reports and it's still on your period.
    private static ReportPeriod _period = ReportPeriod.Week;

    // Survives navigation the same way — leave it on a past day, come back,
    // it's still there. Resets to today only via the "Today" button.
    private static DateOnly _diaryDate = DateOnly.FromDateTime(DateTime.Today);

    // Diary search text, same survives-navigation treatment.
    private static string _diarySearch = "";

    private static readonly (string Label, ReportPeriod Period)[] PeriodOpts =
    {
        ("Day", ReportPeriod.Day), ("Week", ReportPeriod.Week),
        ("Month", ReportPeriod.Month), ("Year", ReportPeriod.Year),
    };

    public ReportsPage()
    {
        InitializeComponent();
        BuildPeriodBar();
        Loaded += (_, _) => Render();
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
        {
            var btn = new ToggleButton
            {
                Content = label,
                Tag = period,
                IsChecked = period == _period,
                MinWidth = 72,
            };
            btn.Click += (s, _) =>
            {
                _period = (ReportPeriod)((ToggleButton)s).Tag;
                foreach (var child in PeriodBar.Children.OfType<ToggleButton>())
                    child.IsChecked = (ReportPeriod)child.Tag == _period;
                Render();
            };
            PeriodBar.Children.Add(btn);
        }
    }

    private void Render()
    {
        Body.Children.Clear();
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
            if (DriftCard(plans, score, today) is { } drift)
                Body.Children.Add(Card(drift));

            // ── summary table ─────────────────────────────────────────────
            Body.Children.Add(Section(periodName));
            Body.Children.Add(Card(_period is ReportPeriod.Day or ReportPeriod.Week
                ? DayTable(weekStats)
                : BucketTable(ReportData.Buckets(_period))));

            // ── top distractions (grouped: "Chrome - YouTube") ────────────
            Body.Children.Add(Section($"TOP DISTRACTIONS — {periodName}"));
            var distractions = ReportData.TopDistractions(_period);
            if (distractions.Count == 0)
                Body.Children.Add(Dim("No off-plan time logged. Impressive."));
            else
                Body.Children.Add(Card(DistractionList(distractions)));

            // ── time by app (expandable groups) ───────────────────────────
            Body.Children.Add(Section($"TIME BY APP — {periodName}"));
            var breakdown = ReportData.AppBreakdown(_period);
            if (breakdown.Count == 0)
                Body.Children.Add(Dim("No activity logged yet."));
            else
                Body.Children.Add(Card(AppBreakdownPanel(breakdown)));

            Body.Children.Add(Section("INSIGHTS"));
            Body.Children.Add(Card(InsightsPanel(weekStats)));

            BuildDiarySection(today);
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
    /// </summary>
    private static StackPanel? DriftCard(List<Plan> plans, ScoreService score, DateOnly today)
    {
        var overdueCount = score.OverdueAsOf(today).Count;
        var driftLines = new List<string>();
        foreach (var plan in plans.Where(p => p.PlanDay >= 1))
        {
            var naiveDate = plan.StartDateParsed.AddDays(plan.PlanDay - 1);
            var shiftDays = today.DayNumber - naiveDate.DayNumber;
            if (shiftDays > 0)
                driftLines.Add($"{plan.Name}: day {plan.PlanDay} landed on {today:dd.MM} — " +
                                $"{shiftDays} day(s) later than a straight count from " +
                                $"{plan.StartDateParsed:dd.MM.yyyy} would put it.");
        }
        if (driftLines.Count == 0 && overdueCount == 0) return null;

        var drift = new StackPanel { Spacing = 4 };
        drift.Children.Add(Caption("SCHEDULE DRIFT"));
        foreach (var line in driftLines)
            drift.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        if (overdueCount > 0)
            drift.Children.Add(new TextBlock
            {
                Text = $"{overdueCount} task(s) currently overdue.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            });
        return drift;
    }

    /// <summary>Week-based rule-of-thumb suggestions, same period as the score card.</summary>
    private static StackPanel InsightsPanel(List<ReportData.DayStat> weekStats)
    {
        var hints = ReportExport.Suggestions(
            weekStats.Sum(s => s.OnMin), weekStats.Sum(s => s.OffMin),
            ReportData.TopDistractions(ReportPeriod.Week));
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

    /// <summary>
    /// Diary search/list section — one day by default; searching widens to
    /// everything still retained (Database.DiaryRetentionDays) instead of
    /// just the day on screen. Kept as one method (not further split) since
    /// its closures share a lot of local state (selection, search text,
    /// the toolbar) that's specific to this one widget.
    /// </summary>
    private void BuildDiarySection(DateOnly today)
    {
        Body.Children.Add(DiaryHeader());

        var searchBox = new TextBox
        {
            PlaceholderText = $"Search the last {Database.DiaryRetentionDays} days' diary (app or description)…",
            Text = _diarySearch,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Body.Children.Add(searchBox);

        // Mark-selected toolbar — built once (not on every keystroke) so
        // its buttons don't get re-wired constantly; UpdateMarkToolbar()
        // just flips enabled/label state as the selection changes.
        var selectedIds = new HashSet<long>();
        var lastRows = new List<ReportData.DiaryEntry>();

        var markToolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        // IsThreeState so it can show "some but not all selected" as a
        // dash rather than lying with a plain checked/unchecked state.
        var selectAllBox = new CheckBox { Content = "Select all", IsThreeState = true, IsEnabled = false };
        var selectedLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var markOnBtn = new Button { Content = "Mark on-plan", IsEnabled = false };
        var markOffBtn = new Button { Content = "Mark off-plan", IsEnabled = false };
        var markNeutralBtn = new Button { Content = "Mark neutral", IsEnabled = false };
        markToolbar.Children.Add(selectAllBox);
        markToolbar.Children.Add(selectedLabel);
        markToolbar.Children.Add(markOnBtn);
        markToolbar.Children.Add(markOffBtn);
        markToolbar.Children.Add(markNeutralBtn);
        Body.Children.Add(markToolbar);

        var diaryResults = new StackPanel { Spacing = 0 };
        Body.Children.Add(diaryResults);

        const int maxSearchResults = 300;

        // Guards Checked/Unchecked below from firing when UpdateMarkToolbar
        // sets IsChecked itself to reflect the current selection — without
        // it, syncing the box would immediately re-trigger select-all/none.
        var syncingSelectAll = false;

        void UpdateMarkToolbar()
        {
            var n = selectedIds.Count;
            selectedLabel.Text = n > 0 ? $"{n} selected" : "";
            markOnBtn.IsEnabled = markOffBtn.IsEnabled = markNeutralBtn.IsEnabled = n > 0;

            syncingSelectAll = true;
            selectAllBox.IsEnabled = lastRows.Count > 0;
            selectAllBox.IsChecked = lastRows.Count == 0 || n == 0 ? false
                : n == lastRows.Count ? true
                : null;
            syncingSelectAll = false;
        }

        void RenderDiaryResults()
        {
            diaryResults.Children.Clear();
            var q = _diarySearch.Trim();
            var searching = q.Length > 0;
            var rows = searching
                ? ReportData.DiaryInRange(today.AddDays(-(Database.DiaryRetentionDays - 1)), today)
                : ReportData.DiaryInRange(_diaryDate, _diaryDate);
            var filtered = !searching ? rows : rows.Where(e =>
                AppNames.Label(e.Window).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (e.Desc is { Length: > 0 } d && d.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                e.Cat.Replace('_', ' ').Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtered.Count == 0)
            {
                diaryResults.Children.Add(Dim(searching
                    ? $"No entries match your search in the last {Database.DiaryRetentionDays} days."
                    : (_diaryDate == today
                        ? "No diary entries yet today. Tracking runs 06:00–20:00."
                        : "No diary entries on this day.")));
                lastRows.Clear();
                selectedIds.Clear();
                UpdateMarkToolbar();
                return;
            }
            if (searching && filtered.Count > maxSearchResults)
            {
                diaryResults.Children.Add(Dim(
                    $"{filtered.Count} matches — showing the {maxSearchResults} most recent."));
                filtered = filtered.Take(maxSearchResults).ToList();
            }
            lastRows = filtered;
            selectedIds.RemoveWhere(id => !filtered.Any(e => e.Id == id));
            UpdateMarkToolbar();
            diaryResults.Children.Add(Card(DiaryList(filtered, selectedIds, UpdateMarkToolbar, showDate: searching)));
        }
        RenderDiaryResults();

        void MarkSelected(string category)
        {
            if (selectedIds.Count == 0) return;
            try
            {
                using var db = new Database();
                var learned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in selectedIds)
                {
                    var row = lastRows.FirstOrDefault(e => e.Id == id);
                    if (row.Id != id) continue;
                    db.UpdateDiaryEntry(id, row.Start, row.End, row.Dur, category, row.Desc);
                    var keyword = AppNames.Sub(row.Window) ?? AppNames.Group(row.Window);
                    if (keyword is { Length: > 0 } && keyword != "—") learned.Add(keyword);
                }
                foreach (var keyword in learned)
                    ConfigService.LearnActivityRule(keyword, category);
                if (learned.Count > 0)
                    (App.MainWindow as MainWindow)?.RestartTracker();
            }
            catch (Exception ex)
            {
                Log.Error("ReportsPage.MarkSelected", ex);
            }
            selectedIds.Clear();
            RenderDiaryResults();
        }
        markOnBtn.Click += (_, _) => MarkSelected("on_plan");
        markOffBtn.Click += (_, _) => MarkSelected("off_plan");
        markNeutralBtn.Click += (_, _) => MarkSelected("neutral");

        selectAllBox.Checked += (_, _) =>
        {
            if (syncingSelectAll) return;
            foreach (var row in lastRows) selectedIds.Add(row.Id);
            RenderDiaryResults();
        };
        selectAllBox.Unchecked += (_, _) =>
        {
            if (syncingSelectAll) return;
            selectedIds.Clear();
            RenderDiaryResults();
        };
        // A three-state box that's currently indeterminate (partial
        // selection) cycles to Unchecked on the next click, same as if
        // it were fully checked — clicking a partial state clears it.
        selectAllBox.Indeterminate += (_, _) =>
        {
            if (syncingSelectAll) return;
            selectedIds.Clear();
            RenderDiaryResults();
        };

        // Filters in place (doesn't touch searchBox itself) so typing
        // keeps focus/cursor position instead of losing it every keystroke.
        searchBox.TextChanged += (_, _) =>
        {
            _diarySearch = searchBox.Text;
            RenderDiaryResults();
        };
    }

    // ── summary tables ───────────────────────────────────────────────────

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
                s.Date.ToString("ddd dd.MM", CultureInfo.InvariantCulture),
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

    private static StackPanel AppBreakdownPanel(List<(string App, ReportData.AppUsage Usage)> breakdown)
    {
        var maxTotal = Math.Max(breakdown[0].Usage.Total, 1);
        var panel = new StackPanel { Spacing = 4 };
        foreach (var (app, usage) in breakdown)
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
                panel.Children.Add(header);
                continue;
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
            header.Tapped += (_, _) =>
            {
                var expanded = subPanel.Visibility == Visibility.Visible;
                subPanel.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
                chevron.Glyph = expanded ? "\uE70D" : "\uE70E";
            };

            panel.Children.Add(header);
            panel.Children.Add(subPanel);
        }
        return panel;
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
            (u.On, "SystemFillColorSuccessBrush"),
            (u.Off, "SystemFillColorCriticalBrush"),
            (u.Neutral, "AccentFillColorDefaultBrush"),
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
                Glyph = "\uE70D",
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

    // ── diary ────────────────────────────────────────────────────────────

    private Grid DiaryHeader()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var searching = _diarySearch.Trim().Length > 0;
        var label = searching ? $"TIME DIARY · SEARCH (LAST {Database.DiaryRetentionDays} DAYS)"
            : _diaryDate == today ? "TIME DIARY · TODAY"
            : $"TIME DIARY · {_diaryDate:ddd dd.MM.yyyy}".ToUpperInvariant();

        var grid = new Grid { Margin = new Thickness(2, 22, 0, 8), ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var caption = Section(label);
        caption.Margin = new Thickness(0);
        Grid.SetColumn(caption, 0);
        grid.Children.Add(caption);

        Button NavBtn(object content, string name, Action onClick, bool enabled = true)
        {
            var btn = new Button
            {
                Content = content,
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 0,
                MinHeight = 0,
                IsEnabled = enabled,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, name);
            btn.Click += (_, _) => onClick();
            return btn;
        }

        var prevGlyph = new FontIcon { Glyph = "", FontSize = 12 };
        var nextGlyph = new FontIcon { Glyph = "", FontSize = 12 };
        var prev = NavBtn(prevGlyph, "Previous day",
            () => { _diaryDate = _diaryDate.AddDays(-1); Render(); }, enabled: !searching);
        var todayBtn = NavBtn("Today", "Jump to today",
            () => { _diaryDate = today; Render(); }, enabled: !searching && _diaryDate != today);
        var next = NavBtn(nextGlyph, "Next day",
            () => { _diaryDate = _diaryDate.AddDays(1); Render(); }, enabled: !searching && _diaryDate < today);

        Grid.SetColumn(prev, 1); Grid.SetColumn(todayBtn, 2); Grid.SetColumn(next, 3);
        grid.Children.Add(prev);
        grid.Children.Add(todayBtn);
        grid.Children.Add(next);
        return grid;
    }

    private StackPanel DiaryList(
        List<ReportData.DiaryEntry> diary,
        HashSet<long> selectedIds, Action onSelectionChanged, bool showDate = false)
    {
        var list = new StackPanel { Spacing = 4 };
        foreach (var (id, date, start, end, dur, cat, window, desc) in diary)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showDate ? 150 : 110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var select = new CheckBox
            {
                MinWidth = 0, VerticalAlignment = VerticalAlignment.Center,
                IsChecked = selectedIds.Contains(id),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(select, "Select this entry");
            select.Checked += (_, _) => { selectedIds.Add(id); onSelectionChanged(); };
            select.Unchecked += (_, _) => { selectedIds.Remove(id); onSelectionChanged(); };
            Grid.SetColumn(select, 0);
            row.Children.Add(select);

            var time = Dim(showDate ? $"{date:dd.MM} · {start} → {end}" : $"{start} → {end}");
            var catText = new TextBlock
            {
                Text = cat.Replace('_', '-'),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources[cat switch
                {
                    "on_plan" => "SystemFillColorSuccessBrush",
                    "off_plan" => "SystemFillColorCriticalBrush",
                    "idle" => "SystemFillColorCautionBrush",
                    "paid" => "AccentTextFillColorPrimaryBrush",
                    _ => "TextFillColorSecondaryBrush",
                }],
                VerticalAlignment = VerticalAlignment.Center,
            };
            // "Application - name" display, same grouping labels as above.
            var windowLabel = AppNames.Label(window);
            var what = new TextBlock
            {
                Text = desc is { Length: > 0 } ? $"“{desc}” ({dur}m)" : $"{windowLabel} ({dur}m)",
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontStyle = desc is { Length: > 0 }
                    ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            };
            var edit = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(6),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ((FontIcon)edit.Content).Glyph = "";
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(edit, "Edit diary entry");
            edit.Click += async (_, _) =>
            {
                if (await Dialogs.EditDiaryEntryDialog.ShowAsync(XamlRoot, id, start, end, dur, cat, desc))
                    Render();
            };
            Grid.SetColumn(time, 1);
            Grid.SetColumn(catText, 2);
            Grid.SetColumn(what, 3);
            Grid.SetColumn(edit, 4);
            row.Children.Add(time);
            row.Children.Add(catText);
            row.Children.Add(what);
            row.Children.Add(edit);
            list.Children.Add(row);
        }
        return list;
    }


    // ── styling helpers ───────────────────────────────────────────────────

    private static Brush ScoreBrush(int score) =>
        (Brush)Application.Current.Resources[
            score >= 20 ? "SystemFillColorSuccessBrush"
            : score < 0 ? "SystemFillColorCriticalBrush"
            : "SystemFillColorCautionBrush"];

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 60,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        Margin = new Thickness(2, 22, 0, 8),
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 50,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
    };

    private static TextBlock Dim(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private static Border Card(UIElement child) => new()
    {
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18, 14, 18, 14),
        Child = child,
    };
}

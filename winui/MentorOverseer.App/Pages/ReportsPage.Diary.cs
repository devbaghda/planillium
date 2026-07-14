using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

// The time-diary section (search, mark-selected, edit/split) — see
// ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    // Survives navigation the same way as _period — leave it on a past day,
    // come back, it's still there. Resets to today only via the "Today" button.
    private static DateOnly _diaryDate = DateOnly.FromDateTime(DateTime.Today);

    // Diary search text, same survives-navigation treatment.
    private static string _diarySearch = "";

    // The debounce timer itself is created once and reused across renders
    // (NavigationCacheMode="Enabled" reuses this page instance, and
    // BuildDiarySection runs on every Render — page nav, period switch, diary
    // day nav). Only _diarySearchDebounceAction is reassigned per render, so
    // a timer armed just before a re-render fires into the CURRENT render's
    // RenderDiaryResults closure instead of a stale one from a torn-down
    // diary panel (round-4 audit finding).
    private DispatcherQueueTimer? _diarySearchDebounce;
    private Action? _diarySearchDebounceAction;

    /// <summary>
    /// Diary search/list section — one day by default; searching widens to
    /// everything still retained (ConfigService.DiaryRetentionDays(),
    /// user-configurable — see Settings) instead of just the day on screen.
    /// Kept as one method (not further split) since
    /// its closures share a lot of local state (selection, search text,
    /// the toolbar) that's specific to this one widget.
    /// </summary>
    private void BuildDiarySection(DateOnly today, ScoreService score)
    {
        Body.Children.Add(DiaryHeader());

        // Reflections (the evening review's one-line answers) were
        // previously write-only — saved but never shown back anywhere in
        // the app (2026-07-09 audit finding #12). Shown for whichever day
        // the diary is currently viewing, same as everything else on this
        // section.
        if (_diarySearch.Trim().Length == 0 && score.LoadReflection(_diaryDate) is { Length: > 0 } reflection)
            Body.Children.Add(ReflectionCallout(reflection));

        var searchBox = new TextBox
        {
            PlaceholderText = $"Search the last {ConfigService.DiaryRetentionDays()} days' diary (app or description)…",
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

        // Own scroll box, both directions: a long day (or a search hitting the
        // full retention window) used to keep growing the whole Reports page
        // and could push row content wider than the page, which then either
        // clipped off-screen or shifted the page's own measured width from
        // one day to the next. Bounding it here keeps the page's width and
        // the diary's own scrolling independent of how much/how wide the
        // content for a given day happens to be.
        // MinWidth pins the row grids to a sane layout width even though the
        // scroller offers them unconstrained width in the scrollable
        // direction — without it, a Grid measured with infinite width can
        // collapse its Star column instead of sizing sensibly.
        var diaryResults = new StackPanel { Spacing = 0, MinWidth = 820 };
        var diaryScroller = new ScrollViewer
        {
            MaxHeight = 520,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = diaryResults,
        };
        Body.Children.Add(diaryScroller);

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
                ? ReportData.DiaryInRange(today.AddDays(-(ConfigService.DiaryRetentionDays() - 1)), today)
                : ReportData.DiaryInRange(_diaryDate, _diaryDate);
            var filtered = !searching ? rows : rows.Where(e =>
                AppNames.Label(e.Window).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (e.Desc is { Length: > 0 } d && d.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                e.Cat.Replace('_', ' ').Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtered.Count == 0)
            {
                diaryResults.Children.Add(Dim(searching
                    ? $"No entries match your search in the last {ConfigService.DiaryRetentionDays()} days."
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
                    if (row is null || row.Id != id) continue;
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
        // Debounced: each keystroke would otherwise open a fresh connection
        // and scan the full retention window synchronously on the UI thread
        // — fine at personal-DB scale today, but a free anti-pattern to fix
        // while the file's already open, before it accumulates enough
        // history to actually be felt.
        _diarySearchDebounceAction = RenderDiaryResults;
        if (_diarySearchDebounce is null)
        {
            _diarySearchDebounce = DispatcherQueue.CreateTimer();
            _diarySearchDebounce.Interval = TimeSpan.FromMilliseconds(250);
            _diarySearchDebounce.IsRepeating = false;
            _diarySearchDebounce.Tick += (_, _) => _diarySearchDebounceAction?.Invoke();
        }
        var searchDebounce = _diarySearchDebounce;
        searchBox.TextChanged += (_, _) =>
        {
            _diarySearch = searchBox.Text;
            searchDebounce.Stop();
            searchDebounce.Start();
        };
    }

    // ── diary ────────────────────────────────────────────────────────────

    private static Border ReflectionCallout(string text) => new()
    {
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(12, 8, 12, 8),
        Margin = new Thickness(0, 0, 0, 10),
        Child = new TextBlock
        {
            Text = "💭 " + text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        },
    };

    private Grid DiaryHeader()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var searching = _diarySearch.Trim().Length > 0;
        var caption = searching
            ? $"TIME DIARY · SEARCH (LAST {ConfigService.DiaryRetentionDays()} DAYS)"
            : "TIME DIARY";

        var grid = new Grid { Margin = new Thickness(2, 22, 0, 8), ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

        // Today is now the jump-shortcut on the left, next to the caption —
        // it used to sit between the prev/next arrows, which read as a state
        // indicator rather than the button it actually is. The current date
        // now lives on the right, between the arrows that move it.
        var todayBtn = NavBtn("Today", "Jump to today",
            () => { _diaryDate = today; Render(); }, enabled: !searching && _diaryDate != today);
        var captionText = Section(caption);
        captionText.Margin = new Thickness(0);
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        left.Children.Add(todayBtn);
        left.Children.Add(captionText);
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var prevGlyph = new FontIcon { Glyph = "", FontSize = 12 };
        var nextGlyph = new FontIcon { Glyph = "", FontSize = 12 };
        var prev = NavBtn(prevGlyph, "Previous day",
            () => { _diaryDate = _diaryDate.AddDays(-1); Render(); }, enabled: !searching);
        // A calendar picker, not just a label — jumping more than a few days
        // used to mean clicking prev/next repeatedly. DateFormat is spelled
        // out numerically (no month/weekday names) for the same reason the
        // rest of the diary uses InvariantCulture: the OS locale is Russian,
        // and name-based tokens would render in Russian instead of digits.
        static DateTimeOffset ToOffset(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var datePicker = new CalendarDatePicker
        {
            Date = ToOffset(_diaryDate),
            MinDate = ToOffset(today.AddDays(-ConfigService.DiaryRetentionDays())),
            MaxDate = ToOffset(today),
            DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}",
            IsEnabled = !searching,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0,
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(datePicker, "Jump to date");
        datePicker.DateChanged += (_, e) =>
        {
            if (e.NewDate is { } picked && DateOnly.FromDateTime(picked.DateTime) != _diaryDate)
            {
                _diaryDate = DateOnly.FromDateTime(picked.DateTime);
                Render();
            }
        };

        var next = NavBtn(nextGlyph, "Next day",
            () => { _diaryDate = _diaryDate.AddDays(1); Render(); }, enabled: !searching && _diaryDate < today);

        Grid.SetColumn(prev, 1); Grid.SetColumn(datePicker, 2); Grid.SetColumn(next, 3);
        grid.Children.Add(prev);
        grid.Children.Add(datePicker);
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
            // Fixed pixel width, not Auto/Star — the page body is a
            // MaxWidth+Center StackPanel (ReportsPage.xaml), which sizes
            // itself to its widest child's natural content width rather
            // than a fixed width. An Auto or capped-MaxWidth column still
            // measures narrower for a short entry than a long one, so the
            // whole centered page visibly grew/shrank per day. A fixed
            // width makes every row occupy the exact same space regardless
            // of that day's content; overflow past it ellipsis-trims with
            // a tooltip for the full text.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(480) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var select = new CheckBox
            {
                MinWidth = 0, VerticalAlignment = VerticalAlignment.Center,
                IsChecked = selectedIds.Contains(id),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(select,
                $"Select entry: {AppNames.Label(window)}, {start}–{end}");
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
            var whatText = desc is { Length: > 0 } ? $"“{desc}” ({dur}m)" : $"{windowLabel} ({dur}m)";
            var what = new TextBlock
            {
                Text = whatText,
                FontStyle = desc is { Length: > 0 }
                    ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(what, whatText);
            var edit = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(6),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(edit, "Edit diary entry");
            edit.Click += async (_, _) =>
            {
                if (await Dialogs.EditDiaryEntryDialog.ShowAsync(XamlRoot, id, start, end, dur, cat, desc))
                    Render();
            };
            var split = new Button
            {
                Content = "Split",
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(split, "Split into several activities");
            split.Click += async (_, _) =>
            {
                if (await Dialogs.SplitDiaryEntryDialog.ShowAsync(XamlRoot, id, date, start, end, dur, cat, window, desc))
                    Render();
            };
            Grid.SetColumn(time, 1);
            Grid.SetColumn(catText, 2);
            Grid.SetColumn(what, 3);
            Grid.SetColumn(edit, 4);
            Grid.SetColumn(split, 5);
            row.Children.Add(time);
            row.Children.Add(catText);
            row.Children.Add(what);
            row.Children.Add(edit);
            row.Children.Add(split);
            list.Children.Add(row);
        }
        return list;
    }
}

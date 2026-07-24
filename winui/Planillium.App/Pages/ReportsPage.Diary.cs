using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// The time-diary section (search, mark-selected, edit/split) — see
// ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    // Survives navigation the same way as _period — leave it on a past day,
    // come back, it's still there. Resets to today only via the "Today" button.
    private static DateOnly _diaryDate = DateOnly.FromDateTime(DateTime.Today);

    // Diary search text, same survives-navigation treatment.
    private static string _diarySearch = "";

    // Column filters (2026-07-22 request, app/page split 2026-07-23) — independent of the
    // free-text search above and combinable with it: null means "no filter." App/page match
    // AppNames.Group/Sub respectively (the same values the row's own App/Page columns show —
    // e.g. app="Chrome", page="GitHub", or app="Telegram", page="Liza Ponomarenko"). Same
    // survives-navigation treatment as _diarySearch/_diaryDate.
    private static string? _diaryCategoryFilter;
    private static string? _diaryAppFilter;
    private static string? _diaryPageFilter;

    // 2026-07-23 request: filters/search normally scope to whichever single day the date
    // nav/picker is showing; this widens that scope to the full retention window (same range
    // free-text search already uses) without requiring a search term. Same survives-navigation
    // treatment as the other diary state.
    private static bool _diaryAllTime;

    // The debounce timer itself is created once and reused across renders
    // (NavigationCacheMode="Enabled" reuses this page instance, and
    // BuildDiarySection runs on every Render — page nav, period switch, diary
    // day nav). Only _diarySearchDebounceAction is reassigned per render, so
    // a timer armed just before a re-render fires into the CURRENT render's
    // RenderDiaryResults closure instead of a stale one from a torn-down
    // diary panel (round-4 audit finding).
    private DispatcherQueueTimer? _diarySearchDebounce;
    private Action? _diarySearchDebounceAction;

    // Diary rows previously only ever appeared on navigation/search/edit —
    // sitting on today's diary while the tracker keeps logging in the
    // background never showed anything new until the page was left and
    // reopened (2026-07-15 bug report). Polls the same read-only query the
    // page already runs, so it's only worth doing while looking at today
    // and not mid-search (an active search already re-renders on its own
    // debounce, and ticking underneath a many-day search result would just
    // be wasted work). Stopped in OnNavigatedFrom so it doesn't keep
    // querying the DB in the background once the page is cached-but-hidden.
    private DispatcherQueueTimer? _diaryLiveRefresh;
    private Action? _diaryLiveRefreshAction;
    private static readonly TimeSpan DiaryLiveRefreshInterval = TimeSpan.FromSeconds(30);

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
        // Placeholder text alone isn't exposed to screen readers as an accessible
        // name — a static name here (audit finding #16) since the placeholder text
        // itself already varies with the retention setting.
        AutomationProperties.SetName(searchBox, "Search the time diary");
        Body.Children.Add(searchBox);

        // Category/app/page filters (2026-07-22, app/page split 2026-07-23) — independent of
        // the search box above, apply whether or not a search is active. Guards
        // SelectionChanged from firing when RenderDiaryResults below re-syncs these boxes'
        // SelectedItem to the persisted filter state (e.g. after the app/page lists are
        // rebuilt), same pattern as syncingSelectAll further down for the same reason.
        var syncingFilters = false;
        const string AllCategories = "All categories";
        const string AllApps = "All apps";
        const string AllPages = "All pages";

        var categoryBox = new ComboBox { PlaceholderText = "Category", MinWidth = 140 };
        categoryBox.Items.Add(AllCategories);
        foreach (var (label, _) in DiaryCategory.EditableOptions) categoryBox.Items.Add(label);
        AutomationProperties.SetName(categoryBox, "Filter diary by category");

        var appBox = new ComboBox { PlaceholderText = "App", MinWidth = 150 };
        AutomationProperties.SetName(appBox, "Filter diary by app");

        var pageBox = new ComboBox { PlaceholderText = "Page", MinWidth = 180 };
        AutomationProperties.SetName(pageBox, "Filter diary by page");

        var allTimeBox = new CheckBox { Content = "All time (not just this day)" };
        AutomationProperties.SetName(allTimeBox,
            $"Apply filters across the last {ConfigService.DiaryRetentionDays()} days instead of just this day");
        allTimeBox.IsChecked = _diaryAllTime;

        var clearFiltersBtn = new Button { Content = "Clear filters", Padding = new Thickness(8, 4, 8, 4) };

        var filterRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        filterRow.Children.Add(categoryBox);
        filterRow.Children.Add(appBox);
        filterRow.Children.Add(pageBox);
        filterRow.Children.Add(allTimeBox);
        filterRow.Children.Add(clearFiltersBtn);
        // This row's combined MinWidth (categoryBox+appBox+pageBox+checkbox+button, ~750-800px)
        // can exceed the actual content width at the app's enforced 900px window floor once the
        // NavigationView pane and page padding are subtracted — without this, "Clear filters"
        // and the "All time" checkbox can be clipped off-screen with no way to reach them
        // (2026-07-24 audit finding #1, a regression from the App/Page column split). Same
        // HorizontalScrollBarVisibility="Auto" treatment as diaryScroller below, just for one row.
        Body.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 0, 0, 8),
            Content = filterRow,
        });

        // Subtotal of whatever's currently filtered/shown — updated in RenderDiaryResults
        // below, right along with the list itself.
        var subtotalText = Dim("");
        subtotalText.Margin = new Thickness(0, 0, 0, 6);
        Body.Children.Add(subtotalText);

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
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
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
        // Widened from 820 (2026-07-23) — the App/Page column split added ~130px of row width.
        var diaryResults = new StackPanel { Spacing = 0, MinWidth = 950 };
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
            // "All time" (2026-07-23) reuses exactly the wide range free-text search already
            // used, just without requiring a search term — so a filter combo alone (e.g. "just
            // show me every Telegram/Liza Ponomarenko entry") can scope past the single day the
            // date nav is showing.
            var wideRange = searching || _diaryAllTime;
            // Matches the date-picker's own MinDate and Database.PruneAndRollupDiary's
            // actual cutoff (rows older than today − N are pruned, so today − N is the
            // oldest day still genuinely present) — this used to be one day short,
            // silently excluding the single oldest day still on disk (round-5 audit
            // finding #6).
            var rows = wideRange
                ? ReportData.DiaryInRange(today.AddDays(-ConfigService.DiaryRetentionDays()), today)
                : ReportData.DiaryInRange(_diaryDate, _diaryDate);

            // Rebuild the app/page filters' options from what's actually in the current scope
            // (today, or the whole range) rather than a fixed list — re-syncs all three boxes'
            // displayed selection to the persisted filter state at the same time, since the
            // app/page lists (and therefore whether the current filter values still appear in
            // view) can change on every call, not just when a filter itself changes. App
            // matches AppNames.Group (e.g. "Chrome", "Telegram"); Page matches AppNames.Sub
            // (e.g. "GitHub", "Liza Ponomarenko") — the same two values the row's own App/Page
            // columns show, split out 2026-07-23 so each is independently filterable.
            syncingFilters = true;
            var appsInView = rows.Select(e => AppNames.Group(e.Window))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
            appBox.Items.Clear();
            appBox.Items.Add(AllApps);
            foreach (var a in appsInView) appBox.Items.Add(a);
            var matchedApp = _diaryAppFilter is { } wantApp
                ? appsInView.FirstOrDefault(a => string.Equals(a, wantApp, StringComparison.OrdinalIgnoreCase))
                : null;
            appBox.SelectedItem = matchedApp ?? AllApps;
            _diaryAppFilter = matchedApp; // drops a filter whose app no longer appears in view

            var pagesInView = rows.Select(e => AppNames.Sub(e.Window))
                .Where(p => p is { Length: > 0 })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            pageBox.Items.Clear();
            pageBox.Items.Add(AllPages);
            foreach (var p in pagesInView) pageBox.Items.Add(p);
            var matchedPage = _diaryPageFilter is { } wantPage
                ? pagesInView.FirstOrDefault(p => string.Equals(p, wantPage, StringComparison.OrdinalIgnoreCase))
                : null;
            pageBox.SelectedItem = matchedPage ?? AllPages;
            _diaryPageFilter = matchedPage; // drops a filter whose page no longer appears in view

            categoryBox.SelectedItem = _diaryCategoryFilter is { } wantCat
                ? DiaryCategory.EditableOptions.FirstOrDefault(o => o.Value == wantCat).Label ?? AllCategories
                : AllCategories;
            allTimeBox.IsChecked = _diaryAllTime;
            syncingFilters = false;

            var filtered = rows.AsEnumerable();
            if (_diaryCategoryFilter is { } catFilter)
                filtered = filtered.Where(e => e.Cat == catFilter);
            if (_diaryAppFilter is { } appFilter)
                filtered = filtered.Where(e =>
                    string.Equals(AppNames.Group(e.Window), appFilter, StringComparison.OrdinalIgnoreCase));
            if (_diaryPageFilter is { } pageFilter)
                filtered = filtered.Where(e =>
                    string.Equals(AppNames.Sub(e.Window) ?? "", pageFilter, StringComparison.OrdinalIgnoreCase));
            if (searching)
                filtered = filtered.Where(e =>
                    AppNames.Label(e.Window).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (e.Desc is { Length: > 0 } d && d.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    e.Cat.Replace('_', ' ').Contains(q, StringComparison.OrdinalIgnoreCase));
            var filteredList = filtered.ToList();
            var filtersActive = _diaryCategoryFilter != null || _diaryAppFilter != null || _diaryPageFilter != null;

            var totalMin = filteredList.Sum(e => e.Dur);
            subtotalText.Text = filteredList.Count == 0
                ? ""
                : $"{filteredList.Count} entr{(filteredList.Count == 1 ? "y" : "ies")} · {FormatDuration(totalMin)} total";

            if (filteredList.Count == 0)
            {
                diaryResults.Children.Add(Dim(wideRange
                    ? $"No entries match your search/filters in the last {ConfigService.DiaryRetentionDays()} days."
                    : filtersActive
                        ? "No entries match the selected filter(s) on this day."
                        : (_diaryDate == today
                            ? "No diary entries yet today. Tracking runs 06:00–20:00."
                            : "No diary entries on this day.")));
                lastRows.Clear();
                selectedIds.Clear();
                UpdateMarkToolbar();
                return;
            }
            if (wideRange && filteredList.Count > maxSearchResults)
            {
                diaryResults.Children.Add(Dim(
                    $"{filteredList.Count} matches — showing the {maxSearchResults} most recent " +
                    "(subtotal above still covers all of them)."));
                filteredList = filteredList.Take(maxSearchResults).ToList();
            }
            lastRows = filteredList;
            selectedIds.RemoveWhere(id => !filteredList.Any(e => e.Id == id));
            UpdateMarkToolbar();
            diaryResults.Children.Add(Card(DiaryList(filteredList, selectedIds, UpdateMarkToolbar, showDate: wideRange)));
        }
        RenderDiaryResults();

        categoryBox.SelectionChanged += (_, _) =>
        {
            if (syncingFilters) return;
            var chosen = categoryBox.SelectedItem as string;
            _diaryCategoryFilter = chosen is null or AllCategories
                ? null
                : DiaryCategory.EditableOptions.FirstOrDefault(o => o.Label == chosen).Value;
            RenderDiaryResults();
        };
        appBox.SelectionChanged += (_, _) =>
        {
            if (syncingFilters) return;
            var chosen = appBox.SelectedItem as string;
            _diaryAppFilter = chosen is null or AllApps ? null : chosen;
            RenderDiaryResults();
        };
        pageBox.SelectionChanged += (_, _) =>
        {
            if (syncingFilters) return;
            var chosen = pageBox.SelectedItem as string;
            _diaryPageFilter = chosen is null or AllPages ? null : chosen;
            RenderDiaryResults();
        };
        // Unlike the category/app/page boxes, this needs a full Render() (not just
        // RenderDiaryResults()) — it changes wideMode, which DiaryHeader() also reads to decide
        // the "TIME DIARY · ALL TIME" caption and whether the date-nav arrows/picker are
        // enabled, and DiaryHeader is only rebuilt on a full Render.
        allTimeBox.Checked += (_, _) => { if (!syncingFilters) { _diaryAllTime = true; Render(); } };
        allTimeBox.Unchecked += (_, _) => { if (!syncingFilters) { _diaryAllTime = false; Render(); } };
        clearFiltersBtn.Click += (_, _) =>
        {
            _diaryCategoryFilter = null;
            _diaryAppFilter = null;
            _diaryPageFilter = null;
            _diaryAllTime = false;
            // The search box sits directly above this button and reads as part of the same
            // filter row — leaving it untouched made "Clear filters" look broken when a typed
            // search term kept the list scoped after a click (2026-07-23 UX re-audit).
            _diarySearch = "";
            // Full Render(), not RenderDiaryResults() — this button resets _diaryAllTime (and
            // now _diarySearch), both of which DiaryHeader()'s wideMode reads to decide the
            // "ALL TIME" caption and whether the date-nav arrows are enabled. The allTimeBox
            // checkbox's own handlers already call Render() for the same reason; this button
            // was the one sibling still calling the partial refresh and left the header stale.
            Render();
        };

        markOnBtn.Click += (_, _) => MarkSelectedDiaryRows(DiaryCategory.OnPlan, selectedIds, lastRows, RenderDiaryResults);
        markOffBtn.Click += (_, _) => MarkSelectedDiaryRows(DiaryCategory.OffPlan, selectedIds, lastRows, RenderDiaryResults);
        markNeutralBtn.Click += (_, _) => MarkSelectedDiaryRows(DiaryCategory.Neutral, selectedIds, lastRows, RenderDiaryResults);

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
        var searchDebounce = EnsureDiarySearchDebounceTimer();
        // Today's own rows are in scope whenever the single day shown IS today, or "All time"
        // is on (its wide range always includes today) — either way freshly-tracked rows for
        // today could appear while this section sits open.
        void SyncLiveRefresh()
        {
            if ((_diaryDate == today || _diaryAllTime) && _diarySearch.Trim().Length == 0)
                _diaryLiveRefresh?.Start();
            else
                _diaryLiveRefresh?.Stop();
        }
        searchBox.TextChanged += (_, _) =>
        {
            _diarySearch = searchBox.Text;
            searchDebounce.Stop();
            searchDebounce.Start();
            SyncLiveRefresh();
        };

        // Only re-poll while today's rows are actually in scope with no search active — a
        // past-only day view is finished history (nothing new will ever appear), and a search
        // already re-renders itself on its own typing debounce above.
        _diaryLiveRefreshAction = RenderDiaryResults;
        EnsureDiaryLiveRefreshTimer();
        SyncLiveRefresh();
    }

    /// <summary>Lazily creates the search-input debounce timer once, reused across renders
    /// (see the field's own doc comment) — pulled out of BuildDiarySection so that ~400-line
    /// method isn't also responsible for timer lifecycle bookkeeping (code-quality audit
    /// finding #7).</summary>
    private DispatcherQueueTimer EnsureDiarySearchDebounceTimer()
    {
        if (_diarySearchDebounce is null)
        {
            _diarySearchDebounce = DispatcherQueue.CreateTimer();
            _diarySearchDebounce.Interval = TimeSpan.FromMilliseconds(250);
            _diarySearchDebounce.IsRepeating = false;
            _diarySearchDebounce.Tick += (_, _) => _diarySearchDebounceAction?.Invoke();
        }
        return _diarySearchDebounce;
    }

    /// <summary>Lazily creates the "today's rows might still be growing" live-refresh
    /// timer once, reused across renders — see EnsureDiarySearchDebounceTimer's doc comment.</summary>
    private DispatcherQueueTimer EnsureDiaryLiveRefreshTimer()
    {
        if (_diaryLiveRefresh is null)
        {
            _diaryLiveRefresh = DispatcherQueue.CreateTimer();
            _diaryLiveRefresh.Interval = DiaryLiveRefreshInterval;
            _diaryLiveRefresh.IsRepeating = true;
            _diaryLiveRefresh.Tick += (_, _) => _diaryLiveRefreshAction?.Invoke();
        }
        return _diaryLiveRefresh;
    }

    /// <summary>The bulk "mark selected rows as on/off-plan/neutral" action — pulled out of
    /// BuildDiarySection as its own self-contained DB-transaction block (code-quality audit
    /// finding #7); takes the handful of closures it actually needs as parameters rather than
    /// capturing the whole method's local state.</summary>
    private void MarkSelectedDiaryRows(string category, HashSet<long> selectedIds,
        List<ReportData.DiaryEntry> lastRows, Action renderDiaryResults)
    {
        if (selectedIds.Count == 0) return;
        try
        {
            using var db = new Database();
            var learned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var affectedDates = new HashSet<DateOnly>();
            // All-or-nothing: this used to write each selected row as its
            // own separate statement with no shared transaction, so a
            // failure partway through a multi-row selection (an ordinary
            // "database briefly busy" moment, since the background
            // tracker writes to the same file) could relabel some rows
            // and silently leave others untouched, with no error shown
            // (2026-07-14 round-6 audit finding #4 — found independently
            // by two review passes).
            db.RunInTransaction(() =>
            {
                foreach (var id in selectedIds)
                {
                    var row = lastRows.FirstOrDefault(e => e.Id == id);
                    if (row is null || row.Id != id) continue;
                    db.UpdateDiaryEntry(id, row.Start, row.End, row.Dur, category, row.Desc);
                    var keyword = AppNames.Sub(row.Window) ?? AppNames.Group(row.Window);
                    if (keyword is { Length: > 0 } && keyword != "—") learned.Add(keyword);
                    affectedDates.Add(row.Date);
                }
            });
            foreach (var keyword in learned)
                ConfigService.LearnActivityRule(keyword, category);
            if (learned.Count > 0)
                (App.MainWindow as MainWindow)?.RestartTracker();
            // A bulk re-category can span several different days — recompute each
            // affected day's score so none of them keep showing a stale figure
            // (2026-07-17 request). Best-effort: doesn't turn an otherwise-successful
            // re-category into a reported failure.
            ScoreService.TryRecalculateDayScores(db, affectedDates, "ReportsPage.MarkSelected.RecalculateScore");
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.MarkSelected", ex);
            SaveErrorBar.IsOpen = true;
        }
        selectedIds.Clear();
        renderDiaryResults();
    }

    // ── diary ────────────────────────────────────────────────────────────

    private static string FormatDuration(int totalMin) =>
        totalMin >= 60 ? $"{totalMin / 60}h {totalMin % 60}m" : $"{totalMin}m";

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
        // Date nav is meaningless once the list spans more than the one day it controls —
        // either from a free-text search (always wide) or the "All time" filter toggle.
        var wideMode = searching || _diaryAllTime;
        var caption = searching
            ? $"TIME DIARY · SEARCH (LAST {ConfigService.DiaryRetentionDays()} DAYS)"
            : _diaryAllTime
                ? $"TIME DIARY · ALL TIME (LAST {ConfigService.DiaryRetentionDays()} DAYS)"
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
            AutomationProperties.SetName(btn, name);
            btn.Click += (_, _) => onClick();
            return btn;
        }

        // Today is now the jump-shortcut on the left, next to the caption —
        // it used to sit between the prev/next arrows, which read as a state
        // indicator rather than the button it actually is. The current date
        // now lives on the right, between the arrows that move it.
        var todayBtn = NavBtn("Today", "Jump to today",
            () => { _diaryDate = today; Render(); }, enabled: !wideMode && _diaryDate != today);
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
            () => { _diaryDate = _diaryDate.AddDays(-1); Render(); }, enabled: !wideMode);
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
            IsEnabled = !wideMode,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0,
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4),
        };
        AutomationProperties.SetName(datePicker, "Jump to date");
        datePicker.DateChanged += (_, e) =>
        {
            if (e.NewDate is { } picked && DateOnly.FromDateTime(picked.DateTime) != _diaryDate)
            {
                _diaryDate = DateOnly.FromDateTime(picked.DateTime);
                Render();
            }
        };

        var next = NavBtn(nextGlyph, "Next day",
            () => { _diaryDate = _diaryDate.AddDays(1); Render(); }, enabled: !wideMode && _diaryDate < today);

        Grid.SetColumn(prev, 1); Grid.SetColumn(datePicker, 2); Grid.SetColumn(next, 3);
        grid.Children.Add(prev);
        grid.Children.Add(datePicker);
        grid.Children.Add(next);
        return grid;
    }

    // Rows shown before "Show more" is needed — a single busy day can hold
    // close to 300 entries (all built as plain Grids in a non-virtualizing
    // StackPanel), so rendering every one of them unconditionally on every
    // Render() was a real, measured contributor to a 2026-07-21 "Reports
    // takes too long to load" report. Selection state lives in selectedIds/
    // lastRows independent of what's actually been built, so rows built
    // later by "Show more" still pick up the right checked state.
    private const int DefaultDiaryRowsShown = 40;

    private StackPanel DiaryList(
        List<ReportData.DiaryEntry> diary,
        HashSet<long> selectedIds, Action onSelectionChanged, bool showDate = false)
    {
        var list = new StackPanel { Spacing = 4 };

        Grid BuildRow(ReportData.DiaryEntry entry)
        {
            var (id, date, start, end, dur, cat, window, desc) = entry;
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showDate ? 150 : 110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            // Fixed pixel widths, not Auto/Star — the page body is a
            // MaxWidth+Center StackPanel (ReportsPage.xaml), which sizes
            // itself to its widest child's natural content width rather
            // than a fixed width. An Auto or capped-MaxWidth column still
            // measures narrower for a short entry than a long one, so the
            // whole centered page visibly grew/shrank per day. A fixed
            // width makes every row occupy the exact same space regardless
            // of that day's content; overflow past it ellipsis-trims with
            // a tooltip for the full text. App/Page were one combined
            // "App - Page" column until 2026-07-23, split into their own
            // columns (and their own filters — see BuildDiarySection) since
            // they're independently meaningful (e.g. "Chrome"/"GitHub" vs.
            // "Telegram"/"Liza Ponomarenko").
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var select = new CheckBox
            {
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = selectedIds.Contains(id),
            };
            AutomationProperties.SetName(select,
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
                Foreground = (Brush)Application.Current.Resources[CategoryBrushKey(cat)],
                VerticalAlignment = VerticalAlignment.Center,
            };
            // Split app/page, same grouping AppNames already does for the combined label
            // (kept below for accessible names/tooltips) and for the app/page filters.
            var windowLabel = AppNames.Label(window);
            var appGroup = AppNames.Group(window);
            var pageSub = AppNames.Sub(window) ?? "—";
            var appText = new TextBlock
            {
                Text = appGroup,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(appText, appGroup);
            var pageText = new TextBlock
            {
                Text = pageSub,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(pageText, pageSub);
            var detailsText = desc is { Length: > 0 } ? $"“{desc}” ({dur}m)" : $"({dur}m)";
            var details = new TextBlock
            {
                Text = detailsText,
                FontStyle = desc is { Length: > 0 }
                    ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            ToolTipService.SetToolTip(details, detailsText);
            var edit = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(6),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(edit, $"Edit entry: {windowLabel}, {start}–{end}");
            edit.Click += async (_, _) =>
            {
                var ok = await Dialogs.EditDiaryEntryDialog.ShowAsync(XamlRoot, id, date, start, end, dur, cat, desc);
                if (ok == true) Render();
                else if (ok == false) { Render(); SaveErrorBar.IsOpen = true; }
            };
            var split = new Button
            {
                Content = "Split",
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(split, $"Split into several activities: {windowLabel}, {start}–{end}");
            split.Click += async (_, _) =>
            {
                var ok = await Dialogs.SplitDiaryEntryDialog.ShowAsync(XamlRoot, id, date, start, end, dur, cat, window, desc);
                if (ok == true) Render();
                else if (ok == false) { Render(); SaveErrorBar.IsOpen = true; }
            };
            Grid.SetColumn(time, 1);
            Grid.SetColumn(catText, 2);
            Grid.SetColumn(appText, 3);
            Grid.SetColumn(pageText, 4);
            Grid.SetColumn(details, 5);
            Grid.SetColumn(edit, 6);
            Grid.SetColumn(split, 7);
            row.Children.Add(time);
            row.Children.Add(catText);
            row.Children.Add(appText);
            row.Children.Add(pageText);
            row.Children.Add(details);
            row.Children.Add(edit);
            row.Children.Add(split);
            return row;
        }

        var shown = Math.Min(DefaultDiaryRowsShown, diary.Count);
        for (var i = 0; i < shown; i++)
            list.Children.Add(BuildRow(diary[i]));

        if (diary.Count > shown)
        {
            var hidden = diary.Count - shown;
            var moreBtn = new HyperlinkButton
            {
                Content = $"Show {hidden} more",
                Margin = new Thickness(0, 6, 0, 0),
            };
            moreBtn.Click += (_, _) =>
            {
                list.Children.Remove(moreBtn);
                for (var i = shown; i < diary.Count; i++)
                    list.Children.Add(BuildRow(diary[i]));
            };
            list.Children.Add(moreBtn);
        }
        return list;
    }
}
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

    // Whether _diaryDate means "today" rather than one specific date the user went looking
    // for. Without this, _diaryDate is a static seeded once at class load: leave the app open
    // on Reports across midnight and the diary stays pinned to yesterday even though every
    // other section of the page has moved on, and even though MainWindow's day-change watcher
    // does call Render() (2026-07-24 fix, 2026-08-04 re-report — the watcher was only ever
    // half the fix, since re-rendering can't help a date that was never recomputed).
    // Deliberately not "always snap to today on Render": navigating to a past day and coming
    // back from another page must still land where you left it, which is the whole point of
    // _diaryDate being static in the first place.
    private static bool _diaryFollowsToday = true;

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
    // The DiaryTag axis (2026-08-06) — same null-means-no-filter, survives-navigation
    // treatment as the three filters above.
    private static string? _diaryTagFilter;

    // 2026-07-23 request: filters/search normally scope to whichever single day the date
    // nav/picker is showing; this widens that scope to the full retention window (same range
    // free-text search already uses) without requiring a search term. Same survives-navigation
    // treatment as the other diary state.
    private static bool _diaryAllTime;

    // How many rows the "Show more" batching (DiaryList) currently has revealed, and which
    // exact scope it was revealed for (see RenderDiaryResults) — added 2026-07-28 same-day
    // follow-up to a same-day bug: while viewing "today" or "All time" with no search text,
    // _diaryLiveRefresh calls RenderDiaryResults() every 30 seconds, which used to rebuild
    // DiaryList() from scratch every time, silently resetting anyone's "Show more" progress
    // back to the default 40 with no warning. Persisting the count here (like _diaryDate/
    // _diarySearch already survive their own kind of rebuild) lets a same-scope refresh
    // rebuild the list at the size the user left it; only an actual scope change (a
    // different day/search/filter/all-time state) resets it back to the default.
    private static int _diaryRowsShown = DefaultDiaryRowsShown;
    private static string _diaryRowsShownScopeKey = "";

    // The three filter ComboBoxes' "no filter" placeholder items — shared between
    // BuildDiaryFilterRow (which seeds them) and RenderDiaryResults (which rebuilds the
    // app/page lists on every call) so the two can't drift apart from each other.
    private const string AllCategories = "All categories";
    private const string AllApps = "All apps";
    private const string AllPages = "All pages";
    private const string AllTags = "All tags";

    // The debounce timer itself is created once and reused across renders
    // (NavigationCacheMode="Enabled" reuses this page instance, and
    // BuildDiarySection runs on every Render — page nav, period switch, diary
    // day nav). Only _diarySearchDebounceAction is reassigned per render, so
    // a timer armed just before a re-render fires into the CURRENT render's
    // RenderDiaryResults closure instead of a stale one from a torn-down
    // diary panel (round-4 audit finding).
    //
    // System.Threading.Timer (not DispatcherQueueTimer) — same swap MainWindow.Startup.cs's
    // watchers already made, for the same reason: DispatcherQueueTimer.Tick was confirmed to
    // silently stop firing there with no error and IsRunning still reading true, and this
    // page's two timers used the exact same WinRT API the fix moved away from everywhere
    // else (2026-07-24 audit finding #5). These only run while the page is on screen, unlike
    // the hidden background watchers, but that's a lower-risk profile, not proof this API is
    // safe here.
    private System.Threading.Timer? _diarySearchDebounce;
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
    private System.Threading.Timer? _diaryLiveRefresh;
    private Action? _diaryLiveRefreshAction;
    private static readonly TimeSpan DiaryLiveRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DiarySearchDebounceInterval = TimeSpan.FromMilliseconds(250);

    // selectedIds (below, local to BuildDiarySection) is rebuilt from scratch on every full
    // Render() — mirroring its count here at the one place it already changes
    // (UpdateMarkToolbar) lets MainWindow's day-change watcher check "is there a bulk
    // selection in progress" without restructuring selectedIds itself. Same shape as
    // TaskNoteView.AnyEditInProgress: a forced background refresh can rebuild this page's
    // whole tree with no user action behind it, which would otherwise silently drop a
    // selection the same way an unsaved note almost was (2026-07-24 audit finding #3).
    private int _diarySelectedCount;
    internal bool HasActiveDiarySelection => _diarySelectedCount > 0;

    /// <summary>
    /// Diary search/list section — one day by default; searching widens to
    /// everything still retained (ConfigService.DiaryRetentionDays(),
    /// user-configurable — see Settings) instead of just the day on screen.
    /// Each UI sub-area (search box, filter row, mark-selected toolbar, the
    /// scrollable list shell) is built by its own method below — those pieces
    /// share no mutable state with each other, so pulling them out was a safe,
    /// mechanical split (2026-07-28 code-quality audit finding — this method
    /// was 389 lines). RenderDiaryResults/UpdateMarkToolbar and the rest of
    /// the event wiring stay here: they share selectedIds/lastRows/
    /// syncingFilters/syncingSelectAll with each other in ways that don't
    /// split apart as cleanly, and moving them would risk exactly the kind of
    /// closure-capture bug this project has been bitten by before.
    /// </summary>
    private void BuildDiarySection(DateOnly today, ScoreService score)
    {
        // Ahead of everything below that reads _diaryDate — this is the one place the
        // calendar date rolling over gets applied to the diary (see _diaryFollowsToday).
        if (_diaryFollowsToday) _diaryDate = today;

        Body.Children.Add(DiaryHeader());

        // Reflections (the evening review's one-line answers) were
        // previously write-only — saved but never shown back anywhere in
        // the app (2026-07-09 audit finding #12). Shown for whichever day
        // the diary is currently viewing, same as everything else on this
        // section.
        if (_diarySearch.Trim().Length == 0 && score.LoadReflection(_diaryDate) is { Length: > 0 } reflection)
            Body.Children.Add(ReflectionCallout(reflection));

        var searchBox = BuildDiarySearchBox();
        var (categoryBox, appBox, pageBox, tagBox, allTimeBox, clearFiltersBtn) = BuildDiaryFilterRow();

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
        var (selectAllBox, selectedLabel, markOnBtn, markOffBtn, markNeutralBtn) = BuildDiaryMarkToolbar();

        var diaryResults = BuildDiaryResultsArea();

        const int maxSearchResults = 300;

        // Guards SelectionChanged from firing when RenderDiaryResults below re-syncs these boxes'
        // SelectedItem to the persisted filter state (e.g. after the app/page lists are
        // rebuilt), same pattern as syncingSelectAll further down for the same reason.
        var syncingFilters = false;

        // Guards Checked/Unchecked below from firing when UpdateMarkToolbar
        // sets IsChecked itself to reflect the current selection — without
        // it, syncing the box would immediately re-trigger select-all/none.
        var syncingSelectAll = false;

        void UpdateMarkToolbar()
        {
            var n = selectedIds.Count;
            _diarySelectedCount = n;
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
            //
            // The four filters (category/app/page/search) are combined with AND on the actual
            // results below, but the App/Page dropdowns' own OPTION LISTS used to be built from
            // every row in view regardless of any filter already active — so picking Category =
            // Off-plan still offered every app that had ANY entry that day, including ones whose
            // entries were entirely on-plan, and picking App also didn't narrow Page's options
            // (2026-07-29 user report: filters "should be interconnected"). Each dropdown's
            // options are now built from rows matching every OTHER active filter — the standard
            // faceted-search shape — so a filter only ever offers choices that can actually
            // produce a result under the filters already applied.
            bool MatchesCategory(ReportData.DiaryEntry e) =>
                _diaryCategoryFilter is not { } cat || e.Cat == cat;
            bool MatchesApp(ReportData.DiaryEntry e) =>
                _diaryAppFilter is not { } app ||
                string.Equals(AppNames.Group(e.Window), app, StringComparison.OrdinalIgnoreCase);
            bool MatchesPage(ReportData.DiaryEntry e) =>
                _diaryPageFilter is not { } page ||
                string.Equals(AppNames.Sub(e.Window) ?? "", page, StringComparison.OrdinalIgnoreCase);
            bool MatchesTag(ReportData.DiaryEntry e) =>
                _diaryTagFilter is not { } tag || e.Tag == tag;
            bool MatchesSearch(ReportData.DiaryEntry e) =>
                !searching ||
                AppNames.Label(e.Window).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (e.Desc is { Length: > 0 } d && d.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                e.Cat.Replace('_', ' ').Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (DiaryTag.LabelOf(e.Tag) is { } tagLabel && tagLabel.Contains(q, StringComparison.OrdinalIgnoreCase));

            syncingFilters = true;
            var appsInView = rows.Where(e => MatchesCategory(e) && MatchesPage(e) && MatchesTag(e) && MatchesSearch(e))
                .Select(e => AppNames.Group(e.Window))
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

            var pagesInView = rows.Where(e => MatchesCategory(e) && MatchesApp(e) && MatchesTag(e) && MatchesSearch(e))
                .Select(e => AppNames.Sub(e.Window))
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
            // Fixed option list, same as categoryBox — never rebuilt from what's in view.
            tagBox.SelectedItem = _diaryTagFilter is { } wantTag
                ? DiaryTag.LabelOf(wantTag) ?? AllTags
                : AllTags;
            allTimeBox.IsChecked = _diaryAllTime;
            syncingFilters = false;

            // Same five predicates decide what actually shows in the results list — the
            // dropdown-option narrowing above and the results below can never disagree with
            // each other, since both read from the same single definition of each filter.
            var filteredList = rows.Where(e =>
                MatchesCategory(e) && MatchesApp(e) && MatchesPage(e) && MatchesTag(e) && MatchesSearch(e)).ToList();
            var filtersActive = _diaryCategoryFilter != null || _diaryAppFilter != null ||
                _diaryPageFilter != null || _diaryTagFilter != null;

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
                            // The window is configurable (Settings ▸ Hours), so this sentence
                            // has to read it rather than restate the old hardcoded 06:00–20:00
                            // constants it used to quote (2026-08-04).
                            ? "No diary entries yet today. Tracking runs " +
                              $"{ConfigService.DiaryStartTime().ToIsoTimeOfDay()}–" +
                              $"{ConfigService.DiaryEndTime().ToIsoTimeOfDay()}."
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
            // A genuine scope change (different day/search/filters/all-time) starts the reveal
            // count fresh; the periodic live-refresh timer re-running this same method with an
            // UNCHANGED scope must not — see _diaryRowsShown's own comment for why.
            var scopeKey = string.Join('|', _diaryDate, q, _diaryCategoryFilter, _diaryAppFilter,
                _diaryPageFilter, _diaryTagFilter, _diaryAllTime);
            if (scopeKey != _diaryRowsShownScopeKey)
            {
                _diaryRowsShown = DefaultDiaryRowsShown;
                _diaryRowsShownScopeKey = scopeKey;
            }
            // Card()'s Border has a non-zero CornerRadius (ReportsPage.Styling.cs), which makes
            // WinUI corner-clip its content to the Border's OWN arranged bounds — MinWidth on
            // diaryResults/the DiaryList StackPanel further down (see their own comments) only
            // ever affected LAYOUT sizing, but couldn't stop this Border, defaulting to
            // Stretch, from being arranged at whatever narrower width diaryResults handed it
            // and then clipping away everything past that (2026-07-28: this is what was
            // actually eating Edit/Split, invisibly — not a scroll/alignment issue at all).
            // MinWidth here has to cover the diary list's own MinWidth (DiaryListWidth) *plus*
            // this Border's horizontal Padding (18+18), or the clip region is still too narrow
            // for what's arranged inside it — that sum is DiaryCardWidth (ReportsPage.xaml.cs).
            var diaryCard = Card(DiaryList(filteredList, selectedIds, UpdateMarkToolbar, showDate: wideRange));
            diaryCard.MinWidth = DiaryCardWidth;
            diaryResults.Children.Add(diaryCard);
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
        tagBox.SelectionChanged += (_, _) =>
        {
            if (syncingFilters) return;
            var chosen = tagBox.SelectedItem as string;
            _diaryTagFilter = chosen is null or AllTags
                ? null
                : DiaryTag.Options.FirstOrDefault(o => o.Label == chosen).Value;
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
            _diaryTagFilter = null;
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
                _diaryLiveRefresh?.Change(DiaryLiveRefreshInterval, DiaryLiveRefreshInterval);
            else
                _diaryLiveRefresh?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
        }
        searchBox.TextChanged += (_, _) =>
        {
            _diarySearch = searchBox.Text;
            searchDebounce.Change(DiarySearchDebounceInterval, System.Threading.Timeout.InfiniteTimeSpan);
            SyncLiveRefresh();
        };

        // Only re-poll while today's rows are actually in scope with no search active — a
        // past-only day view is finished history (nothing new will ever appear), and a search
        // already re-renders itself on its own typing debounce above.
        _diaryLiveRefreshAction = RenderDiaryResults;
        EnsureDiaryLiveRefreshTimer();
        SyncLiveRefresh();
    }

    /// <summary>The diary's free-text search box — split out of BuildDiarySection
    /// (2026-07-28 code-quality audit finding: that method was 389 lines) since this
    /// piece is pure "build one control, add it to Body," with no shared mutable state.</summary>
    private TextBox BuildDiarySearchBox()
    {
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
        return searchBox;
    }

    /// <summary>The category/app/page/tag filter row plus "All time" checkbox and "Clear
    /// filters" button, in their own horizontally-scrollable row — split out of
    /// BuildDiarySection for the same reason as BuildDiarySearchBox above.
    /// RenderDiaryResults (which stays in BuildDiarySection, since it also touches
    /// diaryResults/subtotalText/lastRows) re-syncs these boxes' items/selection on every
    /// call; this method only constructs them, seeded with whatever filter state already
    /// persisted from before.</summary>
    private (ComboBox CategoryBox, ComboBox AppBox, ComboBox PageBox, ComboBox TagBox,
        CheckBox AllTimeBox, Button ClearFiltersBtn) BuildDiaryFilterRow()
    {
        var categoryBox = new ComboBox { PlaceholderText = "Category", MinWidth = 140 };
        categoryBox.Items.Add(AllCategories);
        foreach (var (label, _) in DiaryCategory.EditableOptions) categoryBox.Items.Add(label);
        AutomationProperties.SetName(categoryBox, "Filter diary by category");

        var appBox = new ComboBox { PlaceholderText = "App", MinWidth = 150 };
        AutomationProperties.SetName(appBox, "Filter diary by app");

        var pageBox = new ComboBox { PlaceholderText = "Page", MinWidth = 180 };
        AutomationProperties.SetName(pageBox, "Filter diary by page");

        // The DiaryTag axis (2026-08-06) — fixed option list like Category (not rebuilt from
        // what's in view like App/Page, which are open-ended value sets).
        var tagBox = new ComboBox { PlaceholderText = "Tag", MinWidth = 140 };
        tagBox.Items.Add(AllTags);
        foreach (var (label, _) in DiaryTag.Options) tagBox.Items.Add(label);
        AutomationProperties.SetName(tagBox, "Filter diary by tag");

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
        filterRow.Children.Add(tagBox);
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

        return (categoryBox, appBox, pageBox, tagBox, allTimeBox, clearFiltersBtn);
    }

    /// <summary>The "select all / mark on-plan / off-plan / neutral" bulk-action toolbar —
    /// split out of BuildDiarySection for the same reason as the two methods above.
    /// UpdateMarkToolbar (which stays in BuildDiarySection, since it also touches
    /// selectedIds/lastRows) flips these controls' enabled/label state as the selection
    /// changes; this method only constructs them, starting disabled/empty.</summary>
    private (CheckBox SelectAllBox, TextBlock SelectedLabel, Button MarkOnBtn, Button MarkOffBtn, Button MarkNeutralBtn)
        BuildDiaryMarkToolbar()
    {
        var markToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8),
        };
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

        return (selectAllBox, selectedLabel, markOnBtn, markOffBtn, markNeutralBtn);
    }

    /// <summary>The diary list's own scrollable area — split out of BuildDiarySection for
    /// the same reason as the methods above. RenderDiaryResults (which stays in
    /// BuildDiarySection) fills the returned StackPanel's Children on every call; this
    /// method only builds the empty scroll shell around it.</summary>
    private StackPanel BuildDiaryResultsArea()
    {
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
        var diaryResults = new StackPanel { Spacing = 0, MinWidth = DiaryListWidth };
        var diaryScroller = new ScrollViewer
        {
            MaxHeight = 520,
            // Visible, not Auto (2026-07-28 user report: "can't split the unaccounted
            // time") — Auto's overlay-style indicator only appears on hover and is easy to
            // never notice at all, so scrollable content used to silently look complete
            // without it. The page's own content column (ReportsPage.xaml.cs'
            // maxContentWidth) is now widened to fit a diary row's own MinWidth
            // (DiaryCardWidth) on a wide-enough window, so this scroller/scrollbar mostly
            // matters on a narrower one now — kept regardless as a belt-and-suspenders
            // layer, since the row's fixed pixel columns can still exceed whatever width
            // the window actually has to give.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = diaryResults,
        };
        Body.Children.Add(diaryScroller);
        return diaryResults;
    }

    /// <summary>Lazily creates the search-input debounce timer once, reused across renders
    /// (see the field's own doc comment) — pulled out of BuildDiarySection so that ~400-line
    /// method isn't also responsible for timer lifecycle bookkeeping (code-quality audit
    /// finding #7).</summary>
    private System.Threading.Timer EnsureDiarySearchDebounceTimer() =>
        _diarySearchDebounce ??= new System.Threading.Timer(
            _ => DispatcherQueue.TryEnqueue(() => _diarySearchDebounceAction?.Invoke()),
            null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);

    /// <summary>Lazily creates the "today's rows might still be growing" live-refresh
    /// timer once, reused across renders — see EnsureDiarySearchDebounceTimer's doc comment.
    /// Starts idle (infinite due-time); SyncLiveRefresh arms/disarms the actual period.</summary>
    private System.Threading.Timer EnsureDiaryLiveRefreshTimer() =>
        _diaryLiveRefresh ??= new System.Threading.Timer(
            _ => DispatcherQueue.TryEnqueue(() => _diaryLiveRefreshAction?.Invoke()),
            null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);

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
                    // row.Tag passed through unchanged — this bulk action only ever
                    // re-categorizes (on/off-plan/neutral), it was never meant to touch tags.
                    db.UpdateDiaryEntry(id, row.Start, row.End, row.Dur, category, row.Desc, row.Tag);
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

        // Every date move goes through here so _diaryFollowsToday can't be updated at three
        // of the four navigation sites and missed at the fourth — landing back on today by
        // any route (the button, or stepping forward with the arrow) re-arms the follow.
        void GoTo(DateOnly target)
        {
            _diaryDate = target;
            _diaryFollowsToday = target == today;
            Render();
        }

        // Today is now the jump-shortcut on the left, next to the caption —
        // it used to sit between the prev/next arrows, which read as a state
        // indicator rather than the button it actually is. The current date
        // now lives on the right, between the arrows that move it.
        var todayBtn = NavBtn("Today", "Jump to today",
            () => GoTo(today), enabled: !wideMode && _diaryDate != today);
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
            () => GoTo(_diaryDate.AddDays(-1)), enabled: !wideMode);
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
                GoTo(DateOnly.FromDateTime(picked.DateTime));
        };

        var next = NavBtn(nextGlyph, "Next day",
            () => GoTo(_diaryDate.AddDays(1)), enabled: !wideMode && _diaryDate < today);

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
        // MinWidth must be repeated here, not just on diaryResults two levels up (2026-07-28
        // user report: "can't split the unaccounted time" — Edit/Split were being silently
        // clipped, no scrollbar, no overhang). diaryResults' own MinWidth=DiaryListWidth only
        // forces *that* StackPanel to claim that much width from the ScrollViewer; this
        // StackPanel and the Card() Border between them both default to
        // HorizontalAlignment.Stretch, which means each one gets arranged at whatever width its
        // own parent hands it — and nothing upstream of diaryResults was actually offering that
        // width back down through that chain, so this level (and Card's Border) were both
        // getting stretch-clipped to the page's content column width instead, quietly cutting
        // off Edit/Split with no visual sign anything was missing. Confirmed via a live
        // PrintWindow capture: no scrollbar, no overhang, the row just ends flush with the card
        // edge.
        var list = new StackPanel { Spacing = 4, MinWidth = DiaryListWidth };

        Grid BuildRow(ReportData.DiaryEntry entry)
        {
            var (id, date, start, end, dur, cat, window, desc, tag) = entry;
            // Left, not the FrameworkElement default Stretch (2026-07-28 — the MinWidth
            // changes above turned out not to be enough on their own): a Stretch-aligned
            // Grid gets ARRANGED at whatever final size its ancestor chain hands it, and
            // with no Star column to absorb a shortfall, WinUI was silently zeroing out the
            // Auto-width Edit/Split columns entirely (BoundingRectangle: Empty, not just
            // scrolled off) rather than preserving their measured size and letting the
            // ScrollViewer's own horizontal scroll handle the overflow. Left tells the row
            // to size itself to its true natural (Measure-time) width and never be
            // compressed by Arrange, which is what actually lets the ScrollViewer scroll to
            // it instead of clipping it out of existence.
            var row = new Grid { ColumnSpacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
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

            var time = Dim(showDate ? $"{date.ToDisplayDateShort()} · {start} → {end}" : $"{start} → {end}");
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
            var rawPageSub = AppNames.Sub(window);
            // idle/dismissed entries (and anything else with no app-detected sub-item) have
            // nothing else to show in the Page column — but once the user's answered "what
            // were you doing" for one, that free-text description IS the activity, so show it
            // there instead of a bare "—" (2026-07-29 report: it was landing only in the
            // details column, buried next to the duration, while Page kept showing "—" as if
            // nothing had been recorded). Left untouched whenever there's a real detected page
            // (e.g. Chrome's actual site) — a manually-added note on top of a real page is a
            // supplementary detail, not a replacement for it, so it stays in the details column.
            var descInPage = rawPageSub is null && desc is { Length: > 0 };
            var pageSub = descInPage ? desc! : rawPageSub ?? "—";
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
                FontStyle = descInPage ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(pageText, pageSub);
            // No parens around the duration (2026-07-29 report) — just "12m", or
            // "“desc” 12m" when there's a description that didn't already move to the Page
            // column above. The tag (2026-08-06), when set, appends as its own short suffix —
            // this column already handles overflow via TextTrimming+tooltip below, so it's the
            // lowest-risk place to surface a second, optional field without touching any of
            // the row's fixed pixel column widths (see this row's own width comments above).
            var detailsText = (!descInPage && desc is { Length: > 0 } ? $"“{desc}” {dur}m" : $"{dur}m") +
                (DiaryTag.LabelOf(tag) is { } tagLabel ? $" · #{tagLabel}" : "");
            var details = new TextBlock
            {
                Text = detailsText,
                FontStyle = !descInPage && desc is { Length: > 0 }
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
                var ok = await Dialogs.EditDiaryEntryDialog.ShowAsync(XamlRoot, id, date, start, end, dur, cat, desc, tag);
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
                var ok = await Dialogs.SplitDiaryEntryDialog.ShowAsync(XamlRoot, id, date, start, end, dur, cat, window, desc, tag);
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

        // Starts from _diaryRowsShown, not always the default — so a same-scope rebuild
        // (the periodic live-refresh timer while viewing "today"/"All time") redraws the
        // list at whatever size the user had already revealed via "Show more," instead of
        // silently collapsing back to the default every time it fires (see that field's own
        // comment). RenderDiaryResults already resets it to the default on an actual scope
        // change before this method is ever called.
        var shown = Math.Min(_diaryRowsShown, diary.Count);
        for (var i = 0; i < shown; i++)
            list.Children.Add(BuildRow(diary[i]));

        // One batch at a time, not the whole rest in one go (2026-07-28 request) — a busy
        // search/date-range match can leave hundreds of rows hidden behind this button, and
        // building them all at once defeats the whole point of DefaultDiaryRowsShown above
        // (the same non-virtualizing-StackPanel cost that capped the initial render). Re-adds
        // itself after each batch if there's still more left, so repeated clicks keep working.
        const int showMoreIncrement = 50;
        void AddShowMoreIfNeeded()
        {
            if (shown >= diary.Count) return;
            var hidden = diary.Count - shown;
            HyperlinkButton moreBtn = null!;
            moreBtn = new HyperlinkButton
            {
                Content = $"Show {Math.Min(showMoreIncrement, hidden)} more",
                Margin = new Thickness(0, 6, 0, 0),
            };
            moreBtn.Click += (_, _) =>
            {
                list.Children.Remove(moreBtn);
                var next = Math.Min(shown + showMoreIncrement, diary.Count);
                for (var i = shown; i < next; i++)
                    list.Children.Add(BuildRow(diary[i]));
                shown = next;
                _diaryRowsShown = next;
                AddShowMoreIfNeeded();
            };
            list.Children.Add(moreBtn);
        }
        AddShowMoreIfNeeded();
        return list;
    }
}
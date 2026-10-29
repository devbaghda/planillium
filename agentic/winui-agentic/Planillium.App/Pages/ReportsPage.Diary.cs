using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Dialogs;
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

    // Which field the search looks in (G2, 2026-10-27) — survives navigation like _diarySearch.
    private static DiaryFilter.SearchField _diarySearchField = DiaryFilter.SearchField.All;

    // "Show all" was chosen in the diary list (G2): a same-scope rebuild (live refresh adds a
    // row on top) then keeps showing every row. Reset wherever _diaryRowsShown resets.
    private static bool _diaryShowAll;

    // Column filters (2026-07-22 request, app/page split 2026-07-23; multi-select 2026-10-01) —
    // independent of the free-text search above and combinable with it: empty set means "no filter."
    // App/page match AppNames.Group/Sub respectively (the same values the row's own App/Page columns show —
    // e.g. app="Chrome", page="GitHub", or app="Telegram", page="Liza Ponomarenko"). Same
    // survives-navigation treatment as _diarySearch/_diaryDate.
    // Category/tag use Ordinal; app/page use OrdinalIgnoreCase for case-insensitive matching.
    private static HashSet<string> _diaryCategoryFilter;
    private static HashSet<string> _diaryAppFilter;
    private static HashSet<string> _diaryPageFilter;
    // The DiaryTag axis (2026-08-06) — same empty-set-means-no-filter, survives-navigation
    // treatment as the three filters above.
    private static HashSet<string> _diaryTagFilter;

    static ReportsPage()
    {
        _diaryCategoryFilter = new(StringComparer.Ordinal);
        _diaryAppFilter = new(StringComparer.OrdinalIgnoreCase);
        _diaryPageFilter = new(StringComparer.OrdinalIgnoreCase);
        _diaryTagFilter = new(StringComparer.Ordinal);
    }

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

    // G3 (2026-10-29): the diary's ticks, a page field so they survive Render(), Mark and Undo
    // (the page instance is cached). Before this they were a local of BuildDiarySection, rebuilt
    // empty on every full Render, and Mark cleared them on purpose. They are pruned on every
    // results render against the UNCAPPED filtered list, so a tick drops only when its row no
    // longer exists or no longer matches the filters.
    private readonly HashSet<long> _diaryTicks = new();

    // G3: a preview, write or undo through the safe door is running — the Mark/tag buttons and
    // the bar's Undo stay disabled until it finishes (survives a Render; the closures below are
    // re-pointed at the current build of the section on every BuildDiarySection).
    private bool _diaryDoorBusy;
    private Action? _diaryRenderResultsAction;
    private Action? _diaryUpdateToolbarAction;

    // G3: the result bar's content, page-level so a re-render shows the same message until the
    // user closes it, a new Mark/Undo replaces it, or the Reports page is left.
    private sealed record DiaryResultState(InfoBarSeverity Severity, string Title, string Message,
        bool Closable, long? UndoChangeId = null, bool Undoing = false);
    private DiaryResultState? _diaryResult;
    private InfoBar? _diaryResultBar;

    private static string N0(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

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

        DiaryBody.Children.Add(DiaryHeader());

        // Reflections (the evening review's one-line answers) were
        // previously write-only — saved but never shown back anywhere in
        // the app (2026-07-09 audit finding #12). Shown for whichever day
        // the diary is currently viewing, same as everything else on this
        // section.
        if (_diarySearch.Trim().Length == 0 && score.LoadReflection(_diaryDate) is { Length: > 0 } reflection)
            DiaryBody.Children.Add(ReflectionCallout(reflection));

        var (searchBox, searchFieldBox) = BuildDiarySearchBox();
        var (categoryBtn, appBtn, pageBtn, tagBtn, allTimeBox, clearFiltersBtn, categoryFlyoutContent, tagFlyoutContent, appFlyoutContent, pageFlyoutContent) = BuildDiaryFilterRow();

        // Subtotal of whatever's currently filtered/shown — updated in RenderDiaryResults
        // below, right along with the list itself.
        var subtotalText = Dim("");
        subtotalText.Margin = new Thickness(0, 0, 0, 6);
        DiaryBody.Children.Add(subtotalText);

        // Mark-selected toolbar — built once (not on every keystroke) so
        // its buttons don't get re-wired constantly; UpdateMarkToolbar()
        // just flips enabled/label state as the selection changes.
        // G3: the ticks are the page field (survive Render/Mark/Undo). lastRows = the rows
        // listed (capped at 300 in a wide search); allFiltered = every match (M), uncapped —
        // what Mark without ticks acts on and what the ticks are pruned against.
        var selectedIds = _diaryTicks;
        var lastRows = new List<ReportData.DiaryEntry>();
        var allFiltered = new List<ReportData.DiaryEntry>();
        var (selectAllBox, selectedLabel, markOnBtn, markOffBtn, markNeutralBtn, tagMarkButtons) = BuildDiaryMarkToolbar();
        var categoryMarkButtons = new (Button Btn, string Word)[]
        {
            (markOnBtn, "on-plan"), (markOffBtn, "off-plan"), (markNeutralBtn, "neutral"),
        };

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

        // The "-" entry ("this row has no value in this field", G2): first checkbox after "Clear"
        // in every list. The set is fetched through a getter because the app/page sets are
        // replaced (PruneToAvailable) on every RenderDiaryResults.
        CheckBox NewNoValueCheckbox(string automationName, Func<HashSet<string>> set)
        {
            var chk = new CheckBox
            {
                Content = DiaryFilter.NoValueLabel,
                IsChecked = set().Contains(DiaryFilter.NoValue),
                Tag = DiaryFilter.NoValue,
            };
            AutomationProperties.SetName(chk, automationName);
            chk.Checked += (_, _) => { if (!syncingFilters) { set().Add(DiaryFilter.NoValue); RenderDiaryResults(); } };
            chk.Unchecked += (_, _) => { if (!syncingFilters) { set().Remove(DiaryFilter.NoValue); RenderDiaryResults(); } };
            return chk;
        }

        void BuildCategoryCheckboxList(StackPanel stack)
        {
            stack.Children.Clear();
            var clearBtn = new HyperlinkButton { Content = "Clear", Margin = new Thickness(0, 0, 0, 4) };
            clearBtn.Click += (_, _) => { _diaryCategoryFilter.Clear(); RenderDiaryResults(); };
            stack.Children.Add(clearBtn);
            stack.Children.Add(NewNoValueCheckbox("- (no category)", () => _diaryCategoryFilter));

            foreach (var (label, value) in DiaryCategory.EditableOptions)
            {
                var chk = new CheckBox
                {
                    Content = label,
                    IsChecked = _diaryCategoryFilter.Contains(value),
                    Tag = value,
                };
                AutomationProperties.SetName(chk, label);
                chk.Checked += (_, _) => { if (!syncingFilters) { _diaryCategoryFilter.Add(value); RenderDiaryResults(); } };
                chk.Unchecked += (_, _) => { if (!syncingFilters) { _diaryCategoryFilter.Remove(value); RenderDiaryResults(); } };
                stack.Children.Add(chk);
            }
        }

        void BuildTagCheckboxList(StackPanel stack)
        {
            stack.Children.Clear();
            var clearBtn = new HyperlinkButton { Content = "Clear", Margin = new Thickness(0, 0, 0, 4) };
            clearBtn.Click += (_, _) => { _diaryTagFilter.Clear(); RenderDiaryResults(); };
            stack.Children.Add(clearBtn);
            stack.Children.Add(NewNoValueCheckbox("- (no tag)", () => _diaryTagFilter));

            foreach (var (label, value) in DiaryTag.Options)
            {
                var chk = new CheckBox
                {
                    Content = label,
                    IsChecked = value != null && _diaryTagFilter.Contains(value),
                    Tag = value,
                };
                AutomationProperties.SetName(chk, label);
                var capturedValue = value;
                chk.Checked += (_, _) => { if (!syncingFilters && capturedValue != null) { _diaryTagFilter.Add(capturedValue); RenderDiaryResults(); } };
                chk.Unchecked += (_, _) => { if (!syncingFilters && capturedValue != null) { _diaryTagFilter.Remove(capturedValue); RenderDiaryResults(); } };
                stack.Children.Add(chk);
            }
        }

        // G3: T = ticks, M = every match (uncapped). Category buttons act on the ticks when
        // T > 0, otherwise on all M matches, and say which in their text (SPEC Screen design §1).
        void UpdateMarkToolbar()
        {
            var t = selectedIds.Count;
            var m = allFiltered.Count;
            _diarySelectedCount = t;
            selectedLabel.Text = m == 0 ? "" : t == 0 ? "None ticked" : $"{N0(t)} ticked";

            foreach (var (btn, word) in categoryMarkButtons)
            {
                string text, tip;
                if (m == 0) { text = $"Mark {word}"; tip = "No entries to mark."; }
                else if (t > 0) { text = $"Mark {N0(t)} ticked {word}"; tip = $"Marks only the {N0(t)} ticked rows."; }
                else if (m == 1) { text = $"Mark 1 {word}"; tip = "Marks the 1 entry matching the current view."; }
                else
                {
                    text = $"Mark all {N0(m)} {word}";
                    tip = $"Marks all {N0(m)} entries matching the current filters and search, not only the rows shown.";
                }
                btn.Content = text;
                AutomationProperties.SetName(btn, text);
                btn.IsEnabled = m > 0 && !_diaryDoorBusy;
                ToolTipService.SetToolTip(btn, _diaryDoorBusy ? "Working..." : tip);
            }
            foreach (var (_, btn) in tagMarkButtons)
            {
                btn.IsEnabled = t > 0 && !_diaryDoorBusy;
                ToolTipService.SetToolTip(btn, _diaryDoorBusy ? "Working..." : null);
            }

            syncingSelectAll = true;
            var shownTicked = t == 0 ? 0 : lastRows.Count(r => selectedIds.Contains(r.Id));
            selectAllBox.IsEnabled = lastRows.Count > 0;
            selectAllBox.IsChecked = lastRows.Count == 0 || t == 0 ? false
                : shownTicked == lastRows.Count ? true
                : null;
            syncingSelectAll = false;
        }

        void RebuildAppCheckboxList(StackPanel stack, List<string> appsInView)
        {
            stack.Children.Clear();
            var clearBtn = new HyperlinkButton { Content = "Clear", Margin = new Thickness(0, 0, 0, 4) };
            clearBtn.Click += (_, _) => { _diaryAppFilter.Clear(); RenderDiaryResults(); };
            stack.Children.Add(clearBtn);
            stack.Children.Add(NewNoValueCheckbox("- (no app)", () => _diaryAppFilter));

            foreach (var app in appsInView)
            {
                var chk = new CheckBox
                {
                    Content = app,
                    IsChecked = _diaryAppFilter.Contains(app, StringComparer.OrdinalIgnoreCase),
                    Tag = app,
                };
                AutomationProperties.SetName(chk, app);
                var capturedApp = app;
                chk.Checked += (_, _) => { if (!syncingFilters) { _diaryAppFilter.Add(capturedApp); RenderDiaryResults(); } };
                chk.Unchecked += (_, _) => { if (!syncingFilters) { _diaryAppFilter.Remove(capturedApp); RenderDiaryResults(); } };
                stack.Children.Add(chk);
            }
        }

        void RebuildPageCheckboxList(StackPanel stack, List<string> pagesInView)
        {
            stack.Children.Clear();
            var clearBtn = new HyperlinkButton { Content = "Clear", Margin = new Thickness(0, 0, 0, 4) };
            clearBtn.Click += (_, _) => { _diaryPageFilter.Clear(); RenderDiaryResults(); };
            stack.Children.Add(clearBtn);
            stack.Children.Add(NewNoValueCheckbox("- (no page)", () => _diaryPageFilter));

            foreach (var page in pagesInView)
            {
                var chk = new CheckBox
                {
                    Content = page,
                    IsChecked = _diaryPageFilter.Contains(page, StringComparer.OrdinalIgnoreCase),
                    Tag = page,
                };
                AutomationProperties.SetName(chk, page);
                var capturedPage = page;
                chk.Checked += (_, _) => { if (!syncingFilters) { _diaryPageFilter.Add(capturedPage); RenderDiaryResults(); } };
                chk.Unchecked += (_, _) => { if (!syncingFilters) { _diaryPageFilter.Remove(capturedPage); RenderDiaryResults(); } };
                stack.Children.Add(chk);
            }
        }

        void SyncCategoryCheckboxes()
        {
            // Re-sync checked state for category checkboxes (list is fixed, only state changes)
            if (categoryBtn.Flyout is Flyout { Content: ScrollViewer scroller } &&
                scroller.Content is StackPanel stack)
            {
                foreach (var child in stack.Children.OfType<CheckBox>())
                {
                    if (child.Tag is string catValue)
                    {
                        child.IsChecked = _diaryCategoryFilter.Contains(catValue);
                    }
                }
            }
        }

        void SyncTagCheckboxes()
        {
            // Re-sync checked state for tag checkboxes (list is fixed, only state changes)
            if (tagBtn.Flyout is Flyout { Content: ScrollViewer scroller } &&
                scroller.Content is StackPanel stack)
            {
                foreach (var child in stack.Children.OfType<CheckBox>())
                {
                    if (child.Tag is string tagValue)
                    {
                        child.IsChecked = _diaryTagFilter.Contains(tagValue);
                    }
                }
            }
        }

        void SyncAppCheckboxes()
        {
            // Re-sync checked state for app checkboxes (list is fixed, only state changes)
            if (appBtn.Flyout is Flyout { Content: ScrollViewer scroller } &&
                scroller.Content is StackPanel stack)
            {
                foreach (var child in stack.Children.OfType<CheckBox>())
                {
                    if (child.Tag is string appValue)
                    {
                        child.IsChecked = _diaryAppFilter.Contains(appValue, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
        }

        void SyncPageCheckboxes()
        {
            // Re-sync checked state for page checkboxes (list is fixed, only state changes)
            if (pageBtn.Flyout is Flyout { Content: ScrollViewer scroller } &&
                scroller.Content is StackPanel stack)
            {
                foreach (var child in stack.Children.OfType<CheckBox>())
                {
                    if (child.Tag is string pageValue)
                    {
                        child.IsChecked = _diaryPageFilter.Contains(pageValue, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
        }

        // G5: the App/Page filter option lists last computed by RenderDiaryResults, used to fill
        // the flyout when it opens; and what the open panel currently holds (null = closed/empty).
        var appOptions = new List<string>();
        var pageOptions = new List<string>();
        List<string>? appPanelOptions = null;
        List<string>? pagePanelOptions = null;

        // G5: the rows RenderDiaryResults last loaded for its scope, so the live refresh can skip
        // a rebuild when nothing new was logged (see _diaryLiveRefreshAction below).
        var loadedRows = new List<ReportData.DiaryEntry>();

        // "All time" (2026-07-23) reuses exactly the wide range free-text search already
        // used, just without requiring a search term — so a filter combo alone (e.g. "just
        // show me every Telegram/Liza Ponomarenko entry") can scope past the single day the
        // date nav is showing.
        List<ReportData.DiaryEntry> LoadScopeRows()
        {
            var wide = _diarySearch.Trim().Length > 0 || _diaryAllTime;
            // Matches the date-picker's own MinDate and Database.PruneAndRollupDiary's
            // actual cutoff (rows older than today − N are pruned, so today − N is the
            // oldest day still genuinely present) — this used to be one day short,
            // silently excluding the single oldest day still on disk (round-5 audit
            // finding #6).
            return wide
                ? ReportData.DiaryInRange(today.AddDays(-ConfigService.DiaryRetentionDays()), today)
                : ReportData.DiaryInRange(_diaryDate, _diaryDate);
        }

        void RenderDiaryResults() => RenderDiaryResultsFrom(LoadScopeRows());

        void RenderDiaryResultsFrom(List<ReportData.DiaryEntry> rows)
        {
            loadedRows = rows;
            diaryResults.Children.Clear();
            var q = _diarySearch.Trim();
            var searching = q.Length > 0;
            var wideRange = searching || _diaryAllTime;

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
                DiaryFilter.Matches(_diaryCategoryFilter, e.Cat, StringComparer.Ordinal);
            // Group never returns null: it returns the placeholder "—" for an empty title, which is
            // what the row's App column shows. That placeholder is "no app" for the "-" entry.
            static string? AppValue(ReportData.DiaryEntry e)
            {
                var g = AppNames.Group(e.Window);
                return g == "—" ? null : g;
            }
            bool MatchesApp(ReportData.DiaryEntry e) =>
                DiaryFilter.Matches(_diaryAppFilter, AppValue(e), StringComparer.OrdinalIgnoreCase);
            bool MatchesPage(ReportData.DiaryEntry e) =>
                DiaryFilter.Matches(_diaryPageFilter, AppNames.Sub(e.Window), StringComparer.OrdinalIgnoreCase);
            bool MatchesTag(ReportData.DiaryEntry e) =>
                DiaryFilter.Matches(_diaryTagFilter, e.Tag, StringComparer.Ordinal);
            bool MatchesSearch(ReportData.DiaryEntry e) =>
                !searching ||
                DiaryFilter.SearchMatches(_diarySearchField, q, AppNames.Label(e.Window),
                    AppNames.Group(e.Window), AppNames.Sub(e.Window), e.Desc, e.Cat, DiaryTag.LabelOf(e.Tag));

            syncingFilters = true;

            // Rebuild app/page flyout checkbox lists from what's actually in the current scope
            var appsInView = rows.Where(e => MatchesCategory(e) && MatchesPage(e) && MatchesTag(e) && MatchesSearch(e))
                .Select(e => AppValue(e))
                .Where(a => a is { Length: > 0 })
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
            _diaryAppFilter = DiaryFilter.PruneToAvailable(_diaryAppFilter, appsInView);
            // G5: the checkbox list is built only while its flyout is open (Opening/Closed below).
            // If it is open now (a checkbox click re-rendered), rebuild it only if the option set
            // changed; otherwise just sync IsChecked to preserve scroll/focus (2026-10-01 rule),
            // keyed on what the open panel actually holds.
            appOptions = appsInView;
            if (appPanelOptions != null)
            {
                if (DiaryFilter.SameOptionSet(appPanelOptions, appsInView, StringComparer.OrdinalIgnoreCase))
                    SyncAppCheckboxes();
                else
                {
                    RebuildAppCheckboxList(appFlyoutContent, appsInView);
                    appPanelOptions = appsInView;
                }
            }

            var pagesInView = rows.Where(e => MatchesCategory(e) && MatchesApp(e) && MatchesTag(e) && MatchesSearch(e))
                .Select(e => AppNames.Sub(e.Window))
                .Where(p => p is { Length: > 0 })
                .Cast<string>()  // Explicit cast to clarify nullability for compiler
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            _diaryPageFilter = DiaryFilter.PruneToAvailable(_diaryPageFilter, pagesInView);
            // Same open-only rule as the App list above.
            pageOptions = pagesInView;
            if (pagePanelOptions != null)
            {
                if (DiaryFilter.SameOptionSet(pagePanelOptions, pagesInView, StringComparer.OrdinalIgnoreCase))
                    SyncPageCheckboxes();
                else
                {
                    RebuildPageCheckboxList(pageFlyoutContent, pagesInView);
                    pagePanelOptions = pagesInView;
                }
            }

            // Update all button texts and sync checkbox states for fixed filters (category/tag)
            // re-sync means updating IsChecked and button text
            var categoryLabels = DiaryCategory.EditableOptions
                .Where(o => _diaryCategoryFilter.Contains(o.Value))
                .Select(o => o.Label)
                .ToList();
            WithNoValueFirst(categoryLabels, _diaryCategoryFilter);
            categoryBtn.Content = DiaryFilter.ButtonLabel(AllCategories, categoryLabels);
            SyncCategoryCheckboxes();

            var appLabels = appsInView
                .Where(a => _diaryAppFilter.Contains(a, StringComparer.OrdinalIgnoreCase))
                .Cast<string>()
                .ToList();
            WithNoValueFirst(appLabels, _diaryAppFilter);
            appBtn.Content = DiaryFilter.ButtonLabel(AllApps, appLabels);

            var pageLabels = pagesInView
                .Where(p => _diaryPageFilter.Contains(p, StringComparer.OrdinalIgnoreCase))
                .Cast<string>()
                .ToList();
            WithNoValueFirst(pageLabels, _diaryPageFilter);
            pageBtn.Content = DiaryFilter.ButtonLabel(AllPages, pageLabels);

            var tagLabels = DiaryTag.Options
                .Where(o => o.Value != null && _diaryTagFilter.Contains(o.Value))
                .Select(o => o.Label)
                .Where(l => l != null)
                .Cast<string>()
                .ToList();
            WithNoValueFirst(tagLabels, _diaryTagFilter);
            tagBtn.Content = DiaryFilter.ButtonLabel(AllTags, tagLabels);
            SyncTagCheckboxes();

            allTimeBox.IsChecked = _diaryAllTime;
            syncingFilters = false;

            // Same five predicates decide what actually shows in the results list — the
            // dropdown-option narrowing above and the results below can never disagree with
            // each other, since both read from the same single definition of each filter.
            var filteredList = rows.Where(e =>
                MatchesCategory(e) && MatchesApp(e) && MatchesPage(e) && MatchesTag(e) && MatchesSearch(e)).ToList();
            var filtersActive = _diaryCategoryFilter.Count > 0 || _diaryAppFilter.Count > 0 ||
                _diaryPageFilter.Count > 0 || _diaryTagFilter.Count > 0;

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
                lastRows = new List<ReportData.DiaryEntry>();
                allFiltered = new List<ReportData.DiaryEntry>();
                selectedIds.Clear();
                UpdateMarkToolbar();
                return;
            }
            // G3: prune ticks against EVERY match, before the 300 cap below — the old prune ran
            // against the capped list, so a tick on a row past the cap silently vanished.
            allFiltered = filteredList;
            var matchIds = filteredList.Select(e => e.Id).ToHashSet();
            selectedIds.RemoveWhere(id => !matchIds.Contains(id));
            if (wideRange && filteredList.Count > maxSearchResults)
            {
                diaryResults.Children.Add(Dim(
                    $"{filteredList.Count} matches — showing the {maxSearchResults} most recent " +
                    "(subtotal above still covers all of them)."));
                filteredList = filteredList.Take(maxSearchResults).ToList();
            }
            lastRows = filteredList;
            UpdateMarkToolbar();
            // A genuine scope change (different day/search/filters/all-time) starts the reveal
            // count fresh; the periodic live-refresh timer re-running this same method with an
            // UNCHANGED scope must not — see _diaryRowsShown's own comment for why.
            var catStr = string.Join(",", _diaryCategoryFilter.OrderBy(x => x, StringComparer.Ordinal));
            var appStr = string.Join(",", _diaryAppFilter.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var pageStr = string.Join(",", _diaryPageFilter.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var tagStr = string.Join(",", _diaryTagFilter.OrderBy(x => x, StringComparer.Ordinal));
            var scopeKey = string.Join('|', _diaryDate, q, _diarySearchField, catStr, appStr, pageStr, tagStr, _diaryAllTime);
            if (scopeKey != _diaryRowsShownScopeKey)
            {
                _diaryRowsShown = DefaultDiaryRowsShown;
                _diaryShowAll = false;
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

        // Build checkbox lists for category/tag filters (now that RenderDiaryResults is defined)
        BuildCategoryCheckboxList(categoryFlyoutContent);
        BuildTagCheckboxList(tagFlyoutContent);

        // G5: the App/Page lists can hold one checkbox per distinct page in scope (thousands in
        // All time), so they are built only while their flyout is open and emptied on close.
        if (appBtn.Flyout is Flyout appFlyout)
        {
            appFlyout.Opening += (_, _) =>
            {
                RebuildAppCheckboxList(appFlyoutContent, appOptions);
                appPanelOptions = appOptions;
            };
            appFlyout.Closed += (_, _) =>
            {
                appFlyoutContent.Children.Clear();
                appPanelOptions = null;
            };
        }
        if (pageBtn.Flyout is Flyout pageFlyout)
        {
            pageFlyout.Opening += (_, _) =>
            {
                RebuildPageCheckboxList(pageFlyoutContent, pageOptions);
                pagePanelOptions = pageOptions;
            };
            pageFlyout.Closed += (_, _) =>
            {
                pageFlyoutContent.Children.Clear();
                pagePanelOptions = null;
            };
        }

        RenderDiaryResults();

        // Unlike the category/app/page boxes, this needs a full Render() (not just
        // RenderDiaryResults()) — it changes wideMode, which DiaryHeader() also reads to decide
        // the "TIME DIARY · ALL TIME" caption and whether the date-nav arrows/picker are
        // enabled, and DiaryHeader is only rebuilt on a full Render.
        allTimeBox.Checked += (_, _) => { if (!syncingFilters) { _diaryAllTime = true; Render(); } };
        allTimeBox.Unchecked += (_, _) => { if (!syncingFilters) { _diaryAllTime = false; Render(); } };
        clearFiltersBtn.Click += (_, _) =>
        {
            _diaryCategoryFilter.Clear();
            _diaryAppFilter.Clear();
            _diaryPageFilter.Clear();
            _diaryTagFilter.Clear();
            _diaryAllTime = false;
            // The search box sits directly above this button and reads as part of the same
            // filter row — leaving it untouched made "Clear filters" look broken when a typed
            // search term kept the list scoped after a click (2026-07-23 UX re-audit).
            _diarySearch = "";
            _diarySearchField = DiaryFilter.SearchField.All;
            // Full Render(), not RenderDiaryResults() — this button resets _diaryAllTime (and
            // now _diarySearch), both of which DiaryHeader()'s wideMode reads to decide the
            // "ALL TIME" caption and whether the date-nav arrows are enabled. The allTimeBox
            // checkbox's own handlers already call Render() for the same reason; this button
            // was the one sibling still calling the partial refresh and left the header stale.
            Render();
        };

        // G3: every Mark goes through the safe door (preview → confirm → backup → logged write →
        // Undo). Category: the ticks if any, else every match (M, uncapped). Tag: ticks only
        // (lead ruling 1).
        void StartMark(Button btn, ChangeColumn column, string? value, bool ticksOnly)
        {
            var t = selectedIds.Count;
            if (ticksOnly && t == 0) return;
            var ticked = t > 0;
            var targets = ticked ? selectedIds.ToList() : allFiltered.Select(e => e.Id).ToList();
            if (targets.Count == 0) return;
            var scope = DiaryScopeDescription();
            var scopeLine = ticked
                ? (t == 1 ? "The 1 ticked entry." : $"The {N0(t)} ticked entries.")
                : $"All {N0(targets.Count)} {(targets.Count == 1 ? "entry" : "entries")} matching your current view: {scope}.";
            var scopeText = ticked
                ? $"{N0(t)} ticked {(t == 1 ? "entry" : "entries")} - {scope}"
                : scope;
            _ = RunDiaryDoorAsync(btn, column, value, targets, scopeLine, scopeText);
        }
        markOnBtn.Click += (_, _) => StartMark(markOnBtn, ChangeColumn.Category, DiaryCategory.OnPlan, ticksOnly: false);
        markOffBtn.Click += (_, _) => StartMark(markOffBtn, ChangeColumn.Category, DiaryCategory.OffPlan, ticksOnly: false);
        markNeutralBtn.Click += (_, _) => StartMark(markNeutralBtn, ChangeColumn.Category, DiaryCategory.Neutral, ticksOnly: false);
        foreach (var (tagValue, btn) in tagMarkButtons)
        {
            var b = btn;
            b.Click += (_, _) => StartMark(b, ChangeColumn.Tag, tagValue, ticksOnly: true);
        }
        _diaryRenderResultsAction = RenderDiaryResults;
        _diaryUpdateToolbarAction = UpdateMarkToolbar;

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
        // Changing the field re-filters at once (no debounce); with an empty query nothing
        // visible changes, so skip the work. No full Render(): the header only depends on
        // whether search text is present, which a field change does not alter.
        searchFieldBox.SelectionChanged += (_, _) =>
        {
            var i = searchFieldBox.SelectedIndex;
            if (i < 0 || i >= DiaryFilter.SearchFields.Length) return;
            var field = DiaryFilter.SearchFields[i].Field;
            if (field == _diarySearchField) return;
            _diarySearchField = field;
            if (_diarySearch.Trim().Length > 0) RenderDiaryResults();
        };
        searchBox.TextChanged += (_, _) =>
        {
            _diarySearch = searchBox.Text;
            searchDebounce.Change(DiarySearchDebounceInterval, System.Threading.Timeout.InfiniteTimeSpan);
            SyncLiveRefresh();
        };

        // Only re-poll while today's rows are actually in scope with no search active — a
        // past-only day view is finished history (nothing new will ever appear), and a search
        // already re-renders itself on its own typing debounce above.
        //
        // G5: a tick that finds exactly the rows already shown (DiaryEntry is a record, so
        // SequenceEqual compares every field) changes nothing on screen, so it returns without
        // rebuilding the list; otherwise it renders from the rows it just loaded (no second query).
        _diaryLiveRefreshAction = () =>
        {
            var rows = LoadScopeRows();
            if (rows.SequenceEqual(loadedRows)) return;
            RenderDiaryResultsFrom(rows);
        };
        EnsureDiaryLiveRefreshTimer();
        SyncLiveRefresh();
    }

    /// <summary>The diary's free-text search box — split out of BuildDiarySection
    /// (2026-07-28 code-quality audit finding: that method was 389 lines) since this
    /// piece is pure "build one control, add it to Body," with no shared mutable state.</summary>
    private (TextBox SearchBox, ComboBox FieldBox) BuildDiarySearchBox()
    {
        var searchBox = new TextBox
        {
            PlaceholderText = $"Search the last {ConfigService.DiaryRetentionDays()} days' diary (app or description)…",
            Text = _diarySearch,
        };
        // Placeholder text alone isn't exposed to screen readers as an accessible
        // name — a static name here (audit finding #16) since the placeholder text
        // itself already varies with the retention setting.
        AutomationProperties.SetName(searchBox, "Search the time diary");

        // Which field the search looks in (G2): chooser first, box stretches. Seeded from the
        // static on every build so it survives navigation.
        var fieldBox = new ComboBox { MinWidth = 130 };
        AutomationProperties.SetName(fieldBox, "Search in");
        foreach (var (label, _) in DiaryFilter.SearchFields) fieldBox.Items.Add(label);
        fieldBox.SelectedIndex = Array.FindIndex(DiaryFilter.SearchFields, f => f.Field == _diarySearchField);

        var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(fieldBox, 0);
        Grid.SetColumn(searchBox, 1);
        row.Children.Add(fieldBox);
        row.Children.Add(searchBox);
        DiaryBody.Children.Add(row);
        return (searchBox, fieldBox);
    }

    /// <summary>Puts the "-" label first in a filter button's selected-label list when the
    /// "-" entry is selected (display order: "-" first).</summary>
    private static void WithNoValueFirst(List<string> labels, HashSet<string> selected)
    {
        if (selected.Contains(DiaryFilter.NoValue)) labels.Insert(0, DiaryFilter.NoValueLabel);
    }

    /// <summary>The category/app/page/tag filter row plus "All time" checkbox and "Clear
    /// filters" button, in their own horizontally-scrollable row — split out of
    /// BuildDiarySection for the same reason as BuildDiarySearchBox above.
    /// This method builds the controls once with fixed option lists for category/tag;
    /// RenderDiaryResults rebuilds app/page checkbox lists and re-syncs all checked states
    /// and button text on every call.</summary>
    private (Button CategoryBtn, Button AppBtn, Button PageBtn, Button TagBtn,
        CheckBox AllTimeBox, Button ClearFiltersBtn,
        StackPanel CategoryFlyoutContent, StackPanel TagFlyoutContent,
        StackPanel AppFlyoutContent, StackPanel PageFlyoutContent) BuildDiaryFilterRow()
    {
        // Helper to create a filter button with a scrollable flyout of checkboxes
        (Button, StackPanel) CreateFilterButton(string allLabel, string automationName, double minWidth)
        {
            var btn = new Button
            {
                Content = allLabel,
                MinWidth = minWidth,
                Padding = new Thickness(8, 4, 8, 4),
            };
            AutomationProperties.SetName(btn, automationName);

            var flyoutStack = new StackPanel { Spacing = 4 };
            var flyoutScroller = new ScrollViewer
            {
                MaxHeight = 320,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = flyoutStack,
            };

            var flyout = new Flyout { Content = flyoutScroller };
            btn.Flyout = flyout;

            return (btn, flyoutStack);
        }

        var (categoryBtn, categoryStack) = CreateFilterButton(AllCategories, "Filter diary by category", 140);
        var (appBtn, appStack) = CreateFilterButton(AllApps, "Filter diary by app", 150);
        var (pageBtn, pageStack) = CreateFilterButton(AllPages, "Filter diary by page", 180);
        var (tagBtn, tagStack) = CreateFilterButton(AllTags, "Filter diary by tag", 140);

        // Category/tag lists are built in BuildDiarySection as local functions
        // so they can access syncingFilters and RenderDiaryResults

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
        filterRow.Children.Add(categoryBtn);
        filterRow.Children.Add(appBtn);
        filterRow.Children.Add(pageBtn);
        filterRow.Children.Add(tagBtn);
        filterRow.Children.Add(allTimeBox);
        filterRow.Children.Add(clearFiltersBtn);
        // This row's combined MinWidth (categoryBox+appBox+pageBox+checkbox+button, ~750-800px)
        // can exceed the actual content width at the app's enforced 900px window floor once the
        // NavigationView pane and page padding are subtracted — without this, "Clear filters"
        // and the "All time" checkbox can be clipped off-screen with no way to reach them
        // (2026-07-24 audit finding #1, a regression from the App/Page column split). Same
        // HorizontalScrollBarVisibility="Auto" treatment as diaryScroller below, just for one row.
        DiaryBody.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 0, 0, 8),
            Content = filterRow,
        });

        return (categoryBtn, appBtn, pageBtn, tagBtn, allTimeBox, clearFiltersBtn, categoryStack, tagStack, appStack, pageStack);
    }

    /// <summary>The "select all / mark on-plan / off-plan / neutral" bulk-action toolbar —
    /// split out of BuildDiarySection for the same reason as the two methods above.
    /// UpdateMarkToolbar (which stays in BuildDiarySection, since it also touches
    /// selectedIds/lastRows) flips these controls' enabled/label state as the selection
    /// changes; this method only constructs them, starting disabled/empty.</summary>
    private (CheckBox SelectAllBox, TextBlock SelectedLabel, Button MarkOnBtn, Button MarkOffBtn,
        Button MarkNeutralBtn, List<(string? Tag, Button Btn)> TagMarkButtons)
        BuildDiaryMarkToolbar()
    {
        var markToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8),
        };
        // IsThreeState so it can show "some but not all selected" as a
        // dash rather than lying with a plain checked/unchecked state.
        var selectAllBox = new CheckBox { Content = "Select all", IsThreeState = true, IsEnabled = false };
        AutomationProperties.SetAutomationId(selectAllBox, "diary-select-all");
        ToolTipService.SetToolTip(selectAllBox, "Ticks the rows shown. Mark without ticks already covers every match.");
        var selectedLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        AutomationProperties.SetAutomationId(selectedLabel, "diary-tick-label");
        var markOnBtn = new Button { Content = "Mark on-plan", IsEnabled = false };
        var markOffBtn = new Button { Content = "Mark off-plan", IsEnabled = false };
        var markNeutralBtn = new Button { Content = "Mark neutral", IsEnabled = false };
        AutomationProperties.SetAutomationId(markOnBtn, "diary-mark-on");
        AutomationProperties.SetAutomationId(markOffBtn, "diary-mark-off");
        AutomationProperties.SetAutomationId(markNeutralBtn, "diary-mark-neutral");
        markToolbar.Children.Add(selectAllBox);
        markToolbar.Children.Add(selectedLabel);
        markToolbar.Children.Add(markOnBtn);
        markToolbar.Children.Add(markOffBtn);
        markToolbar.Children.Add(markNeutralBtn);
        DiaryBody.Children.Add(markToolbar);

        // Second row, its own independent axis (2026-08-07 request) — kept off the row
        // above rather than appended to it: on-plan/off-plan/neutral plus five tags plus
        // "Clear tag" would be eight buttons wide, well past what a plain horizontal
        // StackPanel (no wrapping in this app's toolkit) can show without running off the
        // page. A leading "Tag:" label reads it as the same kind of row as the category
        // one above, not an unrelated second toolbar.
        var tagToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8),
        };
        tagToolbar.Children.Add(new TextBlock
        {
            Text = "Tag:",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        var tagMarkButtons = new List<(string? Tag, Button Btn)>();
        foreach (var (label, value) in DiaryTag.Options)
        {
            var btn = new Button { Content = $"Mark {label}", IsEnabled = false };
            tagToolbar.Children.Add(btn);
            tagMarkButtons.Add((value, btn));
        }
        // (none) last, not first — the five real tags are the common case and read left to
        // right in the same order as every other tag picker in this app; clearing is the
        // exception, same reasoning EditDiaryEntryDialog's combo puts "(none)" first in a
        // dropdown (scanned top-down) but this button belongs at the end of a row (scanned
        // left-right, common actions first).
        var clearTagBtn = new Button { Content = "Clear tag", IsEnabled = false };
        tagToolbar.Children.Add(clearTagBtn);
        tagMarkButtons.Add((null, clearTagBtn));
        DiaryBody.Children.Add(tagToolbar);

        // G3: the result bar (running / success + Undo / undo result / errors), directly below
        // the tag row so the Mark buttons never move when it opens or closes. Its content is the
        // page-level _diaryResult, re-applied here on every build of the section.
        var resultBar = new InfoBar { IsOpen = false, Margin = new Thickness(0, 0, 0, 8) };
        AutomationProperties.SetAutomationId(resultBar, "diary-result-bar");
        resultBar.Closed += (_, e) =>
        {
            if (e.Reason == InfoBarCloseReason.CloseButton) _diaryResult = null;
        };
        DiaryBody.Children.Add(resultBar);
        _diaryResultBar = resultBar;
        ApplyDiaryResult();

        return (selectAllBox, selectedLabel, markOnBtn, markOffBtn, markNeutralBtn, tagMarkButtons);
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
            // Back to Auto (2026-08-13 report: "remove the horizontal scroll bar") — Visible was
            // set 2026-07-28 because the row's own width had silently outgrown the page's max
            // content width (DiaryListWidth was stale — see its own comment), so a permanently
            // shown scrollbar was the only thing standing between the user and silently-clipped
            // Edit/Split buttons. That root cause is fixed now (DiaryListWidth recomputed, Page/
            // Details narrowed), so the row fits without scrolling on any window this app
            // supports — Auto still engages if a future column ever grows past that again, it
            // just doesn't sit there permanently, mostly empty, when there's nothing to scroll.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = diaryResults,
        };
        DiaryBody.Children.Add(diaryScroller);
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

    // ── G3: the safe door, page side ─────────────────────────────────────
    // The write itself is DiaryChangeService (plain C#, all-or-nothing, logged, undoable). The
    // old per-page Mark wrote each row back from the page's snapshot of it (whole-row UPDATE),
    // reached only the ≤300 listed rows, cleared the ticks, and taught keywords one config write
    // at a time. Now: preview off the UI thread → confirm dialog → backup + write off the UI
    // thread → result bar with Undo.

    /// <summary>"{period}{filters}" as the dialog's scope line and the change history show it:
    /// "Thu 29 Oct 2026" or "All time", then " - Category: Neutral", " - App: …", " - Page: …",
    /// " - Tag: …", " - Search "q" in Window" for each active filter.</summary>
    private static string DiaryScopeDescription()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var q = _diarySearch.Trim();
        var sb = new System.Text.StringBuilder(q.Length > 0 || _diaryAllTime
            ? "All time"
            : _diaryDate.ToString("ddd d MMM yyyy", inv));
        static string Shown(string v) => v == DiaryFilter.NoValue ? DiaryFilter.NoValueLabel : v;
        void Add(string name, IEnumerable<string> labels)
        {
            var list = labels.ToList();
            if (list.Count > 0) sb.Append(" - ").Append(name).Append(": ").Append(string.Join(", ", list));
        }
        Add("Category", _diaryCategoryFilter.OrderBy(v => v, StringComparer.Ordinal).Select(v =>
            v == DiaryFilter.NoValue ? DiaryFilter.NoValueLabel
            : Array.Find(DiaryCategory.EditableOptions, o => o.Value == v).Label ?? v));
        Add("App", _diaryAppFilter.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).Select(Shown));
        Add("Page", _diaryPageFilter.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).Select(Shown));
        Add("Tag", _diaryTagFilter.OrderBy(v => v, StringComparer.Ordinal).Select(v =>
            v == DiaryFilter.NoValue ? DiaryFilter.NoValueLabel : DiaryTag.LabelOf(v) ?? v));
        if (q.Length > 0)
        {
            var field = Array.Find(DiaryFilter.SearchFields, f => f.Field == _diarySearchField).Label ?? "All fields";
            sb.Append(" - Search \"").Append(q).Append("\" in ").Append(field);
        }
        return sb.ToString();
    }

    private void SetDiaryDoorBusy(bool busy)
    {
        _diaryDoorBusy = busy;
        _diaryUpdateToolbarAction?.Invoke();
        ApplyDiaryResult();
    }

    private void ShowDiaryResult(DiaryResultState? state)
    {
        _diaryResult = state;
        ApplyDiaryResult();
    }

    /// <summary>Pushes _diaryResult into the bar of the current build of the section (if one is
    /// on screen). The Undo action button is rebuilt each time so its enabled state follows
    /// _diaryDoorBusy.</summary>
    private void ApplyDiaryResult()
    {
        var bar = _diaryResultBar;
        if (bar is null) return;
        var s = _diaryResult;
        if (s is null) { bar.IsOpen = false; bar.ActionButton = null; return; }
        bar.Severity = s.Severity;
        bar.Title = s.Title;
        bar.Message = s.Message;
        bar.IsClosable = s.Closable;
        if (s.UndoChangeId is long id)
        {
            var undo = new Button
            {
                Content = s.Undoing ? "Undoing..." : "Undo",
                IsEnabled = !s.Undoing && !_diaryDoorBusy,
            };
            AutomationProperties.SetAutomationId(undo, "diary-result-undo");
            undo.Click += (_, _) => _ = UndoFromDiaryBarAsync(id);
            bar.ActionButton = undo;
        }
        else bar.ActionButton = null;
        bar.IsOpen = true;
    }

    private static void FocusBack(Button btn)
    {
        try { if (btn.XamlRoot is not null) btn.Focus(FocusState.Programmatic); }
        catch (Exception ex) { Log.Error("ReportsPage.DiaryDoor.FocusBack", ex); }
    }

    /// <summary>One bulk Mark through the door: preview (off the UI thread; "Checking N rows..."
    /// after ~300 ms) → confirm dialog → backup + write (off the UI thread) → result bar.
    /// Cancel writes nothing. The ticks are never touched here (addendum 3).</summary>
    private async Task RunDiaryDoorAsync(Button clicked, ChangeColumn column, string? newValue,
        IReadOnlyCollection<long> targets, string scopeLine, string scopeText)
    {
        if (_diaryDoorBusy || targets.Count == 0) return;
        var word = DiaryChangeText.ValueWord(column, newValue);
        var req = new ChangeRequest(column, newValue, targets, scopeText,
            TeachKeywords: column == ChangeColumn.Category);
        var before = _diaryResult;
        SetDiaryDoorBusy(true);
        try
        {
            ChangePreview preview;
            try
            {
                var task = Task.Run(() => { using var db = new Database(); return DiaryChangeService.Preview(db, req); });
                if (await Task.WhenAny(task, Task.Delay(300)) != task)
                    ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Informational,
                        $"Checking {DiaryChangeText.Rows(targets.Count)}...", "", Closable: false));
                preview = await task;
            }
            catch (Exception ex)
            {
                Log.Error("ReportsPage.DiaryDoor.Preview", ex);
                ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Error,
                    "Couldn't check those rows - nothing was changed.", ex.Message, Closable: true));
                return;
            }
            ShowDiaryResult(before);

            var dialog = BuildDiaryDoorDialog(column, newValue, word, preview, scopeLine);
            var answer = await DialogGate.ShowAsync(dialog);
            if (answer != ContentDialogResult.Primary || preview.Total == 0) return;

            ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Informational,
                DiaryChangeText.RunningTitle(column, newValue, preview.Total),
                "Backing up your diary first.", Closable: false));
            try
            {
                var result = await Task.Run(() => { using var db = new Database(); return DiaryChangeService.Apply(db, req); });
                ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Success,
                    DiaryChangeText.DoneTitle(column, newValue, result.Changed),
                    column == ChangeColumn.Category
                        ? "A backup was made. Undo puts the old categories back and forgets the keywords this mark added."
                        : "A backup was made. Undo puts the old tags back.",
                    Closable: true,
                    UndoChangeId: result.Changed > 0 ? result.ChangeId : null));
                if (result.KeywordsChanged) (App.MainWindow as MainWindow)?.RestartTracker();
            }
            catch (BackupFailedException ex)
            {
                Log.Error("ReportsPage.DiaryDoor.Backup", ex);
                ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Error, "Not marked - the backup failed",
                    $"Your diary was left untouched. {ex.Message}", Closable: true));
            }
            catch (Exception ex)
            {
                Log.Error("ReportsPage.DiaryDoor.Apply", ex);
                ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Error, "Couldn't mark those rows",
                    $"Nothing was changed. {ex.Message}", Closable: true));
            }
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.DiaryDoor", ex);
        }
        finally
        {
            SetDiaryDoorBusy(false);
            _diaryRenderResultsAction?.Invoke();
            FocusBack(clicked);
        }
    }

    /// <summary>Undo from the result bar — same door, same skip rule as Settings ▸ Change
    /// history. The ticks stay; a tick drops only if the restored value stops matching the filter.</summary>
    private async Task UndoFromDiaryBarAsync(long changeId)
    {
        if (_diaryDoorBusy) return;
        ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Informational, "Undoing...", "",
            Closable: false, UndoChangeId: changeId, Undoing: true));
        SetDiaryDoorBusy(true);
        try
        {
            var r = await Task.Run(() => { using var db = new Database(); return DiaryChangeService.Undo(db, changeId); });
            ShowDiaryResult(r.Restored == 0 && r.Skipped > 0
                ? new DiaryResultState(InfoBarSeverity.Warning, "Nothing restored", DiaryChangeText.AllSkipped(r.Skipped), Closable: true)
                : new DiaryResultState(InfoBarSeverity.Success, DiaryChangeText.UndoneTitle(r.Restored),
                    r.Skipped > 0 ? DiaryChangeText.SomeSkipped(r.Skipped) : "", Closable: true));
            if (r.KeywordsChanged) (App.MainWindow as MainWindow)?.RestartTracker();
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.DiaryDoor.Undo", ex);
            ShowDiaryResult(new DiaryResultState(InfoBarSeverity.Error, "Couldn't undo",
                $"Nothing was changed. {ex.Message}", Closable: true));
        }
        finally
        {
            SetDiaryDoorBusy(false);
            _diaryRenderResultsAction?.Invoke();
        }
    }

    /// <summary>The confirm dialog (g3-confirm-dialog): scope line, the four-row count table that
    /// always adds up (filled + overwritten = total; unchanged excluded), the safety note.
    /// Default button is Cancel, so Enter/Esc never writes.</summary>
    private ContentDialog BuildDiaryDoorDialog(ChangeColumn column, string? newValue, string word,
        ChangePreview p, string scopeLine)
    {
        var secondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var content = new StackPanel { Spacing = 10, MinWidth = 420 };

        var scope = new TextBlock
        {
            Text = scopeLine, FontSize = 12, Foreground = secondary, TextWrapping = TextWrapping.Wrap,
            MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(scope, scopeLine);
        AutomationProperties.SetAutomationId(scope, "g3-confirm-scope");
        content.Children.Add(scope);

        var clearing = column == ChangeColumn.Tag && newValue is null;
        var (filledLabel, overLabel, sameLabel) = column == ChangeColumn.Category
            ? ("No category yet - will be filled", "Another category - will be overwritten", $"Already {word} - stays as it is")
            : clearing
                ? ("No tag yet", "Has a tag - will be cleared", "No tag - stays as it is")
                : ("No tag yet - will be filled", "Another tag - will be overwritten", $"Already {word} - stays as it is");

        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var row = 0;
        void AddCount(string id, string label, int value, bool bold)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var weight = bold ? FontWeights.SemiBold : FontWeights.Normal;
            var l = new TextBlock { Text = label, FontSize = 14, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetAutomationId(l, id);
            AutomationProperties.SetName(l, $"{label}: {N0(value)}");
            var n = new TextBlock
            {
                Text = N0(value), FontSize = 14, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetRow(l, row); Grid.SetRow(n, row);
            Grid.SetColumn(n, 1);
            grid.Children.Add(l); grid.Children.Add(n);
            row++;
        }
        AddCount("g3-confirm-filled", filledLabel, p.Filled, bold: false);
        AddCount("g3-confirm-overwritten", overLabel, p.Overwritten, bold: p.Overwritten > 0);
        AddCount("g3-confirm-unchanged", sameLabel, p.Unchanged, bold: false);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var divider = new Border
        {
            Height = 1,
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        };
        Grid.SetRow(divider, row); Grid.SetColumnSpan(divider, 2);
        grid.Children.Add(divider);
        row++;
        AddCount("g3-confirm-total", "Rows that will change", p.Total, bold: true);
        content.Children.Add(grid);

        if (p.Total == 0)
            content.Children.Add(new TextBlock
            {
                Text = clearing ? "None of these rows has a tag." : $"Every one of these rows is already {word}.",
                TextWrapping = TextWrapping.Wrap,
            });

        var note = new TextBlock
        {
            Text = column == ChangeColumn.Category
                ? "Your diary is backed up first. You can Undo this right after, or later in Settings > Change history. " +
                  "Marking also teaches Planillium the activity keywords of these rows; Undo forgets the ones this mark added."
                : "Your diary is backed up first. You can Undo this right after, or later in Settings > Change history.",
            FontSize = 12, Foreground = secondary, TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(note, "g3-confirm-note");
        content.Children.Add(note);

        var dialog = DialogControls.Build(XamlRoot,
            DiaryChangeText.ConfirmTitle(column, newValue, p.Total),
            content,
            primaryButtonText: DiaryChangeText.PrimaryText(column, newValue, p.Total),
            closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Close,
            isPrimaryButtonEnabled: p.Total > 0);
        AutomationProperties.SetAutomationId(dialog, "g3-confirm-dialog");
        return dialog;
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
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            // Its own column (2026-08-07 report: was riding along inside the Details column as
            // a " · #Label" suffix, easy to miss next to the duration and liable to get
            // ellipsis-trimmed off first). Narrower than Category — tag labels are short and
            // most rows have none at all, so "—" is the common case here, same convention the
            // Page column already uses for "nothing to show." Narrowed further 2026-08-28 (see
            // DiaryListWidth's own comment) — the longest label ("Procrastination") already
            // needed the ellipsis+tooltip at 90, so shrinking to 75 just makes that the norm
            // instead of the rare case, same tradeoff Page/Details already made.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
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
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            // Page and Details narrowed from 210/260 (2026-08-13 report, "remove the horizontal
            // scroll bar... narrow the descriptions column truncating the name and showing …"),
            // then again from 130/160 (2026-08-28 report — the 08-13 pass never accounted for
            // showDate widening the Time column by 40px in search/All-time view, so the row
            // still overflowed its budget exactly when a search or filter was active, e.g. the
            // "unacc" search that surfaced this). Both already ellipsis-trim with a tooltip for
            // the full text (TextTrimming.CharacterEllipsis below, same pattern App already
            // used) — narrowing just makes that the common case instead of the rare one, so the
            // row's natural width stops outgrowing the page (see DiaryListWidth's own comment).
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
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
            var tagLabelText = DiaryTag.LabelOf(tag) ?? "—";
            var tagText = new TextBlock
            {
                Text = tagLabelText,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (DiaryTag.LabelOf(tag) is { } realTagLabel) ToolTipService.SetToolTip(tagText, realTagLabel);
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
            // column above. The tag used to append here as a " · #Label" suffix (2026-08-06)
            // but moved to its own column (2026-08-07 report) — easy to miss buried next to
            // the duration, and the first thing ellipsis-trimming would cut off.
            var detailsText = !descInPage && desc is { Length: > 0 } ? $"“{desc}” {dur}m" : $"{dur}m";
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
            Grid.SetColumn(tagText, 3);
            Grid.SetColumn(appText, 4);
            Grid.SetColumn(pageText, 5);
            Grid.SetColumn(details, 6);
            Grid.SetColumn(edit, 7);
            Grid.SetColumn(split, 8);
            row.Children.Add(time);
            row.Children.Add(catText);
            row.Children.Add(tagText);
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
        var shown = _diaryShowAll ? diary.Count : Math.Min(_diaryRowsShown, diary.Count);

        // G5 (2026-10-28): the revealed rows go through an ItemsRepeater, which creates a row
        // only when it comes into the diary scroller's viewport (plus a small cache) and drops it
        // when it leaves. Building every revealed row eagerly in this StackPanel cost about
        // 0.5 MB per row — "Show all" on the 300-row cap held ~160 MB of rows of which ~15 are
        // ever on screen. Same rows, order, spacing (4) and paging row as before.
        var revealed = new System.Collections.ObjectModel.ObservableCollection<ReportData.DiaryEntry>();
        for (var i = 0; i < shown; i++)
            revealed.Add(diary[i]);
        var repeater = new ItemsRepeater
        {
            Layout = new StackLayout { Spacing = 4 },
            ItemTemplate = new DiaryRowFactory(BuildRow),
            ItemsSource = revealed,
        };
        list.Children.Add(repeater);

        // One batch at a time, not the whole rest in one go (2026-07-28 request) — a busy
        // search/date-range match can leave hundreds of rows hidden behind this button, and
        // building them all at once defeats the whole point of DefaultDiaryRowsShown above
        // (the same non-virtualizing-StackPanel cost that capped the initial render). Re-adds
        // itself after each batch if there's still more left, so repeated clicks keep working.
        //
        // G2 (2026-10-27): one row of "Showing N of M" plus graded links — Show 50/100/500/1000
        // more (only increments smaller than what is still hidden) and "Show all". "Show all"
        // is sticky (_diaryShowAll) so a live refresh keeps every row shown.
        StackPanel? pagingRow = null;
        void ShowNext(int count)
        {
            if (pagingRow != null) list.Children.Remove(pagingRow);
            var next = Math.Min(shown + count, diary.Count);
            for (var i = shown; i < next; i++)
                revealed.Add(diary[i]);
            shown = next;
            _diaryRowsShown = next;
            AddPagingRowIfNeeded();
        }
        void AddPagingRowIfNeeded()
        {
            pagingRow = null;
            if (shown >= diary.Count) return;
            var hidden = diary.Count - shown;
            pagingRow = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 6, 0, 0),
            };
            var status = Dim($"Showing {shown} of {diary.Count}");
            status.VerticalAlignment = VerticalAlignment.Center;
            status.Margin = new Thickness(0, 0, 8, 0);
            pagingRow.Children.Add(status);
            foreach (var k in DiaryFilter.IncrementsOffered(hidden))
            {
                var inc = k;
                var btn = new HyperlinkButton { Content = $"Show {inc} more" };
                btn.Click += (_, _) => { _diaryShowAll = false; ShowNext(inc); };
                pagingRow.Children.Add(btn);
            }
            var allBtn = new HyperlinkButton { Content = $"Show all ({hidden})" };
            allBtn.Click += (_, _) => { _diaryShowAll = true; ShowNext(hidden); };
            pagingRow.Children.Add(allBtn);
            list.Children.Add(pagingRow);
        }
        AddPagingRowIfNeeded();
        return list;
    }

    /// <summary>Builds one diary row per realized item of the diary's ItemsRepeater (G5).
    /// Rows are not reused: each row's handlers capture its own entry, so a recycled row is
    /// removed from the repeater and dropped (the repeater leaves recycling to the factory;
    /// an element left in place would stay in its Children for good), and a fresh one is
    /// built when the entry comes back into view. BuildRow reads selection state as it builds,
    /// so a row created late still shows the right checked state.</summary>
    private sealed partial class DiaryRowFactory(Func<ReportData.DiaryEntry, Grid> buildRow) : IElementFactory
    {
        public UIElement GetElement(ElementFactoryGetArgs args) => buildRow((ReportData.DiaryEntry)args.Data);

        public void RecycleElement(ElementFactoryRecycleArgs args)
        {
            if (args.Parent is Panel panel)
                panel.Children.Remove(args.Element);
        }
    }
}
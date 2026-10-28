using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _initialising = true;

    /// <summary>Previous salary: built in code so it shares <see cref="RateRow"/> with the payable
    /// tag rows (B1, 2026-10-23: same layout, same money box).</summary>
    private readonly MoneyBox MonthlyIncomeBox = new() { Header = "Previous salary — monthly net (€)", Width = 240, KeepsStoredValue = true };

    /// <summary>Set when Tab is pressed in the new-tag line, so a Tab out of it is not treated
    /// like a click elsewhere (which clears the line).</summary>
    private bool _tabPending;

    /// <summary>The SCORING section's inputs, keyed by their config.json key — built once in
    /// <see cref="BuildScoringSection"/> from <see cref="ScoringRules.All"/> so load and save
    /// both iterate the same table the score formula itself reads from.</summary>
    private readonly Dictionary<string, NumberBox> _scoringBoxes = new(StringComparer.Ordinal);

    /// <summary>The section panels, in the same order as the menu entries in XAML — index i of
    /// SectionList shows index i of this. Kept as one list so the two can't drift into showing
    /// the wrong panel for a click; the pairing is asserted once, in the constructor.</summary>
    private readonly StackPanel[] _panels;

    /// <summary>Which section was last open, remembered for the lifetime of the app rather than
    /// per page instance: navigating away and back re-creates this page from scratch, and landing
    /// on "General" every time is exactly the annoyance a menu is supposed to remove.</summary>
    private static int _lastSection;

    public SettingsPage()
    {
        InitializeComponent();
        _panels = [GeneralPanel, HoursPanel, IncomePanel, ScoringPanel, KeywordsPanel, IdlePanel, TickTickPanel, AiPanel, DataPanel, HistoryPanel];
        if (_panels.Length != SectionList.Items.Count)
            throw new InvalidOperationException(
                $"Settings has {SectionList.Items.Count} menu entries but {_panels.Length} panels — " +
                "every menu entry must open a section.");
        BuildScoringSection();
        IncomeFocusPark.LostFocus += (_, _) => IncomeFocusPark.IsTabStop = false;
        MonthlyIncomeBox.Committed += (_, _) => { if (!_initialising) SaveIncome(); };
        MonthlyIncomeBox.Submitted += (_, _) => Park(MonthlyIncomeBox);
        MonthlyIncomeBox.Cancelled += (_, _) => Park(MonthlyIncomeBox);   // B1h: Esc leaves the box
        WatchMoneyBox(MonthlyIncomeBox, () => "Previous salary needs a number");
        SalaryHost.Children.Add(RateRow(MonthlyIncomeBox, null));
        NewPaidTagRate.Value = double.NaN;
        WatchMoneyBox(NewPaidTagRate, null);
        var (lineHint, showLineHint) = ErrorHint();
        _showNewTagHint = showLineHint;
        NewPaidTagHintHost.Children.Add(lineHint);
        NewPaidTagRate.NegativeChanged += (_, _) => SyncNewTagHint();
        // B1d: a refusal message stays until the line is actually changed (compared by value, so a
        // text re-format of the same number does not wipe the message it just caused).
        NewPaidTagRate.ValueChanged += (_, _) => { if (!NewPaidTagRate.Value.Equals(_refusedAmount)) ClearNewTagError(); };
        NewPaidTagName.TextChanged += (_, _) => { if ((NewPaidTagName.Text ?? "").Trim() != _refusedName) ClearNewTagError(); };
        // handledEventsToo: a click on a button or text box marks the press handled, but it must
        // still count as "outside the new-tag line".
        AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(Page_PointerPressed), true);
        AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(Page_PointerReleased), true);
        SectionList.SelectedIndex = _lastSection;
        // The strip is fixed at two lines so it can't resize the page (see XAML), which means a
        // long message trims. Mirroring it into the tooltip here rather than at ~20 assignment
        // sites keeps every one of them a plain `SaveStatus.Text = ...` and makes it impossible
        // for a new one to forget.
        SaveStatus.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) =>
            ToolTipService.SetToolTip(SaveStatus, SaveStatus.Text.Length > 0 ? SaveStatus.Text : null));
        Loaded += (_, _) =>
        {
            var theme = StateService.Load().Theme;
            ThemeChoice.SelectedIndex = theme switch
            {
                "light" => 1,
                "dark" => 2,
                _ => 0,
            };
            OpacitySlider.Value = StateService.Load().Opacity;
            StartupToggle.IsOn = StartupService.IsEnabled;
            YourName.Text = ConfigService.UserName;
            RefreshTickTickStatus();
            RefreshAiStatus();
            LoadRules();
            LoadIncome();
            // After LoadRules, not before: the keyword counts are read off the boxes it fills.
            RefreshSummaries();
            _initialising = false;

            RefreshTrackerInfo();
            if (App.MainWindow is MainWindow win) win.TrackingStateChanged += RefreshTrackerInfo;
            DataInfo.Text = $"{AppInfo.DisplayName} v{AppVersion.Current}\nShared data folder: " + AppPaths.Root;
        };
        // Without this, the tracker-status paragraph below stays subscribed to a page
        // instance the Frame has already discarded — harmless here (MainWindow outlives
        // every page) but leaves a dangling handler each time Settings is re-navigated to
        // (2026-07-23 UX re-audit fix).
        Unloaded += (_, _) =>
        {
            if (App.MainWindow is MainWindow win) win.TrackingStateChanged -= RefreshTrackerInfo;
        };
    }

    /// <summary>Shows the chosen section and hides the rest. Nothing is created or destroyed
    /// here — every panel stays loaded, so the values in a section you haven't opened are still
    /// the ones SaveRules writes, and switching costs a visibility flip rather than a rebuild.
    /// The scroll position resets so a section can't open halfway down.</summary>
    private void Section_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Clicking the already-selected row, or a click that lands between rows, leaves the
        // ListView with nothing selected — restore rather than showing an empty pane.
        if (SectionList.SelectedIndex < 0)
        {
            SectionList.SelectedIndex = _lastSection;
            return;
        }
        _lastSection = SectionList.SelectedIndex;
        for (var i = 0; i < _panels.Length; i++)
            _panels[i].Visibility = i == _lastSection ? Visibility.Visible : Visibility.Collapsed;
        if (_panels[_lastSection] == HistoryPanel) LoadChangeHistory();
        SectionScroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    /// <summary>
    /// Fills each menu entry with a one-line summary of what's inside that section, so the menu
    /// reads as a settings overview rather than seven labels (2026-08-04). Every
    /// figure here is read back from the same source the section's own controls load from —
    /// never from the controls' current text — so a header can't show a value that failed
    /// validation and was never actually saved.
    ///
    /// Called after load and after every successful save/toggle. Cheap: config reads are cached
    /// by ConfigService, and nothing here touches the database.
    /// </summary>
    private void RefreshSummaries()
    {
        var name = ConfigService.UserName;
        var theme = StateService.Load().Theme switch
        {
            "light" => "Light",
            "dark" => "Dark",
            _ => "Follow Windows",
        };
        // Kept deliberately terse. A summary gets two wrapped lines in a 200px-wide menu entry,
        // and TextTrimming means an overlong one doesn't complain, it just quietly loses its
        // tail — so the leading item has to be the one worth reading.
        SumGeneral.Text = string.Join(" · ", new[]
        {
            name.Length > 0 ? name : "No name set",
            theme,
            $"{(int)StateService.Load().Opacity}%",
            StartupService.IsEnabled ? "autostart on" : "autostart off",
        });

        SumHours.Text = $"{ConfigService.WorkStartTime().ToIsoTimeOfDay()}–" +
                        $"{ConfigService.WorkEndTime().ToIsoTimeOfDay()} · " +
                        $"review {EodTime()} · idle {ConfigService.IdleThresholdMinutes()}m";

        SumIncome.Text = $"{MainWindow.FormatEur(ConfigService.PotentialMonthlyIncomeEur())}/mo · " +
                         (ConfigService.IsEmployed() ? "employed" : "unemployed");

        // The two rules that describe the shape of the economy best — the full set is one click
        // away, and a 12-value summary would be unreadable at this size.
        SumScoring.Text = $"{ConfigService.ScoringRate("task_completed"):+#;-#;0} per task done · " +
                          $"{ConfigService.ScoringRate("task_overdue_penalty"):+#;-#;0} per task missed";

        SumKeywords.Text = $"{KeywordCount(RulesOn)} on-plan · {KeywordCount(RulesOff)} off-plan · " +
                           $"{KeywordCount(RulesNeutral)} neutral";
        SumIdle.Text = $"{KeywordCount(IdleOn)} on-plan · {KeywordCount(IdleOff)} off-plan · " +
                       $"{KeywordCount(IdleNeutral)} neutral";
        // SumTickTick is deliberately not set here — RefreshTickTickStatus owns every piece of
        // TickTick display state and is already called from all three places it can change.
    }

    /// <summary>Non-blank lines in one of the keyword boxes — the same "one per line" rule
    /// SaveRules' own Lines() helper applies, so the count can't disagree with what gets
    /// saved.</summary>
    private static int KeywordCount(TextBox box) =>
        box.Text.Split('\n', '\r').Count(l => l.Trim().Length > 0);

    private static string EodTime() =>
        ConfigService.Root.TryGetProperty("end_of_day_summary_time", out var v) &&
        v.GetString() is { Length: > 0 } s ? s : "20:00";

    /// <summary>Re-reads the tracker's actual running state — called on load AND whenever
    /// the tray's Pause/Resume toggle fires, so this paragraph can't disagree with the tray
    /// menu's own label while Settings sits open (2026-07-23 UX re-audit: previously computed
    /// once at page-load only).</summary>
    private void RefreshTrackerInfo()
    {
        var win = App.MainWindow as MainWindow;
        TrackerInfo.Text = win?.Tracker is { Running: true }
            // Both figures were hardcoded into this sentence and would have started lying the
            // moment the diary window became configurable (2026-08-04).
            ? $"This app is tracking your activity ({ActivityTracker.PollSeconds}s polls, " +
              $"diary {ConfigService.DiaryStartTime().ToIsoTimeOfDay()}–{ConfigService.DiaryEndTime().ToIsoTimeOfDay()})."
            // Running can be false either because it's paused from the tray (a normal,
            // deliberate state, 2026-07-23 "Pause tracking") or because startup genuinely
            // failed — phrased so it doesn't accuse a deliberate pause of being a bug.
            : "Tracking isn't running — resume it from the tray icon, or check " +
              "data/mentor-winui.log if you didn't pause it yourself.";
    }

    /// <summary>Creates one input per <see cref="ScoringRules.All"/> entry. Runs from the
    /// constructor, not Loaded: LoadRules() (which fills the values) runs on Loaded and needs
    /// the boxes to already exist. Where they sit is <see cref="LayoutScoringGrid"/>'s job.</summary>
    private void BuildScoringSection()
    {
        foreach (var rule in ScoringRules.All)
        {
            var box = new NumberBox
            {
                Header = rule.Label,
                Value = rule.Default,
                Minimum = rule.Min,
                Maximum = rule.Max,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            // Same autosave-on-change contract as every other field in this group — see
            // Rules_Changed's doc comment for why nothing here has a Save button.
            box.ValueChanged += Rules_ValueChanged;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, rule.Label);
            ScoringGrid.Children.Add(box);
            _scoringBoxes[rule.Key] = box;
        }
        LayoutScoringGrid(TwoColumnMinWidth);
    }

    /// <summary>Below this, the scoring inputs go one per row. Measured, not guessed: the widest
    /// label in <see cref="ScoringRules.All"/> ("Overdue penalty repeats for (days)") needs about
    /// 215px, and a NumberBox header neither wraps nor reports that it clipped — it just renders
    /// a different, shorter setting name. Two columns plus the 12px gap therefore need ~440.</summary>
    private const double TwoColumnMinWidth = 440;

    private int _scoringColumns;

    /// <summary>Reflows the scoring inputs between one and two columns for the width actually
    /// available. Cheap and idempotent — it returns immediately unless the column count itself
    /// changes, which is also what stops the re-layout it triggers from calling it again.</summary>
    private void LayoutScoringGrid(double available)
    {
        var columns = available >= TwoColumnMinWidth ? 2 : 1;
        if (columns == _scoringColumns) return;
        _scoringColumns = columns;

        ScoringGrid.ColumnDefinitions.Clear();
        ScoringGrid.RowDefinitions.Clear();
        for (var c = 0; c < columns; c++)
            ScoringGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                // Capped for the same reason as the hand-written grids in XAML: on a wide window
                // an uncapped star column gives a two-digit number a 400px-wide box.
                MaxWidth = 300,
            });
        for (var r = 0; r < (ScoringRules.All.Count + columns - 1) / columns; r++)
            ScoringGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < ScoringRules.All.Count; i++)
        {
            var box = _scoringBoxes[ScoringRules.All[i].Key];
            Grid.SetColumn(box, i % columns);
            Grid.SetRow(box, i / columns);
        }
    }

    private void ScoringGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        LayoutScoringGrid(e.NewSize.Width);

    // ── rules & timing (writes the shared config.json) ───────────────────

    private void LoadRules()
    {
        var cfg = ConfigService.Root;
        static string Words(System.Text.Json.JsonElement root, string section, string key)
        {
            if (!root.TryGetProperty(section, out var s) || !s.TryGetProperty(key, out var arr))
                return "";
            return string.Join("\n", arr.EnumerateArray()
                .Select(v => v.GetString() ?? "").Where(v => v.Length > 0));
        }
        static double NumD(System.Text.Json.JsonElement root, string key, double fallback) =>
            root.TryGetProperty(key, out var v) && v.TryGetDouble(out var n) ? n : fallback;

        // Working-hours/reminder/idle defaults now come from ConfigService's shared
        // methods rather than a second, independently-hardcoded copy of the same
        // fallbacks ActivityTracker also reads — previously these could silently
        // drift apart if one copy's default was ever changed without the other.
        WorkStart.Text = ConfigService.WorkStartTime().ToIsoTimeOfDay();
        WorkEnd.Text = ConfigService.WorkEndTime().ToIsoTimeOfDay();
        EodTimeBox.Text = cfg.TryGetProperty("end_of_day_summary_time", out var eod)
            ? eod.GetString() ?? "20:00" : "20:00";
        GraceMin.Value = ConfigService.ReminderGraceMinutes();
        RepeatMin.Value = ConfigService.ReminderIntervalMinutes();
        IdleMin.Value = ConfigService.IdleThresholdMinutes();
        RetentionDays.Value = ConfigService.DiaryRetentionDays();
        LateDayReminderHours.Value = NumD(cfg, "late_day_task_reminder_hours", 2.0);
        // Reads through ConfigService rather than off `cfg` directly, so a missing key shows
        // the same default the score formula would actually have used for it.
        foreach (var rule in ScoringRules.All)
            _scoringBoxes[rule.Key].Value = ConfigService.ScoringRate(rule.Key);
        RulesOn.Text = Words(cfg, "activity_rules", DiaryCategory.OnPlan);
        RulesOff.Text = Words(cfg, "activity_rules", DiaryCategory.OffPlan);
        RulesNeutral.Text = Words(cfg, "activity_rules", DiaryCategory.Neutral);
        IdleOn.Text = Words(cfg, "idle_activity_rules", DiaryCategory.OnPlan);
        IdleOff.Text = Words(cfg, "idle_activity_rules", DiaryCategory.OffPlan);
        IdleNeutral.Text = Words(cfg, "idle_activity_rules", DiaryCategory.Neutral);
    }

    // ── income (lost/gained-earnings counter, 2026-09-22 request) ────────

    private void LoadIncome()
    {
        MonthlyIncomeBox.Value = ConfigService.PotentialMonthlyIncomeEur();
        EmployedToggle.IsOn = ConfigService.IsEmployed();
        RenderPaidTags();
    }

    /// <summary>One row per payable tag: editable monthly rate, its hourly value, remove. Rebuilt
    /// after add/remove so the list always matches what config.json now holds.</summary>
    private void RenderPaidTags()
    {
        PaidTagsHost.Children.Clear();
        foreach (var tag in PaidTags.All())
        {
            var t = tag;
            // B1d: a payable tag never holds 0 or nothing — leaving the box that way puts the stored amount back.
            var rate = new MoneyBox { Header = $"{t.Label} — monthly net (€)", Width = 240, KeepsStoredValue = true, Accepts = v => v > 0 };
            rate.Value = t.MonthlyEur;
            WatchMoneyBox(rate, () => $"\"{t.Label}\" needs an amount above 0");
            rate.Committed += (_, _) =>
            {
                if (double.IsNaN(rate.Value) || rate.IsNegative || _initialising) return;
                SavePaidTags(PaidTags.All().Select(p => p.Value == t.Value ? p with { MonthlyEur = rate.Value } : p), rerender: false);
            };
            rate.Submitted += (_, _) => Park(rate);
            rate.Cancelled += (_, _) => Park(rate);   // B1h: Esc leaves the box
            var remove = new Button { Content = "Remove", VerticalAlignment = VerticalAlignment.Bottom };
            remove.Click += (_, _) =>
            {
                SavePaidTags(PaidTags.All().Where(p => p.Value != t.Value), rerender: true);
                // The clicked button has just left the tree; without this, focus falls to the first
                // input on the page — the salary box (B1c: the cursor must never land there unasked).
                Park(null);
            };
            PaidTagsHost.Children.Add(RateRow(rate, remove));
        }
        if (PaidTagsHost.Children.Count == 0)
            PaidTagsHost.Children.Add(new TextBlock { Text = "No payable tags.", FontSize = 12 });
    }

    /// <summary>One money row: the box, its "= €x,xx/h" readout (kept live, "= —/h" while empty)
    /// and an optional Remove button. Shared by the previous salary and every payable tag.</summary>
    private const string NegativeText = "Amounts can't be negative.";

    /// <summary>B1c: the "can't be negative" message for one money box — error colour, an error icon
    /// (so it doesn't rely on colour alone), semibold, and announced by screen readers when it
    /// appears. Visible exactly while the box's text is negative (<see cref="MoneyBox.IsNegative"/>),
    /// whether or not the box still has focus.</summary>
    private static StackPanel NegativeHint(MoneyBox box)
    {
        var (panel, show) = ErrorHint();
        box.NegativeChanged += (_, on) => show(on ? NegativeText : null);
        show(box.IsNegative ? NegativeText : null);
        return panel;
    }

    /// <summary>B1d: the red message line itself (error colour, error icon, semibold, announced by
    /// screen readers), with a setter: a text shows it, null hides it. Shared by the money rows'
    /// minus message and the new-tag line's refusal messages so both look identical.</summary>
    private static (StackPanel Panel, Action<string?> Show) ErrorHint()
    {
        var critical = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        var text = new TextBlock
        {
            FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            Foreground = critical, VerticalAlignment = VerticalAlignment.Center,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(text, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Assertive);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Visibility = Visibility.Collapsed };
        panel.Children.Add(new FontIcon { Glyph = "", FontSize = 14, Foreground = critical, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(text);
        void Show(string? message)
        {
            var was = panel.Visibility == Visibility.Visible ? text.Text : null;
            panel.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
            if (message is null || message == was) return;
            text.Text = message;
            Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(text)?
                .RaiseAutomationEvent(Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.LiveRegionChanged);
        }
        return (panel, Show);
    }

    /// <summary>B1d, for every money box on the Income section: a minus appearing wipes a stale
    /// "Saved." (nothing is being saved now), and a box that put its stored amount back says so.</summary>
    private void WatchMoneyBox(MoneyBox box, Func<string>? revertReason)
    {
        box.NegativeChanged += (_, on) => { if (on) SaveStatus.Text = ""; };
        if (revertReason is not null)
            box.Reverted += (_, _) => SaveStatus.Text = revertReason() + " — the saved amount was kept.";
    }

    // ── the new-tag line's message (B1c minus, B1d refusals) ─────────────

    private Action<string?> _showNewTagHint = _ => { };
    private string? _newTagError;
    private string? _refusedName;
    private double _refusedAmount = double.NaN;

    /// <summary>The minus message wins while the amount is negative; otherwise the last refusal.</summary>
    private void SyncNewTagHint() => _showNewTagHint(NewPaidTagRate.IsNegative ? NegativeText : _newTagError);

    private void ClearNewTagError()
    {
        if (_newTagError is null) return;
        _newTagError = null;
        _refusedName = null;
        SyncNewTagHint();
    }

    /// <summary>Shows why Add was refused, under the line, and removes any "Saved." left over from
    /// an earlier success (B1d: a refusal must not sit next to a success message).</summary>
    private void RefuseNewTag(string message)
    {
        _newTagError = message;
        _refusedName = (NewPaidTagName.Text ?? "").Trim();
        _refusedAmount = NewPaidTagRate.Value;
        SaveStatus.Text = "";
        SyncNewTagHint();
    }

    private static StackPanel RateRow(MoneyBox rate, Button? remove)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var hourly = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        void Refresh() => hourly.Text = double.IsNaN(rate.Value) || rate.IsNegative
            ? "= —/h" : $"= {MainWindow.FormatEur(rate.Value / IncomeService.WorkingHoursPerMonth)}/h";
        rate.ValueChanged += (_, _) => Refresh();
        rate.NegativeChanged += (_, _) => Refresh();   // B1d: deleting the minus alone must refresh too
        Refresh();
        row.Children.Add(rate);
        row.Children.Add(hourly);
        if (remove is not null) row.Children.Add(remove);
        var col = new StackPanel { Spacing = 4 };
        col.Children.Add(row);
        col.Children.Add(NegativeHint(rate));
        return col;
    }

    // ── leaving the "New payable tag" line (B1, 2026-10-23) ──────────────
    // Esc, or a click anywhere else, empties the line without adding a tag. Tab out of it does
    // not (keyboard users move through the page). Focus is read in LostFocus for controls that take
    // focus on click (a text box already has focus again by the time a page-level pointer handler
    // runs) and in Page_PointerPressed for clicks on things that don't (headings, blank space).

    private bool InNewPaidTagLine(DependencyObject? d)
    {
        for (; d is not null; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d))
            if (ReferenceEquals(d, NewPaidTagLine)) return true;
        return false;
    }

    private void NewPaidTagLine_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Tab) { _tabPending = true; return; }
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // B1c: Enter in the name or amount box = the Add button. On the Add button itself Enter
            // already clicks it, so it is left alone.
            if (e.OriginalSource is not TextBox box) return;
            e.Handled = true;
            if (TryAddPaidTag()) Park(box);
            return;
        }
        if (e.Key != Windows.System.VirtualKey.Escape) return;
        e.Handled = true;
        ClearNewPaidTagLine();
        SectionList.Focus(FocusState.Programmatic);
    }

    // ── "the cursor is nowhere" (B1b park, B1c Enter/blank-click/Tab rules) ──
    // WinUI has no "no focus" state inside a window, so "nowhere" is the 1 px see-through
    // IncomeFocusPark button: it has no caret, so typing reaches no box. It sits just before the
    // salary box in the tab order, so a plain Tab from it would put the cursor in the salary box
    // whatever box was left — the very jump the user reported. Tab/Shift+Tab on the park therefore
    // continue from the element that was parked from, as if the park had never happened.

    /// <summary>The element focus was parked from (null when it no longer exists, e.g. after Remove).</summary>
    private UIElement? _parkedFrom;

    private void Park(UIElement? from)
    {
        _parkedFrom = from;
        IncomeFocusPark.IsTabStop = true;   // a button that is not a tab stop refuses programmatic focus
        IncomeFocusPark.Focus(FocusState.Programmatic);
    }

    private void IncomeFocusPark_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Tab) return;
        var from = _parkedFrom;
        if (from is null || !IsInIncomePanel(from) || from.Visibility != Visibility.Visible) return;
        e.Handled = true;
        var shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                     & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        // A Tab out of the new-tag line must not clear it (same flag the line's own Tab sets).
        if (InNewPaidTagLine(from)) _tabPending = true;
        from.Focus(FocusState.Keyboard);
        Microsoft.UI.Xaml.Input.FocusManager.TryMoveFocus(
            shift ? Microsoft.UI.Xaml.Input.FocusNavigationDirection.Previous : Microsoft.UI.Xaml.Input.FocusNavigationDirection.Next,
            new Microsoft.UI.Xaml.Input.FindNextElementOptions { SearchRoot = XamlRoot.Content });
    }

    private bool IsInIncomePanel(DependencyObject? d)
    {
        for (; d is not null; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d))
            if (ReferenceEquals(d, IncomePanel)) return true;
        return false;
    }

    private void NewPaidTag_LostFocus(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var tab = _tabPending;
            _tabPending = false;
            if (tab) return;
            var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (focused is not null && !InNewPaidTagLine(focused)) ClearNewPaidTagLine();
        });
    }

    /// <summary>What the last press landed on, for the release-time check (a TextBox captures the
    /// pointer, so the release's own source is not where the click was).</summary>
    private DependencyObject? _pressSource;

    private void Page_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _tabPending = false;   // a click is never "the Tab that left the line"
        _focusIntent = null;
        var src = e.OriginalSource as DependencyObject;
        _pressSource = src;
        var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        var inLine = focused is not null && InNewPaidTagLine(focused);
        var blank = IsNonInteractive(src);
        // B1b/B1c: a click on a heading, caption or blank space takes the cursor out of a money box
        // (or the new-tag line) and leaves it nowhere. A click on another control lets that control
        // take focus as usual.
        if ((focused is MoneyBox || inLine) && blank) Park(focused as UIElement);
        // B1d: whatever the click did, the cursor must not end up in an Income box the click was not
        // on (WinUI may hand focus to the first focusable box when a container takes a click —
        // the salary box is that first box). Checked once the press, and again once the release,
        // has been fully processed.
        DispatcherQueue.TryEnqueue(() => GuardStrayFocus(src));
        if (!inLine || InNewPaidTagLine(src)) return;
        ClearNewPaidTagLine();
        if (blank) return;
        // Clicked a control outside the line: if it did not take focus itself, don't leave the
        // cursor in the just-emptied line.
        DispatcherQueue.TryEnqueue(() =>
        {
            var now = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (now is not null && InNewPaidTagLine(now)) Park(now as UIElement);
        });
    }

    private void Page_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var src = _pressSource;
        DispatcherQueue.TryEnqueue(() => GuardStrayFocus(src));
    }

    /// <summary>B1d: if, after a click, a text box in the Income section holds the cursor although
    /// the click did not land in that box, the cursor is moved to "nowhere" (the park). A click into
    /// a box, or on a control that takes focus itself (toggle, button), is left alone.</summary>
    private void GuardStrayFocus(DependencyObject? src)
    {
        if (XamlRoot is null || IncomePanel.Visibility != Visibility.Visible) return;
        if (Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) is not TextBox box) return;
        if (!IsInIncomePanel(box) || IsWithin(src, box) || ReferenceEquals(box, _focusIntent)) return;
        Park(box);
    }

    private static bool IsWithin(DependencyObject? d, DependencyObject container)
    {
        for (; d is not null; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d))
            if (ReferenceEquals(d, container)) return true;
        return false;
    }

    /// <summary>True when the press landed on something that is not an input control: a heading,
    /// caption, readout or blank space anywhere on this page. The content pane's ScrollViewer is
    /// the one control allowed in the chain (blank space inside it); its scroll bars are not.</summary>
    private bool IsNonInteractive(DependencyObject? d)
    {
        for (; d is not null; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d))
        {
            if (ReferenceEquals(d, this)) return true;
            if (ReferenceEquals(d, SectionScroll)) continue;
            if (d is Microsoft.UI.Xaml.Controls.Control) return false;
        }
        return false;
    }

    private void ClearNewPaidTagLine()
    {
        NewPaidTagName.Text = "";
        NewPaidTagRate.Value = double.NaN;   // B1d: empty, not "0" — 0 is not an amount a tag may have
        ClearNewTagError();
    }

    private void SavePaidTags(IEnumerable<PaidTag> tags, bool rerender)
    {
        try
        {
            PaidTags.Save(tags.ToList());
            SaveStatus.Text = "Saved.";
            if (rerender) RenderPaidTags();
            (App.MainWindow as MainWindow)?.RefreshIncome();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.SavePaidTags", ex);
            SaveStatus.Text = Log.Friendly("Couldn't save payable tags", ex);
        }
    }

    /// <summary>B1d: after a mouse (or keyboard) Add the cursor leaves the line — not into a box and
    /// not left on the button; Tab carries on from the button. On a refusal the cursor goes to the
    /// box that needs fixing.</summary>
    private void AddPaidTag_Click(object sender, RoutedEventArgs e)
    {
        if (TryAddPaidTag(out var fix)) { Park(NewPaidTagAdd); return; }
        _focusIntent = fix;   // put there on purpose: the stray-focus guard must leave it
        fix?.Focus(FocusState.Programmatic);
    }

    /// <summary>A box the page itself just focused in answer to a click (refused Add); exempt from
    /// <see cref="GuardStrayFocus"/> until the next press.</summary>
    private Control? _focusIntent;

    private bool TryAddPaidTag() => TryAddPaidTag(out _);

    /// <summary>The Add button's action, shared with Enter in the line (B1c). Returns true when a
    /// tag was added; on a refusal the line keeps its text, the reason shows in red under the line
    /// and <paramref name="fix"/> is the box to correct. B1d: an empty amount, an amount of 0 and a
    /// text that is no number (letters are stripped as typed, so it arrives empty) are refused — a
    /// payable tag at 0 is never saved.</summary>
    private bool TryAddPaidTag(out Control? fix)
    {
        fix = null;
        var label = (NewPaidTagName.Text ?? "").Trim();
        var slug = PaidTags.Slug(label);
        if (slug.Length == 0) { fix = NewPaidTagName; RefuseNewTag("Give the new tag a name (letters or digits)."); return false; }
        if (NewPaidTagRate.IsNegative) { fix = NewPaidTagRate; SaveStatus.Text = ""; return false; }   // its own message is already showing
        if (double.IsNaN(NewPaidTagRate.Value) || NewPaidTagRate.Value <= 0)
        {
            fix = NewPaidTagRate;
            RefuseNewTag("Monthly net needs an amount above 0.");
            return false;
        }
        var all = PaidTags.All().ToList();
        if (all.Any(p => p.Value == slug)) { fix = NewPaidTagName; RefuseNewTag($"\"{label}\" is already a payable tag."); return false; }
        // A name matching a built-in tag (e.g. Selfdev) simply makes that tag payable.
        var builtIn = Array.Find(DiaryTag.Options, o => o.Value == slug);
        all.Add(new PaidTag(slug, builtIn.Label ?? label, NewPaidTagRate.Value));
        SavePaidTags(all, rerender: true);
        ClearNewPaidTagLine();
        return true;
    }

    /// <summary>Independent of SaveRules — same reasoning as SaveHoursAndEod's own doc
    /// comment: a bad value anywhere else on this page must never block this pair, and vice
    /// versa. Past the NaN guard nothing here can fail validation: a negative amount (B1c: the
    /// box keeps a typed minus) is never committed, and a toggle flip meanwhile keeps the stored
    /// amount — so this is a single unconditional Mutate rather than a two-outcome
    /// validate-then-save.</summary>
    private void SaveIncome()
    {
        if (double.IsNaN(MonthlyIncomeBox.Value))
        {
            SaveStatus.Text = "Potential monthly income needs a number.";
            return;
        }
        // B1c: a negative amount is never saved; a toggle flip while the box shows one keeps the
        // amount that is already stored.
        var amount = MonthlyIncomeBox.IsNegative ? ConfigService.PotentialMonthlyIncomeEur() : MonthlyIncomeBox.Value;
        try
        {
            ConfigService.Mutate(cfg =>
            {
                cfg["income"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["monthly_net_eur"] = amount,
                    ["employed"] = EmployedToggle.IsOn,
                };
            });
            SaveStatus.Text = "Saved.";
            RefreshSummaries();
            // Historical ledger rows are never rewritten (settled decision: a toggle flip
            // only changes the sign of days posted from now on) — this just lets the sidebar
            // chip's label/today-preview-driven Reports figures reflect the new toggle/rate
            // immediately rather than waiting for the next catch-up run.
            (App.MainWindow as MainWindow)?.RefreshIncome();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.SaveIncome", ex);
            SaveStatus.Text = Log.Friendly("Couldn't save income settings", ex);
        }
    }

    private void Income_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        SaveIncome();
    }

    /// <summary>Fires on LostFocus for every hours/reminders/keyword TextBox and
    /// ValueChanged for every NumberBox in that section — the whole group used to only
    /// save via an explicit "Save settings" button, inconsistent with Theme/Opacity/
    /// "start with Windows" above it (which already autosave), and with nothing warning
    /// if you navigated away with unsaved edits (audit finding #10). Saving the whole
    /// group together on any one field's change mirrors exactly what the old button did
    /// in one Mutate call — this just removes the extra click.</summary>
    private void Rules_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        SaveRules();
    }

    private void Rules_ValueChanged(NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
    {
        if (_initialising) return;
        SaveRules();
    }

    /// <summary>Two independent save phases, not one all-or-nothing write. Working
    /// hours/EOD used to be validated and saved in the same single Mutate() call as every
    /// reminder/scoring/keyword field on this page — so a NaN scoring NumberBox (e.g. one
    /// mid-edit, momentarily empty while the user is retyping it, the instant a LostFocus
    /// on some other field fired this same handler) silently discarded an otherwise-valid
    /// Work Start/End edit: the textbox kept showing the new time, but nothing was ever
    /// written to config.json, so the tracker, Kickoff, Today — everything that reads
    /// working hours — kept using the old value with no visible sign why (2026-08-17 user
    /// report: "the day of the app starts at 8 though in the settings it is now set to 6";
    /// confirmed live by reading config.json — it still held the old start time). Splitting
    /// hours/EOD into their own save, attempted first and independent of everything else on
    /// the page, means a bad field anywhere else here can never block that save again.</summary>
    private void SaveRules()
    {
        if (!SaveHoursAndEod(out var hoursError))
        {
            SaveStatus.Text = hoursError;
            return;
        }

        var rulesOk = SaveRemainingRules(out var rulesError);
        // The tracker reads config once at construction — restart it so hours/keyword/
        // threshold changes apply now, not at the next app start. Unconditional: hours/EOD
        // above already succeeded even if the rest of the page didn't.
        (App.MainWindow as MainWindow)?.RestartTracker();
        if (rulesOk)
        {
            SaveStatus.Text = "Saved — tracker restarted with the new rules.";
            // Only on the full-success path: a header must never advertise a value that a
            // validation failure below stopped from being written.
            RefreshSummaries();
            RefreshTrackerInfo();
        }
        else
        {
            // Hours/EOD are already safely on disk at this point — say so, so a genuine
            // partial save never reads as a total failure.
            SaveStatus.Text = $"Working hours saved. {rulesError}";
        }
    }

    /// <returns>true once working hours + EOD are validated and written (or unconditionally
    /// once the write itself has been attempted); false only when nothing was written, with
    /// <paramref name="error"/> explaining what to fix.</returns>
    private bool SaveHoursAndEod(out string error)
    {
        foreach (var (box, label) in new[]
                 { (WorkStart, "Work start"), (WorkEnd, "Work end"), (EodTimeBox, "Day review at") })
        {
            if (!DateExtensions.TryParseTimeOfDay(box.Text.Trim(), out _))
            {
                error = $"{label} must be HH:MM (e.g. 08:00).";
                return false;
            }
        }

        // Work start must precede work end. This was only worth enforcing once the diary window
        // became these same hours (2026-08-04): inverted, it isn't a short working day, it's no
        // tracking at all — nothing logged, all day, with the app otherwise looking healthy.
        DateExtensions.TryParseTimeOfDay(WorkStart.Text.Trim(), out var workStart);
        DateExtensions.TryParseTimeOfDay(WorkEnd.Text.Trim(), out var workEnd);
        if (workStart >= workEnd)
        {
            error = "\"Work start\" must be earlier than \"Work end\" — " +
                    "otherwise nothing gets tracked at all.";
            return false;
        }

        try
        {
            ConfigService.Mutate(cfg =>
            {
                cfg["working_hours"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["start"] = WorkStart.Text.Trim(),
                    ["end"] = WorkEnd.Text.Trim(),
                };
                // No "diary_hours" written: the diary window is working_hours (2026-08-04).
                // A stray block from the few hours that setting existed is removed rather than
                // left behind, so nobody later finds it in config.json and assumes it's live.
                cfg.Remove("diary_hours");
                cfg["end_of_day_summary_time"] = EodTimeBox.Text.Trim();
            });
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.SaveHoursAndEod", ex);
            error = Log.Friendly("Couldn't save working hours", ex);
            return false;
        }
    }

    /// <returns>true once every reminder/idle/retention/scoring/keyword field is validated
    /// and written; false if nothing in this phase was written, with <paramref name="error"/>
    /// explaining what to fix. Never touches working_hours/end_of_day_summary_time — those
    /// are SaveHoursAndEod's responsibility, already committed by the time this runs.</returns>
    private bool SaveRemainingRules(out string error)
    {
        // A NumberBox emptied by the user reports Value = NaN, and (int)NaN is a garbage number
        // (int.MinValue), not a zero — writing that into the score formula (or a reminder/idle/
        // retention setting — these went unchecked before this split, same silent-garbage risk,
        // just never yet reported) would be silent and spectacular. Bail out the same way an
        // unparseable time does, rather than saving any of it.
        foreach (var rule in ScoringRules.All)
        {
            if (double.IsNaN(_scoringBoxes[rule.Key].Value))
            {
                error = $"\"{rule.Label}\" needs a number (default: {rule.Default}) — the rest of this page wasn't saved.";
                return false;
            }
        }
        foreach (var (box, label) in new (NumberBox Box, string Label)[]
                 { (GraceMin, "Reminder grace"), (RepeatMin, "Reminder repeat"), (IdleMin, "Idle threshold"),
                   (RetentionDays, "Diary retention"), (LateDayReminderHours, "Late-day reminder") })
        {
            if (double.IsNaN(box.Value))
            {
                error = $"\"{label}\" needs a number — the rest of this page wasn't saved.";
                return false;
            }
        }

        static System.Text.Json.Nodes.JsonArray Lines(TextBox box) =>
            new(box.Text.Split('\n', '\r')
                .Select(l => l.Trim()).Where(l => l.Length > 0)
                .Select(l => (System.Text.Json.Nodes.JsonNode)l).ToArray());

        try
        {
            ConfigService.Mutate(cfg =>
            {
                cfg["reminder_grace_minutes"] = (int)GraceMin.Value;
                cfg["reminder_interval_minutes"] = (int)RepeatMin.Value;
                cfg["idle_threshold_minutes"] = (int)IdleMin.Value;
                // User-configurable retention (2026-07-09 audit finding
                // #34) — Database.DiaryRetentionDays remains the fallback
                // default, read via ConfigService.DiaryRetentionDays().
                cfg["diary_retention_days"] = (int)RetentionDays.Value;
                cfg["late_day_task_reminder_hours"] = LateDayReminderHours.Value;
                // Merged into whatever "scoring" already holds, rather than replaced wholesale
                // like the keyword blocks below — those are fully represented on this page, this
                // one might not be if a future rule lands in config.json before it lands here.
                if (cfg["scoring"] is not System.Text.Json.Nodes.JsonObject scoring)
                    cfg["scoring"] = scoring = new System.Text.Json.Nodes.JsonObject();
                foreach (var rule in ScoringRules.All)
                    scoring[rule.Key] = (int)_scoringBoxes[rule.Key].Value;
                cfg["activity_rules"] = new System.Text.Json.Nodes.JsonObject
                {
                    [DiaryCategory.OnPlan] = Lines(RulesOn),
                    [DiaryCategory.OffPlan] = Lines(RulesOff),
                    [DiaryCategory.Neutral] = Lines(RulesNeutral),
                };
                cfg["idle_activity_rules"] = new System.Text.Json.Nodes.JsonObject
                {
                    [DiaryCategory.OnPlan] = Lines(IdleOn),
                    [DiaryCategory.OffPlan] = Lines(IdleOff),
                    [DiaryCategory.Neutral] = Lines(IdleNeutral),
                };
            });
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.SaveRemainingRules", ex);
            error = Log.Friendly("Couldn't save your settings", ex);
            return false;
        }
    }

    private void RefreshTickTickStatus()
    {
        TickTickStatus.Text = TickTickService.IsAuthorized
            ? "Connected — personal tasks due today appear on the Today page."
            : "Not connected.";
        TickTickConnectBtn.Content = TickTickService.IsAuthorized
            ? "Reconnect TickTick…" : "Connect TickTick…";
        // Only worth offering once there's something to disconnect — before the first
        // connect, or after a Disconnect/Clear-all-data already removed the tokens,
        // there's nothing left for it to do (2026-07-18 audit finding R10-02).
        TickTickDisconnectBtn.Visibility = TickTickService.IsAuthorized
            ? Visibility.Visible : Visibility.Collapsed;
        SumTickTick.Text = TickTickService.IsAuthorized ? "Connected" : "Not connected";
    }

    private void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        StartupService.SetEnabled(StartupToggle.IsOn);
        RefreshSummaries();
    }

    /// <summary>Autosaves like Theme/Opacity/"start with Windows" above it — previously
    /// this could only be set once, at first run, via NameSetupDialog, with no way to see
    /// or change it afterward short of opening config.json directly (2026-07-18 audit
    /// finding R10-13). An empty name is valid (mentor-voice copy already falls back to a
    /// neutral greeting) — nothing here forces a value.</summary>
    private void YourName_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        try
        {
            ConfigService.Mutate(cfg => cfg["user_name"] = YourName.Text.Trim());
            // Every other autosaving field on this page confirms on success — this one
            // didn't, so typing a name and tabbing away gave no sign it actually saved
            // (2026-07-18 audit finding R11-09).
            SaveStatus.Text = "Saved.";
            RefreshSummaries();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.YourName", ex);
            SaveStatus.Text = Log.Friendly("Couldn't save your name", ex);
        }
    }

    private void Opacity_Changed(object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_initialising) return;
        var value = (int)e.NewValue;
        (App.MainWindow as MainWindow)?.ApplyOpacity(value);

        var state = StateService.Load();
        state.Opacity = value;
        StateService.Save(state);
        RefreshSummaries();
    }

    private void RefreshAiStatus()
    {
        var has = AiSettings.HasKey;
        AiStatus.Text = has ? "A key is saved on this PC." : "No key saved — Ask Claude is off.";
        SumAi.Text = has ? "Key saved · " + AiSettings.Model : "No key — manual copy/paste";
        AiModelBox.Text = AiSettings.Model;
        AiRemoveBtn.IsEnabled = has;
        AiTestBtn.IsEnabled = has;
        AiKeyBox.PlaceholderText = has ? "Saved — type a new key to replace it" : "sk-ant-…";
    }

    private void AiSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AiSettings.SaveModel(AiModelBox.Text);
            var key = AiKeyBox.Password.Trim();
            if (key.Length > 0)
            {
                if (!AiSettings.SaveKey(key)) { AiResult.Text = "Windows refused to store the key. Nothing was saved."; return; }
                AiKeyBox.Password = "";
            }
            AiResult.Text = "Saved.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage (AI save)", ex);
            AiResult.Text = "Couldn't save. Nothing was changed.";
        }
        RefreshAiStatus();
    }

    private CancellationTokenSource? _aiTestCts;

    private async void AiTest_Click(object sender, RoutedEventArgs e)
    {
        AiTestBtn.IsEnabled = false;
        AiStopBtn.Visibility = Visibility.Visible;
        AiResult.Text = "Testing… (gives up after 30 seconds)";
        _aiTestCts = new CancellationTokenSource();
        try
        {
            await AiSettings.CreateClient().TestAsync(_aiTestCts.Token);
            AiResult.Text = "Connected ✓";
        }
        catch (AiException ex) when (ex.Kind == AiFailure.Cancelled) { AiResult.Text = "Stopped."; }
        catch (AiException ex) { AiResult.Text = ex.Message; }
        catch (Exception ex)
        {
            Log.Error("SettingsPage (AI test)", ex);
            AiResult.Text = "Something went wrong talking to the AI service.";
        }
        finally
        {
            _aiTestCts.Dispose();
            _aiTestCts = null;
            AiStopBtn.Visibility = Visibility.Collapsed;
            RefreshAiStatus();
        }
    }

    private void AiStop_Click(object sender, RoutedEventArgs e) => _aiTestCts?.Cancel();

    private async void AiRemove_Click(object sender, RoutedEventArgs e)
    {
        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Remove the API key?",
            "Deletes the saved key from this PC. Ask Claude turns off until you add one again; " +
            "the manual copy/paste screens keep working.",
            primaryButtonText: "Remove", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;
        try { AiSettings.RemoveKey(); AiResult.Text = "Key removed."; }
        catch (Exception ex) { Log.Error("SettingsPage (AI remove)", ex); AiResult.Text = "Couldn't remove the key."; }
        RefreshAiStatus();
    }

    private async void TickTickConnect_Click(object sender, RoutedEventArgs e)
    {
        await Dialogs.TickTickConnectDialog.ShowAsync(XamlRoot);
        RefreshTickTickStatus();
    }

    /// <summary>Removes the stored client secret and tokens, and the saved client ID —
    /// previously the only way to fully undo a "Connect TickTick" was to open Windows
    /// Credential Manager by hand and find the right entries (2026-07-18 audit finding
    /// R10-02).</summary>
    private async void TickTickDisconnect_Click(object sender, RoutedEventArgs e)
    {
        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Disconnect TickTick?",
            "Removes the saved client ID, secret, and access tokens from this " +
            "PC. Your TickTick account and tasks themselves aren't affected — " +
            "you can reconnect any time.",
            primaryButtonText: "Disconnect", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        // Unlike every other mutating button on this page, this one had no try/catch —
        // a failed write partway through (Disconnect deletes 3 credentials, then rewrites
        // config.json) could silently leave the screen still saying "Connected" with no
        // error shown (2026-07-18 audit finding R11-01, found independently by two audit
        // passes). RefreshTickTickStatus in `finally` keeps the screen honest either way.
        try
        {
            TickTickAuth.Disconnect();
            SaveStatus.Text = "TickTick disconnected.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.TickTickDisconnect", ex);
            SaveStatus.Text = Log.Friendly("Couldn't fully disconnect TickTick", ex);
        }
        finally
        {
            RefreshTickTickStatus();
        }
    }

    private async void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        // Disabled + "Exporting…" status while it runs, matching the Clear buttons' own
        // RunClearActionAsync pattern — without this, nothing visibly happens when clicked,
        // which invites a double-click into two concurrent exports writing the same file
        // (2026-07-24 audit finding #6).
        ExportAllBtn.IsEnabled = false;
        SaveStatus.Text = "Exporting…";
        try
        {
            // Off the UI thread — this opens the DB and reads every user table, which will
            // only grow with time and used to run directly inside the button click handler
            // (audit finding #12).
            var path = await Task.Run(() => DataExport.ExportAll());
            SaveStatus.Text = "Exported to " + path;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ExportAll", ex);
            SaveStatus.Text = Log.Friendly("Couldn't export your data", ex);
        }
        finally
        {
            ExportAllBtn.IsEnabled = true;
        }
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        int diaryRows, rollupDays;
        try
        {
            using var db = new Database();
            (diaryRows, rollupDays) = db.CountActivityHistory();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearHistory.Count", ex);
            SaveStatus.Text = Log.Friendly("Couldn't read your activity history", ex);
            return;
        }
        if (diaryRows == 0 && rollupDays == 0)
        {
            SaveStatus.Text = "No activity history to clear.";
            return;
        }

        // This only clears the database — any report/data export the user
        // already saved to disk (data/report.html, report.csv,
        // full-export.json) is a second, untouched copy of some of the same
        // information. Rather than silently delete files the user may have
        // deliberately kept, name the ones that currently exist and offer
        // an opt-in checkbox to remove them in the same action instead of
        // just warning and leaving it to a manual trip to File Explorer.
        var exportNames = ExportFiles.All
            .Where(f => File.Exists(Path.Combine(AppPaths.Root, "data", f)))
            .ToList();

        var confirmPanel = new StackPanel { Spacing = 10 };
        confirmPanel.Children.Add(new TextBlock
        {
            Text = $"Deletes {diaryRows} diary session(s) and {rollupDays} day(s) of " +
                   "rolled-up totals — the tracked window-activity record. Your plans, " +
                   "task completions, notes, and score are not affected. The change history and the " +
                   "automatic backups saved before bulk changes are deleted too. This cannot be undone.",
            TextWrapping = TextWrapping.Wrap,
        });
        var deleteExportsBox = new CheckBox
        {
            Content = $"Also delete {string.Join(", ", exportNames)} from your data folder",
            Visibility = Visibility.Collapsed,
        };
        if (exportNames.Count > 0)
        {
            confirmPanel.Children.Add(new TextBlock
            {
                Text = $"{string.Join(", ", exportNames)} in your data folder still holds a copy " +
                       "of some of this.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            deleteExportsBox.Visibility = Visibility.Visible;
            confirmPanel.Children.Add(deleteExportsBox);
        }

        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Clear activity history?", confirmPanel,
            primaryButtonText: "Clear history", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;
        var deleteExports = deleteExportsBox.IsChecked == true;

        // VACUUM rewrites the whole database file, not just the deleted
        // rows, so its cost scales with total file size — on months of
        // history this can take a perceptible moment. Running it inline on
        // the click handler used to freeze the window for that whole time
        // (2026-07-09 audit finding #8); moved off the UI thread, with the
        // button disabled and a status message so a slow clear still reads
        // as "working," not "stuck."
        await RunClearActionAsync(ClearHistoryBtn, () =>
        {
            using var db = new Database();
            db.ClearActivityHistory();
            // G3: the automatic backups taken before bulk changes are full copies of the
            // database — leaving them would quietly keep what this button promises to remove.
            var failed = DataChangeDoor.DeleteBackups();
            if (deleteExports) failed.AddRange(DeleteExportFiles(exportNames));
            ThrowIfExportFilesRemain(failed);
        }, "Activity history cleared.", "your activity history", "SettingsPage.ClearHistory");
    }

    /// <summary>Deletes each named file from the data folder, best-effort — logs and
    /// continues past a locked file instead of leaving the rest untried. Returns the
    /// names that could NOT be removed, so the caller can decide whether to report that
    /// (2026-07-18 audit finding, found during round-8's own re-audit: both "clear" actions
    /// used to swallow a delete failure into the log only, so a file left open elsewhere —
    /// report.csv in Excel, say — silently survived a "cleared" action with no error shown).</summary>
    private static List<string> DeleteExportFiles(IEnumerable<string> names)
    {
        var failed = new List<string>();
        foreach (var f in names)
        {
            try { File.Delete(Path.Combine(AppPaths.Root, "data", f)); }
            catch (Exception ex)
            {
                Log.Error($"SettingsPage.DeleteExportFile({f})", ex);
                failed.Add(f);
            }
        }
        return failed;
    }

    /// <summary>Distinguishes "the clear itself failed" from "the clear succeeded, but a
    /// leftover export file couldn't be removed" — RunClearActionAsync's catch checks for
    /// this type specifically so the two cases get different headlines instead of both
    /// being flattened into a generic "Couldn't clear X" that would contradict this
    /// exception's own already-correct message (2026-07-18 audit finding R10-01: wrapping
    /// this in Log.Friendly's "Couldn't clear..." prefix produced a message that said the
    /// clear failed in its first sentence and succeeded in its second).</summary>
    private sealed class ExportCleanupException(string message) : Exception(message);

    /// <summary>Throws so a partial file-delete failure surfaces through
    /// RunClearActionAsync's existing error path instead of being silently absorbed —
    /// the database-level clear this runs alongside already succeeded by this point, so
    /// the message is explicit that it's the leftover file(s), not the data itself.</summary>
    private static void ThrowIfExportFilesRemain(List<string> failed)
    {
        if (failed.Count > 0)
            throw new ExportCleanupException(
                $"Your data was cleared, but {string.Join(", ", failed)} couldn't be removed — close it elsewhere and try again.");
    }

    /// <summary>Shared busy-state sequence for the two "clear my data"
    /// buttons below — confirm dialog already shown by the caller; this
    /// covers disable-button → "Clearing…" → run off the UI thread →
    /// status message → re-enable, so the two buttons can't drift apart on
    /// this shared shape while still owning their own confirm copy/dbAction.
    /// <paramref name="whatFailed"/> keeps the failure message specific
    /// ("Couldn't clear reflections" vs "Couldn't clear activity history")
    /// instead of a single generic one that reads the same for both.</summary>
    private async Task RunClearActionAsync(Button trigger, Action dbAction, string successMessage,
        string whatFailed, string logTag)
    {
        trigger.IsEnabled = false;
        SaveStatus.Text = "Clearing…";
        try
        {
            await Task.Run(dbAction);
            SaveStatus.Text = successMessage;
        }
        catch (ExportCleanupException ex)
        {
            // Already a complete, correct, human-readable message — the clear itself
            // succeeded, only a leftover export file didn't. Log.Friendly's "Couldn't
            // clear..." prefix would contradict it (R10-01), so show it as-is.
            Log.Warn(logTag, ex.Message);
            SaveStatus.Text = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error(logTag, ex);
            SaveStatus.Text = Log.Friendly($"Couldn't clear {whatFailed}", ex);
        }
        finally
        {
            trigger.IsEnabled = true;
        }
    }

    /// <summary>
    /// Reflections (the evening review's one-line answers) previously had
    /// no delete path anywhere in the app — a deliberately separate action
    /// from "Clear activity history" above, since reflections are the
    /// user's own reflective text, not tracked window-activity data
    /// (2026-07-09 audit finding #12).
    /// </summary>
    private async void ClearReflections_Click(object sender, RoutedEventArgs e)
    {
        int count;
        try
        {
            using var db = new Database();
            using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
            count = score.CountReflections();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearReflections.Count", ex);
            SaveStatus.Text = Log.Friendly("Couldn't read your reflections", ex);
            return;
        }
        if (count == 0)
        {
            SaveStatus.Text = "No reflections to clear.";
            return;
        }

        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Clear my reflections?",
            $"Deletes {count} evening-review reflection(s). Your plans, task " +
            "completions, activity diary, and score are not affected. The automatic backups saved " +
            "before bulk changes are deleted too, since they hold a copy. This cannot be undone.",
            primaryButtonText: "Clear reflections", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        await RunClearActionAsync(ClearReflectionsBtn, () =>
        {
            using var db = new Database();
            using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
            score.ClearReflections();
            // G3: the automatic backups hold the reflections too (see ClearHistory_Click).
            ThrowIfExportFilesRemain(DataChangeDoor.DeleteBackups());
        }, "Reflections cleared.", "your reflections", "SettingsPage.ClearReflections");
    }

    /// <summary>
    /// Wipes every remaining data table this app keeps (task completions, reschedules/
    /// day-offs, task notes, score history, reflections, TickTick sync links, and the
    /// activity diary) plus the debug log, in one action — before this, only activity
    /// history and reflections had any delete path at all (audit finding #6); the debug
    /// log had none either (audit finding #25). Plan definitions themselves
    /// (plans/active/*.json) are never touched here — archiving/deleting a plan is its
    /// own separate action on the Plans page. Also deletes any report.html/report.csv/
    /// full-export.json sitting in the data folder — unconditionally, unlike the smaller
    /// "Clear activity history" button's opt-in checkbox, since this action is already the
    /// most destructive one in the app and its own confirmation already says "this cannot
    /// be undone" (2026-07-18 audit finding R8-05: this used to only clear the database,
    /// leaving those export files as an untouched second copy of the same data).
    /// </summary>
    private async void ClearAllData_Click(object sender, RoutedEventArgs e)
    {
        int rowCount;
        try
        {
            using var db = new Database();
            rowCount = db.CountAllData();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearAllData.Count", ex);
            SaveStatus.Text = Log.Friendly("Couldn't read your data", ex);
            return;
        }
        if (rowCount == 0)
        {
            SaveStatus.Text = "No data to clear.";
            return;
        }

        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Clear all my data?",
            $"Deletes {rowCount} row(s) across every data table this app keeps — " +
            "task completions, reschedules and day-offs, task notes, score history, " +
            "reflections, TickTick sync links, and the activity diary — plus the debug " +
            "log, your saved TickTick connection (client ID, secret, and tokens), and " +
            "any report.html/report.csv/full-export.json you've exported to the data " +
            "folder, the change history and the automatic backups saved before bulk changes. Your plan definitions and the other settings on this page " +
            "(including your name) are not touched — use the Plans page to archive " +
            "or remove a plan. This cannot be undone.",
            primaryButtonText: "Clear everything", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        await RunClearActionAsync(ClearAllDataBtn, () =>
        {
            using var db = new Database();
            db.ClearAllData();
            Log.Clear();
            // Previously only the ticktick_sync database table (plan-task ↔ TickTick-task
            // ID links) was cleared here — the actual credentials in Windows Credential
            // Manager survived every "clear all my data" run with no way to remove them
            // short of opening Credential Manager by hand (2026-07-18 audit finding R10-02).
            TickTickAuth.Disconnect();
            var failed = DataChangeDoor.DeleteBackups();   // G3: see ClearHistory_Click
            failed.AddRange(DeleteExportFiles(ExportFiles.All));
            ThrowIfExportFilesRemain(failed);
        }, "All data cleared.", "your data", "SettingsPage.ClearAllData");
        RefreshTickTickStatus();
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initialising) return;
        var tag = (ThemeChoice.SelectedItem as RadioButton)?.Tag as string ?? "default";

        var state = StateService.Load();
        state.Theme = tag;
        StateService.Save(state);

        if ((App.MainWindow as MainWindow)?.Content is FrameworkElement root)
        {
            root.RequestedTheme = tag switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            // MainWindow's ActualThemeChanged subscription re-applies
            // ThemeSync automatically, but calling it directly here too
            // means the C#-built UI already on screen (this page included)
            // updates immediately rather than waiting for that event to
            // propagate through the dispatcher.
            ThemeSync.Apply(root.ActualTheme);
        }
        RefreshSummaries();
    }
}

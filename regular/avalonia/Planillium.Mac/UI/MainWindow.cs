using System.Globalization;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Planillium.App.Dialogs;
using Planillium.App.Pages;
using Planillium.App.Services;

namespace Planillium.App;

/// <summary>Posts work to the UI thread (stand-in for WinUI's DispatcherQueue, so the shared watcher code is unchanged).</summary>
internal sealed class UiQueue
{
    public bool TryEnqueue(Action a) { Dispatcher.UIThread.Post(a); return true; }
}

// Split like the Windows window: this file = lifecycle + sidebar; .Startup = watchers/catch-up;
// .Tracker = activity tracker wiring; .Tray = menu-bar icon.
public sealed partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }
    public ActivityTracker? Tracker { get; private set; }
    private readonly UiQueue _dq = new();

    private readonly ContentControl ContentFrame = new();
    private readonly StackPanel NavList = new() { Spacing = 2 };
    private readonly Ellipse TodayDot = new() { Width = 8, Height = 8, Fill = Ui.Brush("SystemFillColorCriticalBrush"), IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse ActivityDot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center, Fill = Ui.Brush("TextFillColorTertiaryBrush") };
    private readonly TextBlock ActivityText = new() { Text = "Starting…", FontSize = 12, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock ActivityWindow = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 2, Foreground = Ui.Brush("TextFillColorTertiaryBrush") };
    private readonly TextBlock ScoreValue = new() { Text = "—", FontSize = 18, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock IncomeLabel = new() { Text = "FINAL BALANCE", FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Ui.Brush("TextFillColorTertiaryBrush") };
    private readonly TextBlock IncomeValue = new() { Text = "—", FontSize = 18, FontWeight = FontWeight.SemiBold };
    private readonly StackPanel PlanDriftPanel = new() { Spacing = 8 };
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, PageBase> _pages = new();
    private string _currentTag = "";
    private bool _reallyClose;

    private static readonly (string Tag, string Label)[] NavItems =
        { ("today", "Today"), ("schedule", "Schedule"), ("reports", "Reports"), ("plans", "Plans") };

    public MainWindow()
    {
        Instance = this;
        Title = AppInfo.DisplayName;
        MinWidth = 900; MinHeight = 600;
        var st = StateService.Load();
        Width = Math.Max(900, st.WindowWidth); Height = Math.Max(600, st.WindowHeight);
        Opacity = Math.Clamp(st.Opacity, 40, 100) / 100.0;
        ApplyTheme(st.Theme);
        Content = BuildShell();

        var page = Environment.GetEnvironmentVariable("MENTOR_PAGE") ?? "today";
        Navigate(page);
        RefreshScore();
        RunStartupCatchUp();
        StartEodWatcher();
        StartKickoffWatcher();
        StartLateDayTaskReminderWatcher();
        StartDiaryPruneWatcher();
        StartDayChangeWatcher();
        InitTray();

        Opened += (_, _) => _ = InitTrackingAsync();
        Activated += (_, _) => CheckPendingNotifications();
        Closing += (_, e) =>
        {
            SaveWindowSize();
            if (_reallyClose) return;
            e.Cancel = true;
            Hide();
        };
        Closed += (_, _) =>
        {
            Tracker?.Stop();
            DisposeTray();
            _eodTimer?.Dispose(); _kickoffTimer?.Dispose(); _lateDayReminderTimer?.Dispose();
            _diaryPruneTimer?.Dispose(); _dayChangeTimer?.Dispose();
        };
    }

    public void ApplyTheme(string? theme)
    {
        RequestedThemeVariant = theme switch
        {
            "light" => Avalonia.Styling.ThemeVariant.Light,
            "dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
        if (Application.Current != null) Application.Current.RequestedThemeVariant = RequestedThemeVariant;
        var dark = theme == "dark" || (theme != "light" && (Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant ?? Avalonia.Platform.PlatformThemeVariant.Dark) == Avalonia.Platform.PlatformThemeVariant.Dark);
        ThemeSync.Apply(dark);
        if (Content != null) Background = Ui.Brush("AppGroundBrush");
    }

    private Control BuildShell()
    {
        Background = Ui.Brush("AppGroundBrush");
        var side = new DockPanel { LastChildFill = false, Margin = new Thickness(12, 12, 12, 12) };
        var brand = new TextBlock { Text = AppInfo.DisplayName, FontSize = 18, FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 4, 0, 14) };
        DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);
        foreach (var (tag, label) in NavItems)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            if (tag == "today") { Grid.SetColumn(TodayDot, 1); row.Children.Add(TodayDot); }
            var b = new Button { Content = row, Tag = tag, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 9), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => Navigate(tag);
            _navButtons[tag] = b;
            NavList.Children.Add(b);
        }
        DockPanel.SetDock(NavList, Dock.Top); side.Children.Add(NavList);

        var footer = new StackPanel { Spacing = 8 };
        var pill = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5), HorizontalAlignment = HorizontalAlignment.Left, Background = Ui.Brush("SubtleFillColorSecondaryBrush") };
        var pillRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pillRow.Children.Add(ActivityDot); pillRow.Children.Add(ActivityText); pill.Child = pillRow;
        footer.Children.Add(pill); footer.Children.Add(ActivityWindow);
        footer.Children.Add(Chip("BALANCE", ScoreValue, "pts"));
        footer.Children.Add(Chip(null, IncomeValue, null, IncomeLabel));
        footer.Children.Add(PlanDriftPanel);
        var buy = new Button { Content = "Buy entertainment time", FontSize = 12, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4, 2), Foreground = Ui.Brush("AccentTextFillColorPrimaryBrush") };
        buy.Click += async (_, _) => await SpendDialog.ShowBuyAsync(this);
        var log = new Button { Content = "Log spend (no regrets)", FontSize = 12, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4, 2), Foreground = Ui.Brush("AccentTextFillColorPrimaryBrush") };
        log.Click += async (_, _) => await SpendDialog.ShowLogSpendAsync(this);
        var links = new StackPanel { Margin = new Thickness(2, 0, 0, 0) };
        links.Children.Add(buy); links.Children.Add(log);
        footer.Children.Add(links);
        var settings = new Button { Content = "Settings", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 9), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Tag = "settings" };
        settings.Click += (_, _) => Navigate("settings");
        _navButtons["settings"] = settings;
        footer.Children.Add(settings);
        DockPanel.SetDock(footer, Dock.Bottom); side.Children.Add(footer);

        var sideBorder = new Border { Width = 248, Child = new ScrollViewer { Content = side }, Background = Ui.Brush("SubtleFillColorSecondaryBrush") };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(sideBorder);
        Grid.SetColumn(ContentFrame, 1); grid.Children.Add(ContentFrame);
        return grid;
    }

    private static Border Chip(string? label, TextBlock value, string? unit, TextBlock? labelBlock = null)
    {
        var lb = labelBlock ?? new TextBlock { Text = label, FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Ui.Brush("TextFillColorTertiaryBrush") };
        var col = new StackPanel { Spacing = 2 };
        col.Children.Add(lb);
        if (unit == null) col.Children.Add(value);
        else
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(value);
            row.Children.Add(new TextBlock { Text = unit, FontSize = 12, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 2), Foreground = Ui.Brush("TextFillColorTertiaryBrush") });
            col.Children.Add(row);
        }
        return new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10), Background = Ui.Brush("CardBackgroundFillColorDefaultBrush"), BorderBrush = Ui.Brush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1), Child = col };
    }

    private PageBase MakePage(string tag) => tag switch
    {
        "today" => new TodayPage(),
        "schedule" => new SchedulePage(),
        "reports" => new ReportsPage(),
        "plans" => new PlansPage(),
        "settings" => new SettingsPage(),
        _ => new TodayPage(),
    };

    public void Navigate(string tag)
    {
        if (!_navButtons.ContainsKey(tag)) tag = "today";
        _currentTag = tag;
        foreach (var (t, b) in _navButtons)
            b.Background = t == tag ? Ui.Brush("CardBackgroundFillColorDefaultBrush") : Brushes.Transparent;
        if (!_pages.TryGetValue(tag, out var page)) _pages[tag] = page = MakePage(tag);
        (ContentFrame.Content as PageBase)?.NavigatedFrom();
        ContentFrame.Content = page;
        page.NavigatedTo();
    }

    private void SaveWindowSize()
    {
        try
        {
            var s = StateService.Load();
            if (WindowState == WindowState.Normal) { s.WindowWidth = (int)Width; s.WindowHeight = (int)Height; }
            StateService.Save(s);
        }
        catch (Exception ex) { Log.Error("SaveWindowSize", ex); }
    }

    public void SetOpacityPercent(int pct) => Opacity = Math.Clamp(pct, 40, 100) / 100.0;

    private void CheckPendingNotifications()
    {
        if (!IsVisible) return;
        var pending = NotificationCenter.TakePending();
        if (pending.Count == 0) return;
        _ = ShowPendingNotificationsGuardedAsync(pending);
    }

    private async Task ShowPendingNotificationsGuardedAsync(List<PendingNotification> pending)
    {
        try { await PendingNotificationsDialog.ShowAsync(this, pending); }
        catch (Exception ex) { Log.Error("PendingNotificationsDialog.ShowAsync", ex); }
    }

    /// <summary>True when the window is something the user can currently see (not hidden to the menu bar, not minimised).</summary>
    public bool IsOnScreen() => IsVisible && WindowState != WindowState.Minimized;

    internal async Task HandleNotificationActivation(string action, IDictionary<string, string> args)
    {
        ShowFromTray();
        switch (action)
        {
            case ToastArgs.Kickoff:
                await KickoffDialog.ShowAsync(this);
                break;
            case ToastArgs.IdleReturn:
                if (args.TryGetValue(ToastArgs.Mins, out var minsStr) && int.TryParse(minsStr, out var mins) &&
                    args.TryGetValue(ToastArgs.Start, out var startStr) &&
                    DateTime.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start))
                {
                    var alreadyAnswered = false;
                    try
                    {
                        using var conn = AppPaths.OpenConnection();
                        alreadyAnswered = !DiaryWriter.HasIdlePlaceholder(conn, start, start.AddMinutes(mins));
                    }
                    catch (Exception ex) { Log.Error("HandleNotificationActivation.HasIdlePlaceholder", ex); }
                    if (!alreadyAnswered) await IdleReturnDialog.ShowAsync(this, mins, start);
                }
                break;
            case ToastArgs.Review:
                await ReviewDialog.ShowAsync(this);
                break;
        }
    }
}

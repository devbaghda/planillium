using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Pages;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

public sealed partial class MainWindow : Window
{
    public ActivityTracker? Tracker { get; private set; }
    private readonly DispatcherQueue _dq;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Mentor Overseer";
        _dq = DispatcherQueue;

        // Mica ground; falls back to the theme's solid color on unsupported OS.
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));

        ApplyOpacity(StateService.Load().Opacity);

        // Theme override saved in Settings (default = follow Windows).
        if (Content is FrameworkElement root)
            root.RequestedTheme = StateService.Load().Theme switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

        // Debug/verification hook: MENTOR_PAGE=reports|plans|settings|schedule
        // opens straight on that page (default: Today). Drives the nav
        // selection so the rail highlight and page can't disagree.
        var page = Environment.GetEnvironmentVariable("MENTOR_PAGE") ?? "today";
        if (page == "settings")
            // Nav.SettingsItem isn't materialized until the template applies —
            // navigate the frame directly (rail highlight not needed here).
            ContentFrame.Navigate(typeof(SettingsPage));
        else
            Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => (string?)i.Tag == page)
                ?? Nav.MenuItems.OfType<NavigationViewItem>().First();
        RefreshScore();
        StartTracker();
        CatchUpScores();
        StartEodWatcher();

        InitTray();

        // Closing the window hides to the tray — tracking and focus alerts
        // continue. Actually quitting is the tray menu's job.
        AppWindow.Closing += (_, e) =>
        {
            if (_reallyClose) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        Closed += (_, _) =>
        {
            Tracker?.Stop();
            _tray?.Dispose();
        };
    }

    // ── tray ──────────────────────────────────────────────────────────────

    private H.NotifyIcon.TaskbarIcon? _tray;

    private void InitTray()
    {
        try
        {
            var open = new MenuFlyoutItem { Text = "Open Mentor Overseer" };
            open.Click += (_, _) => ShowFromTray();
            var quit = new MenuFlyoutItem { Text = "Quit (stops tracking)" };
            quit.Click += (_, _) => { _reallyClose = true; Close(); };
            var menu = new MenuFlyout();
            menu.Items.Add(open);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(quit);

            var openCommand = new Microsoft.UI.Xaml.Input.XamlUICommand();
            openCommand.ExecuteRequested += (_, _) => ShowFromTray();

            _tray = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "Mentor Overseer",
                Icon = new System.Drawing.Icon(
                    Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")),
                ContextFlyout = menu,
                ContextMenuMode = H.NotifyIcon.ContextMenuMode.SecondWindow,
                LeftClickCommand = openCommand,
            };
            _tray.ForceCreate();
        }
        catch (Exception ex)
        {
            // No tray = closing must really close, or the app becomes unkillable.
            Log.Error("InitTray (window close will quit instead)", ex);
            _tray = null;
            _reallyClose = true;
        }
    }

    public void HideToTray() => AppWindow.Hide();

    // ── window opacity ────────────────────────────────────────────────────

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
    private const int GwlExstyle = -20;
    private const long WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x2;

    /// <summary>Whole-window opacity (Settings slider), 40–100 %. At 100 the
    /// layered style is removed so DWM effects (Mica) run untouched.</summary>
    public void ApplyOpacity(int percent)
    {
        try
        {
            percent = Math.Clamp(percent, 40, 100);
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var exStyle = GetWindowLongPtrW(hwnd, GwlExstyle).ToInt64();
            if (percent >= 100)
            {
                if ((exStyle & WsExLayered) != 0)
                    SetWindowLongPtrW(hwnd, GwlExstyle, new IntPtr(exStyle & ~WsExLayered));
                return;
            }
            if ((exStyle & WsExLayered) == 0)
                SetWindowLongPtrW(hwnd, GwlExstyle, new IntPtr(exStyle | WsExLayered));
            SetLayeredWindowAttributes(hwnd, 0, (byte)(percent * 255 / 100), LwaAlpha);
        }
        catch (Exception ex)
        {
            Log.Error("ApplyOpacity", ex);
        }
    }

    private void ShowFromTray()
    {
        _dq.TryEnqueue(() =>
        {
            AppWindow.Show();
            Activate();
        });
    }

    private bool _reallyClose;
    private string? _eodOfferedOn;

    private static void CatchUpScores()
    {
        try
        {
            var plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            score.EnsureScoreCaughtUp();
        }
        catch (Exception ex)
        {
            Log.Error("CatchUpScores (db likely locked — next launch retries)", ex);
        }
    }

    /// <summary>At the configured end-of-day time, offer the evening review once.</summary>
    private void StartEodWatcher()
    {
        var timer = _dq.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += async (_, _) =>
        {
            // ">=" not "==": an exact-minute match on a drifting 1-minute
            // timer can skip the minute and never offer the review that day.
            var today = DateTime.Today.ToString("yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
            if (DateTime.Now.TimeOfDay >= EodTime() &&
                _eodOfferedOn != today &&
                StateService.Load().LastReview != today)
            {
                _eodOfferedOn = today;  // offer once per day; "Later" means later by choice
                await Dialogs.ReviewDialog.ShowAsync(this);
            }
        };
        timer.Start();
    }

    public static TimeSpan EodTime()
    {
        var eod = "20:00";
        if (ConfigService.Root.TryGetProperty("end_of_day_summary_time", out var v) &&
            v.GetString() is { Length: > 0 } s) eod = s;
        return TimeSpan.TryParse(eod, out var t) ? t : new TimeSpan(20, 0, 0);
    }

    public void RefreshScore()
    {
        try
        {
            using var db = new Database();
            ScoreValue.Text = db.ScoreBalance().ToString();
        }
        catch (Exception ex)
        {
            Log.Error("RefreshScore", ex);
            ScoreValue.Text = "—";
        }
    }

    // ── activity tracker wiring ───────────────────────────────────────────

    /// <summary>Stop and re-create the tracker — Settings saves call this so
    /// new keywords/thresholds apply immediately.</summary>
    public void RestartTracker()
    {
        Tracker?.Stop();
        Tracker = null;
        StartTracker();
    }

    private void StartTracker()
    {
        // Always start — the tracker itself pauses per-poll whenever the
        // Python app is running, whichever order the two were launched in.
        try
        {
            Tracker = new ActivityTracker(ConfigService.Root);
            Tracker.OnStatus += (cls, window) => _dq.TryEnqueue(() => UpdatePill(cls, window));
            Tracker.OnAlert += (title, msg) => _dq.TryEnqueue(() => Notify(title, msg));
            Tracker.OnIdleReturn += (mins, start) =>
                _dq.TryEnqueue(() => _ = IdleReturnDialog.ShowAsync(this, mins, start));
            Tracker.Start();
        }
        catch (Exception ex)
        {
            Log.Error("StartTracker", ex);
            ActivityText.Text = "Tracker unavailable";
        }
    }

    private void UpdatePill(string cls, string window)
    {
        var (label, brushKey) = cls switch
        {
            "on_plan" => ("On plan", "SystemFillColorSuccessBrush"),
            "off_plan" => ("Off plan", "SystemFillColorCriticalBrush"),
            "paid" => ("Paid time", "AccentFillColorDefaultBrush"),
            "paused" => ("Python app is tracking", "TextFillColorTertiaryBrush"),
            _ => ("Neutral", "TextFillColorTertiaryBrush"),
        };
        ActivityText.Text = Tracker?.OffPlanMinutes is int m and > 0
            ? $"{label} · {m}m" : label;
        ActivityDot.Fill = (Brush)Application.Current.Resources[brushKey];

        var app = window.Split('–', '—', '-')[0].Trim();
        ActivityWindow.Text = app.Length > 60 ? app[..60] : app;
    }

    private static void Notify(string title, string message)
    {
        try
        {
            var toast = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            AppNotificationManager.Default.Show(toast);
        }
        catch (Exception ex)
        {
            // Pill still shows the off-plan state, but a broken toast path is
            // a broken core feature — leave a trace.
            Log.Error("Notify (toast)", ex);
        }
    }

    private async void BuyTime_Click(object sender, RoutedEventArgs e) =>
        await Dialogs.SpendDialog.ShowBuyAsync(this);

    private async void LogSpend_Click(object sender, RoutedEventArgs e) =>
        await Dialogs.SpendDialog.ShowLogSpendAsync(this);

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        switch (tag)
        {
            case "today":    ContentFrame.Navigate(typeof(TodayPage)); break;
            case "schedule": ContentFrame.Navigate(typeof(SchedulePage)); break;
            case "reports":  ContentFrame.Navigate(typeof(ReportsPage)); break;
            case "plans":    ContentFrame.Navigate(typeof(PlansPage)); break;
        }
    }
}

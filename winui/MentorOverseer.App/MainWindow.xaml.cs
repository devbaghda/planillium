using System.Diagnostics;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
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
        Title = AppInfo.DisplayName;
        // Ensure the custom title bar text matches the app display name
        try
        {
            TitleText.Text = AppInfo.DisplayName;
        }
        catch (Exception ex) { Log.Error("MainWindow.SetTitleText", ex); }
        _dq = DispatcherQueue;

        // Mica ground; falls back to the theme's solid color on unsupported OS.
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        var savedState = StateService.Load();
        // Clamp to the monitor's actual work area, not just a floor — a
        // maximized custom-title-bar window can report/save a size a few
        // pixels taller/wider than the visible screen (WinAppSDK invisible-
        // resize-border quirk), which then persists forever and reopens
        // partially off-screen on every future launch since Resize() alone
        // has no ceiling.
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Clamp(savedState.WindowWidth, MinWidthDip, workArea.Width);
        var height = Math.Clamp(savedState.WindowHeight, MinHeightDip, workArea.Height);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        // Resize() alone doesn't move the window — the app never saves/
        // restores position, so it keeps whatever default placement the OS
        // assigned a fresh AppWindow, which can push a work-area-sized
        // window off the right/bottom edge. Clamp position against the
        // now-final size so the whole window stays on-screen.
        var pos = AppWindow.Position;
        AppWindow.Move(new Windows.Graphics.PointInt32(
            Math.Clamp(pos.X, workArea.X, workArea.X + workArea.Width - width),
            Math.Clamp(pos.Y, workArea.Y, workArea.Y + workArea.Height - height)));
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        AppWindow.SetIcon(iconPath);
        // The custom title bar's own logo mark used to be a placeholder
        // FontIcon glyph (a generic checkmark) instead of the real artwork —
        // load the same file the taskbar/tray icon use so all three agree.
        try { AppLogoImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)); }
        catch (Exception ex) { Log.Error("MainWindow.AppLogoImage", ex); }
        InstallMinSizeHook();

        ApplyOpacity(savedState.Opacity);

        // Theme override saved in Settings (default = follow Windows).
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = StateService.Load().Theme switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            // Deferred to Loaded rather than called here directly: the two
            // accent-derived keys ThemeSync.Apply reads (SystemAccentColor*)
            // aren't guaranteed available until the resource tree is fully
            // merged. root.ActualTheme is the true resolved theme (Light/
            // Dark, never Default), and the ActualThemeChanged subscription
            // keeps it correct afterward if "Follow Windows" is selected and
            // the OS theme changes while running.
            root.Loaded += (_, _) => ThemeSync.Apply(root.ActualTheme);
            root.ActualThemeChanged += (sender, _) => ThemeSync.Apply(sender.ActualTheme);
            // InitTrackingAsync (which may show the first-run disclosure
            // ContentDialog) also has to wait for Loaded — Content.XamlRoot
            // isn't valid until the visual tree is actually loaded, and
            // calling ContentDialog.ShowAsync before that throws
            // ArgumentException ("This element does not have a XamlRoot"),
            // which on a fire-and-forget task is silently swallowed by the
            // UnobservedTaskException handler — so the tracker would never
            // start on first run at all, not just start too early. Caught
            // by testing this in the isolated environment before this fix
            // shipped; see CONTEXT.md.
            root.Loaded += (_, _) => _ = InitTrackingAsync();
        }
        else
        {
            // Defensive fallback — shouldn't happen for a XAML-defined
            // Window, but without this branch a null/non-FrameworkElement
            // Content would mean the tracker never starts at all.
            _ = InitTrackingAsync();
        }

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
        RunStartupCatchUp();
        StartEodWatcher();
        StartKickoffWatcher();

        InitTray();
        // Click-to-reopen for kickoff/idle-return toasts. Best-effort: if this
        // throws (seen once as a COMException 0x80070490 "Element not found"
        // — likely sensitive to being subscribed before AppNotificationManager
        // .Register(), which App.OnLaunched now does deliberately after
        // constructing this window), the toasts themselves still work via
        // ToastNotifier — losing click-through isn't worth crashing the whole
        // app over, since an unhandled exception here previously took the
        // entire tracker down at launch.
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            _notificationActivationWired = true;
        }
        catch (Exception ex)
        {
            Log.Error("NotificationInvoked subscribe (toast click-through disabled)", ex);
        }

        // Closing the window hides to the tray — tracking and focus alerts
        // continue. Actually quitting is the tray menu's job.
        AppWindow.Closing += (_, e) =>
        {
            SaveWindowSize();
            if (_reallyClose) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        Closed += (_, _) =>
        {
            Tracker?.Stop();
            _tray?.Dispose();
            if (_notificationActivationWired)
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            if (_hwnd != IntPtr.Zero) WTSUnRegisterSessionNotification(_hwnd);
        };
    }

    private bool _notificationActivationWired;

    /// <summary>
    /// Whether the window is actually something the user can currently see —
    /// not hidden to the tray, and not minimized. Kickoff/idle-return prompts
    /// use this to decide between opening their interactive ContentDialog
    /// directly versus routing through a toast notification instead; a
    /// ContentDialog inside a hidden or minimized window renders but is never
    /// seen, which is exactly the gap toast routing exists to close.
    /// </summary>
    public bool IsOnScreen() =>
        AppWindow.IsVisible &&
        (AppWindow.Presenter as OverlappedPresenter)?.State != OverlappedPresenterState.Minimized;

    /// <summary>Bring the window forward, then open the dialog a clicked
    /// toast (kickoff or welcome-back) was standing in for.</summary>
    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue(ToastArgs.Action, out var action)) return;
        _dq.TryEnqueue(() => _ = HandleNotificationActivation(action, args.Arguments));
    }

    private async Task HandleNotificationActivation(string action, IDictionary<string, string> args)
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
                    DateTime.TryParse(startStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var start))
                {
                    await IdleReturnDialog.ShowAsync(this, mins, start);
                }
                break;
            case ToastArgs.Review:
                await ReviewDialog.ShowAsync(this);
                break;
        }
    }

    // ── tray ──────────────────────────────────────────────────────────────

    private H.NotifyIcon.TaskbarIcon? _tray;

    private void InitTray()
    {
        try
        {
            var open = new MenuFlyoutItem { Text = $"Open {AppInfo.DisplayName}" };
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
                ToolTipText = AppInfo.DisplayName,
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

    // ── minimum window size ──────────────────────────────────────────────
    //
    // WinAppSDK 1.5's OverlappedPresenter has no PreferredMinimumSize yet, so
    // an unresizable-by-us window could be dragged down to a few pixels and
    // wedge NavigationView/page content into a broken state — read by the
    // user as "resize doesn't work well." WM_GETMINMAXINFO is the native way
    // to enforce a floor: the OS itself refuses to drag past it, so it's as
    // smooth as any other window's resize (no fighting the live drag).

    private const int MinWidthDip = 900;
    private const int MinHeightDip = 600;
    private const int GwlpWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;

    // Windows session lock/unlock notifications (2026-07-09 audit finding
    // #11) — piggybacks on the same WndProc subclass hook the min-size fix
    // already installs, rather than adding a second one.
    private const uint WmWtsSessionChange = 0x02B1;
    private const int WtsSessionLock = 0x7;
    private const int NotifyForThisSession = 0;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _origWndProc;
    private IntPtr _hwnd;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);
    [System.Runtime.InteropServices.DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    private void InstallMinSizeHook()
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _wndProcDelegate = (hWnd2, msg, wParam, lParam) =>
        {
            if (msg == WmGetMinMaxInfo)
            {
                var scale = GetDpiForWindow(hWnd2) / 96.0;
                var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MinMaxInfo>(lParam);
                mmi.ptMinTrackSize.X = (int)(MinWidthDip * scale);
                mmi.ptMinTrackSize.Y = (int)(MinHeightDip * scale);
                System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }
            // Session lock (Win+L, screen-saver): tell the tracker to close
            // out the current session right away instead of waiting out the
            // full idle threshold before noticing the user is gone
            // (2026-07-09 audit finding #11). Still forwarded to the
            // original WndProc below — this is observation, not a message
            // we need to override the default handling of.
            if (msg == WmWtsSessionChange && wParam.ToInt32() == WtsSessionLock)
                Tracker?.NotifySessionLocked();
            return CallWindowProcW(_origWndProc, hWnd2, msg, wParam, lParam);
        };
        _origWndProc = SetWindowLongPtrW(_hwnd, GwlpWndProc,
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        if (!WTSRegisterSessionNotification(_hwnd, NotifyForThisSession))
            Log.Error("InstallMinSizeHook.WTSRegisterSessionNotification",
                new InvalidOperationException(
                    $"Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}"));
    }

    private void SaveWindowSize()
    {
        try
        {
            var s = StateService.Load();
            s.WindowWidth = Math.Max(AppWindow.Size.Width, MinWidthDip);
            s.WindowHeight = Math.Max(AppWindow.Size.Height, MinHeightDip);
            StateService.Save(s);
        }
        catch (Exception ex)
        {
            Log.Error("SaveWindowSize", ex);
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

    /// <summary>
    /// CatchUpScores (up to 7 days of scoring math) and PruneOldDiary (a
    /// rollup INSERT + DELETE) used to run synchronously on the constructor
    /// before the window ever appeared — real, if usually small, delay
    /// added directly to how long the app takes to become visible on
    /// launch (2026-07-09 audit finding #7). Both already open their own
    /// Database/ScoreService connections (the same per-call-connection
    /// pattern ActivityTracker's background poll already uses), so running
    /// them off the UI thread is safe. RefreshScore runs again afterward in
    /// case catch-up added score for a missed day.
    /// </summary>
    private void RunStartupCatchUp()
    {
        _ = Task.Run(() =>
        {
            CatchUpScores();
            PruneOldDiary();
            _dq.TryEnqueue(RefreshScore);
        });
    }

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

    private static void PruneOldDiary()
    {
        try
        {
            using var db = new Database();
            // The parameter default is only a fallback — the actual
            // retention window is user-configurable (Settings, 2026-07-09
            // audit finding #34).
            db.PruneAndRollupDiary(ConfigService.DiaryRetentionDays());
        }
        catch (Exception ex)
        {
            Log.Error("PruneOldDiary", ex);
        }
    }

    /// <summary>At the configured end-of-day time, offer the evening review once.</summary>
    private void StartEodWatcher()
    {
        var timer = _dq.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += async (_, _) => await ReviewDialog.Trigger(this);
        timer.Start();
    }

    /// <summary>At the configured start-of-day time, show the morning kickoff
    /// once — regardless of which page happens to be open. Today page's own
    /// Loaded check still covers "opened after start time, before this timer's
    /// next tick"; KickoffDialog's _showing guard keeps the two from double-showing.</summary>
    private void StartKickoffWatcher()
    {
        var timer = _dq.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += async (_, _) => await KickoffDialog.Trigger(this);
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

    /// <summary>
    /// The first-run tracking disclosure must finish — be shown and
    /// dismissed by the user — before the tracker's poll timer can capture
    /// anything. Previously this only happened to hold true by incidental
    /// timing (TodayPage's OnNavigatedTo requested the dialog, but this
    /// constructor's call to StartTracker() didn't wait on it — an async
    /// void method returns control to its caller at the first await, so the
    /// constructor carried on and started tracking regardless). A slow
    /// first launch could start capturing/logging the foreground window
    /// before the user had seen or dismissed the notice (2026-07-09 privacy
    /// audit finding #2). This makes the ordering an actual code dependency
    /// instead of a coincidence.
    /// </summary>
    private async Task InitTrackingAsync()
    {
        await NameSetupDialog.EnsureShownAsync(this);
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
                _dq.TryEnqueue(() => _ = IdleReturnDialog.Trigger(this, mins, start));
            DateTime? lastDiaryEnd = null;
            try
            {
                using var db = new Database();
                lastDiaryEnd = db.LastDiaryEnd();
            }
            catch (Exception ex) { Log.Error("StartTracker.LastDiaryEnd", ex); }
            Tracker.Start(lastDiaryEnd);
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
            // A deliberate, known state (not "nothing detected" like Neutral)
            // gets the more visible of the two calibrated grays so the pill
            // isn't pixel-identical to Neutral at a glance.
            "dayoff" => ("Day off", "TextFillColorSecondaryBrush"),
            _ => ("Neutral", "TextFillColorTertiaryBrush"),
        };
        ActivityText.Text = Tracker?.OffPlanMinutes is int m and > 0
            ? $"{label} · {m}m" : label;
        ActivityDot.Fill = (Brush)Application.Current.Resources[brushKey];

        var app = window.Split('–', '—', '-')[0].Trim();
        ActivityWindow.Text = app.Length > 60 ? app[..60] : app;
    }

    private static void Notify(string title, string message) => ToastNotifier.Show(title, message, tag: null);

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

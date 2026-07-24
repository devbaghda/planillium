using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
using Planillium.App.Dialogs;
using Planillium.App.Pages;
using Planillium.App.Services;

namespace Planillium.App;

// This window's code-behind is split across several files by responsibility
// (all still the one MainWindow type — partial class, not separate objects):
//   MainWindow.xaml.cs           — this file: lifecycle, notification click-
//                                  through, nav rail
//   MainWindow.Tray.cs           — the notify-icon / tray menu
//   MainWindow.NativeInterop.cs  — window opacity + min-size WndProc hook
//   MainWindow.Startup.cs        — startup catch-up, EOD/kickoff watchers,
//                                  sidebar score + plan-drift refresh
//   MainWindow.Tracker.cs        — ActivityTracker wiring, status pill
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
            // Same XamlRoot-not-ready race as InitTrackingAsync above, reproduced live
            // (2026-07-22): the very first Activated can fire during construction, before
            // Loaded, so CheckPendingNotifications' guard below skips it that once and simply
            // leaves the state untouched — Loaded firing shortly after (whichever of the two
            // ends up running last) is what actually catches it, since TakePending() draining
            // to empty on whichever runs first makes the other a safe no-op.
            root.Loaded += (_, _) => CheckPendingNotifications();
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
        // selection so the rail highlight and page can't disagree. #if DEBUG-
        // gated (round-5 audit finding #24) — unlike MENTOR_ROOT, this has no
        // legitimate purpose for a real user running the shipped Release exe.
        var page = "today";
#if DEBUG
        page = Environment.GetEnvironmentVariable("MENTOR_PAGE") ?? "today";
#endif
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
        StartLateDayTaskReminderWatcher();
        StartDiaryPruneWatcher();

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
            DisposeTray();
            if (_notificationActivationWired)
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            if (_hwnd != IntPtr.Zero) WTSUnRegisterSessionNotification(_hwnd);
        };

        // Being brought to the foreground is still the trigger (2026-07-20 request), but as of
        // 2026-07-22 it now shows what actually fired instead of just silently clearing the dot
        // — a plain tray-icon click used to open the app with no way to see what the red dot had
        // been about. TakePending() clears the unread state itself, so this only shows a dialog
        // when there's genuinely something to recap.
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                CheckPendingNotifications();
        };
    }

    /// <summary>Shows the "while you were away" recap if anything's pending — guarded against
    /// the XamlRoot-not-ready race (see the Loaded hook above): if Content isn't a loaded
    /// FrameworkElement yet, this just does nothing and leaves the pending state untouched
    /// rather than draining it into a dialog that would throw.</summary>
    private void CheckPendingNotifications()
    {
        if (Content is not FrameworkElement { XamlRoot: not null }) return;
        var pending = NotificationCenter.TakePending();
        if (pending.Count == 0) return;
        // Guarded like every other fire-and-forget dispatch in this file (2026-07-24 audit
        // finding #3) — an unguarded fault here becomes an unobserved task exception, and the
        // practical effect is the recap silently never appearing with nothing in the log to
        // explain why, even though TakePending() already cleared the tray's unread dot. Called
        // directly (not via _dq.TryEnqueue) since this method itself runs on the UI thread
        // already, from the Activated event handler above.
        _ = ShowPendingNotificationsGuardedAsync(pending);
    }

    private async Task ShowPendingNotificationsGuardedAsync(List<PendingNotification> pending)
    {
        try { await PendingNotificationsDialog.ShowAsync(this, pending); }
        catch (Exception ex) { Log.Error("PendingNotificationsDialog.ShowAsync", ex); }
    }

    private bool _notificationActivationWired;
    private bool _reallyClose;

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
        // Same guarded-dispatch shape as Tracker.OnIdleReturn (MainWindow.Tracker.cs) — an
        // unawaited fire-and-forget Task here would become an unobserved task exception,
        // surfacing (if ever) only at GC finalization instead of in the log (audit finding #1).
        _dq.TryEnqueue(async () =>
        {
            try { await HandleNotificationActivation(action, args.Arguments); }
            catch (Exception ex) { Log.Error("HandleNotificationActivation", ex); }
        });
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
            case "today": ContentFrame.Navigate(typeof(TodayPage)); break;
            case "schedule": ContentFrame.Navigate(typeof(SchedulePage)); break;
            case "reports": ContentFrame.Navigate(typeof(ReportsPage)); break;
            case "plans": ContentFrame.Navigate(typeof(PlansPage)); break;
        }
    }
}

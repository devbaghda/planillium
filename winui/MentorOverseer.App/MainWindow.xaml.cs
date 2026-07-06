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

        Closed += (_, _) => Tracker?.Stop();
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
        catch { /* db briefly locked — next launch catches up */ }
    }

    /// <summary>At the configured end-of-day time, offer the evening review once.</summary>
    private void StartEodWatcher()
    {
        var timer = _dq.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += async (_, _) =>
        {
            var eod = "20:00";
            if (ConfigService.Root.TryGetProperty("end_of_day_summary_time", out var v) &&
                v.GetString() is { Length: > 0 } s) eod = s;
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            if (DateTime.Now.ToString("HH:mm") == eod &&
                StateService.Load().LastReview != today)
            {
                await Dialogs.ReviewDialog.ShowAsync(this);
            }
        };
        timer.Start();
    }

    public void RefreshScore()
    {
        try
        {
            using var db = new Database();
            ScoreValue.Text = db.ScoreBalance().ToString();
        }
        catch
        {
            ScoreValue.Text = "—";
        }
    }

    // ── activity tracker wiring ───────────────────────────────────────────

    private void StartTracker()
    {
        // The Python app runs the same tracker against the same database —
        // never track twice in parallel.
        if (Process.GetProcessesByName("MentorOverseer").Length > 0)
        {
            ActivityText.Text = "Python app is tracking";
            return;
        }

        try
        {
            Tracker = new ActivityTracker(ConfigService.Root);
            Tracker.OnStatus += (cls, window) => _dq.TryEnqueue(() => UpdatePill(cls, window));
            Tracker.OnAlert += (title, msg) => _dq.TryEnqueue(() => Notify(title, msg));
            Tracker.OnIdleReturn += (mins, start) =>
                _dq.TryEnqueue(() => _ = IdleReturnDialog.ShowAsync(this, mins, start));
            Tracker.Start();
        }
        catch
        {
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
        catch
        {
            // Toasts can fail for unpackaged apps on older OS builds — the
            // status pill still shows the off-plan state, so stay silent.
        }
    }

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
            case "schedule": ContentFrame.Navigate(typeof(StubPage), "Schedule"); break;
            case "reports":  ContentFrame.Navigate(typeof(ReportsPage)); break;
            case "plans":    ContentFrame.Navigate(typeof(PlansPage)); break;
        }
    }
}

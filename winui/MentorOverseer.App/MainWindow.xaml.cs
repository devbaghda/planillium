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

        // Closing the window stops tracking (no tray yet) — make that an
        // informed decision instead of a silent end to the diary.
        AppWindow.Closing += (_, e) =>
        {
            if (_reallyClose || Tracker is not { Running: true }) return;
            e.Cancel = true;
            _dq.TryEnqueue(async () =>
            {
                var confirm = new ContentDialog
                {
                    Title = "Close Mentor Overseer?",
                    Content = "Activity tracking and focus alerts stop until you open it again.",
                    PrimaryButtonText = "Close anyway",
                    CloseButtonText = "Stay open",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot,
                };
                if (await DialogGate.ShowAsync(confirm) == ContentDialogResult.Primary)
                {
                    _reallyClose = true;
                    Close();
                }
            });
        };
        Closed += (_, _) => Tracker?.Stop();
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

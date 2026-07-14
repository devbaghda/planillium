using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

// ActivityTracker wiring and the sidebar status pill — see MainWindow.xaml.cs
// for the file split.
public sealed partial class MainWindow
{
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
}

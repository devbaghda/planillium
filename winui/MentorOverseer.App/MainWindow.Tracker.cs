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
            // async lambda (not a discarded "_ = Trigger(...)") so a fault
            // inside PromptRouter/ShowAsync is actually caught and logged
            // instead of becoming an unobserved task exception — the exact
            // silent-failure shape already seen once from this same call
            // (2026-07-09 log: XamlRoot missing inside IdleReturnDialog,
            // never surfaced until finalization). A prompt that silently
            // never shows and never logs is indistinguishable from "the
            // gap was never detected at all," which made the 2026-07-15
            // missing-morning-prompt report hard to diagnose from the code
            // alone — this at least closes that blind spot going forward.
            Tracker.OnIdleReturn += (mins, start) => _dq.TryEnqueue(async () =>
            {
                try { await IdleReturnDialog.Trigger(this, mins, start); }
                catch (Exception ex) { Log.Error("IdleReturnDialog.Trigger", ex); }
            });
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
        // Color comes from the shared CategoryStyle.BrushKey table (Services/CategoryStyle.cs)
        // — this pill used to keep its own independent copy that had drifted from the Reports
        // page's colors for "paid" and the neutral default (round-7 audit finding #3).
        var label = cls switch
        {
            DiaryCategory.OnPlan => "On plan",
            DiaryCategory.OffPlan => "Off plan",
            DiaryCategory.Paid => "Paid time",
            DiaryCategory.DayOff => "Day off",
            _ => "Neutral",
        };
        ActivityText.Text = Tracker?.OffPlanMinutes is int m and > 0
            ? $"{label} · {m}m" : label;
        ActivityDot.Fill = (Brush)Application.Current.Resources[CategoryStyle.BrushKey(cls)];

        var app = window.Split('–', '—', '-')[0].Trim();
        ActivityWindow.Text = app.Length > 60 ? app[..60] + "…" : app;
    }

    private static void Notify(string title, string message) => ToastNotifier.Show(title, message, tag: null);
}

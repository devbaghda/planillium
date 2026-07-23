using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// Morning kickoff — the day's designed beginning: one spotlighted task with
/// its mentor note, the rest of the day at a glance, the time budget, and
/// yesterday's result. Shows once per day, at or after the configured
/// working-day start time — triggered both by MainWindow's per-minute
/// watcher (fires even if Today isn't the open page) and by TodayPage's
/// Loaded handler (catches the case where the app is opened after start
/// time but before the watcher's next tick).
/// </summary>
public static class KickoffDialog
{
    // Guards against the two trigger paths (TodayPage.Loaded and MainWindow's
    // per-minute watcher) both passing ShouldShow() in the same instant and
    // queuing two kickoff dialogs back to back through DialogGate.
    private static bool _showing;

    // Separate from StateService.LastKickoff (which now means "the real
    // dialog was actually shown," not "we tried") — this only exists to
    // stop the per-minute watcher from re-sending the toast every single
    // minute the window stays hidden. In-memory/per-run is fine: a restart
    // re-nudging once is an acceptable, arguably correct, edge case.
    private static string? _toastSentOn;

    public static bool ShouldShow()
    {
        if (_showing) return false;
        if (DateTime.Now.TimeOfDay < ConfigService.WorkStartTime()) return false;
        return StateService.Load().LastKickoff !=
               DateTime.Today.ToIsoDate();
    }

    /// <summary>
    /// Background-watcher entry point: shows the interactive dialog directly
    /// when the window is actually on screen, otherwise raises a toast so the
    /// prompt still reaches the user while the app sits in the tray — a bare
    /// ContentDialog inside a hidden window is invisible and never seen.
    /// Deliberately does NOT call <see cref="MarkShownToday"/> on the toast
    /// path: only the real dialog actually being opened counts as "shown."
    /// A toast that's missed or dismissed unseen must not silently burn the
    /// day's one kickoff — <see cref="ShouldShow"/> keeps returning true
    /// (so opening the app later that day still offers the real card), and
    /// <see cref="_toastSentOn"/> alone stops the per-minute watcher from
    /// re-nudging every single minute in the meantime.
    /// </summary>
    public static Task Trigger(MainWindow window)
    {
        if (!ShouldShow()) return Task.CompletedTask;
        var today = DateTime.Today.ToIsoDate();
        var name = ConfigService.UserName is { Length: > 0 } n ? n : "there";
        return PromptRouter.ShowOrToast(window, () => ShowAsync(window),
            () => _toastSentOn == today, () => _toastSentOn = today,
            $"Good morning, {name}.", "Time to start your day — click to see today's plan.",
            ToastArgs.Kickoff, (ToastArgs.Action, ToastArgs.Kickoff));
    }

    public static void MarkShownToday()
    {
        var state = StateService.Load();
        state.LastKickoff = DateTime.Today.ToIsoDate();
        StateService.Save(state);
    }

    public static async Task ShowAsync(MainWindow window)
    {
        // The guard this field exists for (see its doc comment above) was never actually
        // wired up here — round-5 audit finding #2, mirror of the round-3 ReviewDialog gap.
        if (_showing) return;
        _showing = true;
        try
        {
            await ShowCore(window);
        }
        finally
        {
            _showing = false;
        }
    }

    private static async Task ShowCore(MainWindow window)
    {
        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        var completions = db.LoadCompletions();

        // Today's open tasks across all plans, spotlight = longest duration.
        var todayTasks = new List<(Plan Plan, AssignedTask Task)>();
        foreach (var plan in plans.Where(p => p.PlanDay >= 1))
            foreach (var t in PlanStore.TasksFor(plan, db, completions))
                if (t.AssignedDay == plan.PlanDay && !t.Completed)
                    todayTasks.Add((plan, t));

        var spotlight = todayTasks
            .OrderByDescending(x => x.Task.Task.DurationMin ?? 0)
            .FirstOrDefault();
        var budget = todayTasks.Sum(x => x.Task.Task.DurationMin ?? 30);

        var panel = new StackPanel { Spacing = 14, MinWidth = 460 };
        var dateText = DateTime.Today.ToDisplayDateFull();
        panel.Children.Add(new TextBlock
        {
            // "0 tasks, 0h 00m of focused work" is a demotivating way to
            // describe a free day — say what it actually is.
            Text = todayTasks.Count > 0
                ? $"{dateText} — {todayTasks.Count} task(s), about {budget / 60}h {budget % 60:00}m of focused work."
                : $"{dateText} — nothing scheduled today. A rest day, or a chance to clear the overdue list.",
            TextWrapping = TextWrapping.Wrap,
        });

        if (spotlight.Task != null)
        {
            var focus = new StackPanel { Spacing = 4 };
            focus.Children.Add(new TextBlock
            {
                Text = "FOCUS FIRST",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                CharacterSpacing = 80,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            });
            focus.Children.Add(new TextBlock
            {
                Text = spotlight.Task.Task.Text,
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap,
            });
            if (spotlight.Task.Task.MentorNote is { Length: > 0 } note)
                focus.Children.Add(new TextBlock
                {
                    Text = "Mentor's note: " + note,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                });
            panel.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(14, 6, 0, 6),
                Child = focus,
            });
        }

        var rest = todayTasks.Where(x => x.Task != spotlight.Task).ToList();
        if (rest.Count > 0)
            panel.Children.Add(new TextBlock
            {
                Text = "Then: " + string.Join(" · ", rest.Select(x => x.Task.Task.Text)),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });

        // Yesterday's recap + streak.
        using (var score = new ScoreService(plans, db))
        {
            var y = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
            var (total, done) = score.DayTaskCounts(y);
            var streak = score.CurrentStreak();
            var recap = total > 0 ? $"Yesterday: {done}/{total} done." : "";
            if (streak > 0) recap += $"  🔥 {streak}-day streak — keep it alive.";
            if (recap.Length > 0)
                panel.Children.Add(new TextBlock
                {
                    Text = recap.Trim(),
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                });
        }

        var name = ConfigService.UserName is { Length: > 0 } n ? n : "there";
        // The only recurring dialog in the app with no secondary/close button at all —
        // every sibling (ReviewDialog, IdleReturnDialog) offers a way out (2026-07-14
        // round-6 audit finding #24). "Start the day" doesn't do anything besides dismiss
        // the card (the day's tasks are already visible on Today regardless), so this is a
        // genuine escape hatch, not a hidden way to skip something that otherwise wouldn't
        // happen.
        var dialog = DialogControls.Build(window.Content.XamlRoot, $"Good morning, {name}.", panel,
            primaryButtonText: "Start the day", closeButtonText: "Later", defaultButton: ContentDialogButton.Primary);
        await DialogGate.ShowAsync(dialog);

        MarkShownToday();
    }
}

using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Morning kickoff — the day's designed beginning: one spotlighted task with
/// its mentor note, the rest of the day at a glance, the time budget, and
/// yesterday's result. Shows once per day, first launch after 06:00.
/// </summary>
public static class KickoffDialog
{
    public static bool ShouldShow()
    {
        if (DateTime.Now.Hour < 6) return false;
        return StateService.Load().LastKickoff !=
               DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static async Task ShowAsync(MainWindow window)
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
        var dateText = DateTime.Today.ToString("dddd dd.MM", CultureInfo.InvariantCulture);
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
        var dialog = new ContentDialog
        {
            Title = $"Good morning, {name}.",
            Content = panel,
            PrimaryButtonText = "Start the day",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };
        await DialogGate.ShowAsync(dialog);

        var state = StateService.Load();
        state.LastKickoff = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        StateService.Save(state);
    }
}

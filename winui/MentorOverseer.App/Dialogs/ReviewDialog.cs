using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Evening review — the day's designed ending: completion ring, readable
/// score ledger (v2 rules, floor visible), one-line reflection, and the
/// "Close the day" action that writes the guarded daily_score /
/// overdue_accrual ledger rows.
/// </summary>
public static class ReviewDialog
{
    public static async Task ShowAsync(MainWindow window)
    {
        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var (total, done) = score.DayTaskCounts(today);
        var (onMin, offMin) = score.DayDiaryMinutes(today);
        var streak = score.CurrentStreak();

        var taskPt = done * ConfigService.ScoringRate("task_completed", 10);
        var onPt = (int)(onMin / 60.0 * ConfigService.ScoringRate("on_plan_hour", 3));
        var offPt = (int)(offMin / 60.0 * ConfigService.ScoringRate("off_plan_hour", -2));
        var missPt = Math.Max(0, total - done) * ConfigService.ScoringRate("task_overdue_penalty", -5);
        var streakPt = streak * ConfigService.ScoringRate("streak_bonus_per_day", 5);
        var dayTotal = Math.Max(taskPt + onPt + offPt + missPt + streakPt, ScoreService.DailyFloor);

        var overdueCapped = score.OverdueAsOf(today)
            .Count(x => x.DaysOverdue <= ScoreService.OverdueAccrualCapDays);
        var accrual = overdueCapped * ConfigService.ScoringRate("task_overdue_penalty", -5);

        var panel = new StackPanel { Spacing = 12, MinWidth = 460 };

        var ring = new ProgressRing
        {
            IsIndeterminate = false,
            Value = total > 0 ? done * 100.0 / total : 0,
            Width = 72, Height = 72,
        };
        var ringRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 18 };
        ringRow.Children.Add(ring);
        var ringText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        ringText.Children.Add(new TextBlock
        {
            Text = $"{done}/{total} tasks done",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        ringText.Children.Add(new TextBlock
        {
            Text = $"{onMin / 60}h {onMin % 60:00}m on-plan · {offMin}m off-plan",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        ringRow.Children.Add(ringText);
        panel.Children.Add(ringRow);

        var ledger = new StackPanel { Spacing = 2 };
        void Row(string label, int amount, bool always = false)
        {
            if (amount == 0 && !always) return;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = new TextBlock { Text = label };
            var v = new TextBlock
            {
                Text = amount > 0 ? $"+{amount}" : amount.ToString(),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources[
                    amount >= 0 ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"],
            };
            Grid.SetColumn(v, 1);
            g.Children.Add(l);
            g.Children.Add(v);
            ledger.Children.Add(g);
        }
        Row($"{done} task(s) completed", taskPt);
        Row("On-plan focus", onPt);
        Row($"{streak}-day streak bonus", streakPt);
        Row($"{Math.Max(0, total - done)} task(s) missed today", missPt);
        Row($"Overdue carry ({overdueCapped} task(s), 3-day cap)", accrual);
        Row("Day total (floor −10)", dayTotal + accrual, always: true);
        panel.Children.Add(new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = ledger,
        });

        var reflection = new TextBox
        {
            PlaceholderText = "One line: what moved the needle today?",
        };
        panel.Children.Add(reflection);

        var dialog = new ContentDialog
        {
            Title = $"Day closed — {DateTime.Today:dddd dd.MM}",
            Content = panel,
            PrimaryButtonText = $"Close the day · {(dayTotal + accrual >= 0 ? "+" : "")}{dayTotal + accrual} pts",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            score.CreditDayScoreIfMissing(today);
            score.CreditOverdueAccrualIfMissing(today);
            if (reflection.Text.Trim() is { Length: > 0 } text)
                score.SaveReflection(today, text);
            var state = StateService.Load();
            state.LastReview = DateTime.Today.ToString("yyyy-MM-dd");
            StateService.Save(state);
            window.RefreshScore();
        }
    }
}

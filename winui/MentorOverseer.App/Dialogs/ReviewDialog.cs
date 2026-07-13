using System.Globalization;
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
        // Before reviewing, reconcile any stretch where the user finished and
        // stepped away before the day's end but was never asked about it —
        // ask "where have you been?" once here so that time is accounted for
        // instead of vanishing into an unlabelled idle gap.
        if (window.Tracker is { } tracker)
        {
            try
            {
                using var gapDb = new Database();
                if (tracker.PendingDayGap(gapDb) is { } gap)
                {
                    await IdleReturnDialog.ShowAsync(window, gap.Minutes, gap.Start);
                    // The whole gap is now written to the diary (even a skipped
                    // dialog logs it as idle) — stop the poll loop re-asking.
                    tracker.MarkAccountedThrough(gap.Start.AddMinutes(gap.Minutes));
                }
            }
            catch (Exception ex) { Log.Error("ReviewDialog.PendingDayGap", ex); }
        }

        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var (total, done) = score.DayTaskCounts(today);
        var (onMin, offMin) = score.DayDiaryMinutes(today);
        var streak = score.CurrentStreak();

        var taskPt = done * ConfigService.ScoringRate("task_completed", 10);
        var multiTaskPt = Math.Max(0, done - 1) * ConfigService.ScoringRate("multi_task_bonus_per_extra_task", 3);
        var onPt = (int)(onMin / 60.0 * ConfigService.ScoringRate("on_plan_hour", 3));
        var offPt = (int)(offMin / 60.0 * ConfigService.ScoringRate("off_plan_hour", -2));
        var missPt = Math.Max(0, total - done) * ConfigService.ScoringRate("task_overdue_penalty", -5);
        var streakPt = streak * ConfigService.ScoringRate("streak_bonus_per_day", 5);
        var rawTotal = taskPt + multiTaskPt + onPt + offPt + missPt + streakPt;
        var dayTotal = Math.Max(rawTotal, ScoreService.DailyFloor);
        var floored = dayTotal != rawTotal;

        var overdueCapped = score.OverdueAsOf(today)
            .Count(x => x.DaysOverdue <= ScoreService.OverdueAccrualCapDays);
        var accrual = overdueCapped * ConfigService.ScoringRate("task_overdue_penalty", -5);

        // Only ever non-zero on a Monday, when last week (now fully closed)
        // recovered from the week before it closing negative.
        var comeback = score.ComputeWeeklyComeback(today);

        var panel = new StackPanel { Spacing = 12, MinWidth = 460 };

        // The EOD "designed ending" the dialog never actually had: every day
        // got identical neutral framing regardless of how it went. A day
        // that clears every task, or scores the same "great day" bar Reports
        // uses (>=20), earns a line that says so instead of just numbers —
        // a completed comeback week takes priority, being the rarer event.
        var finalTotal = dayTotal + accrual + comeback;
        var perfect = total > 0 && done == total;
        if (comeback > 0)
            panel.Children.Add(new TextBlock
            {
                Text = "🎉 Comeback week — back in the green after a losing one.",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            });
        else if (perfect || finalTotal >= 20)
            panel.Children.Add(new TextBlock
            {
                Text = perfect
                    ? "🎉 Perfect day — every task done."
                    : "🎉 Strong day — well above the line.",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            });

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
        Row("Multi-task bonus (more than 1 done today)", multiTaskPt);
        Row("On-plan focus", onPt);
        Row($"{streak}-day streak bonus", streakPt);
        Row($"{Math.Max(0, total - done)} task(s) missed today", missPt);
        Row($"Overdue carry ({overdueCapped} task(s), 3-day cap)", accrual);
        Row("Weekly comeback bonus (last week recovered)", comeback);
        Row(floored ? "Day total — floored at −10, it can't get worse" : "Day total",
            finalTotal, always: true);
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

        // "Close the day" locks the daily_score ledger row — before the
        // configured end-of-day this is a preview only, otherwise an early
        // click would freeze the score and later work would never count.
        var beforeEod = DateTime.Now.TimeOfDay < MainWindow.EodTime();
        if (beforeEod)
            panel.Children.Add(new TextBlock
            {
                Text = $"Preview — the day can be closed after " +
                       $"{MainWindow.EodTime():hh\\:mm}, so work until then still counts.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });

        var dialog = new ContentDialog
        {
            Title = $"{DateTime.Today.ToString("dddd dd.MM", CultureInfo.InvariantCulture)} — day review",
            Content = panel,
            PrimaryButtonText = $"Close the day · {(finalTotal >= 0 ? "+" : "")}{finalTotal} pts",
            CloseButtonText = beforeEod ? "Close" : "Later",
            IsPrimaryButtonEnabled = !beforeEod,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await DialogGate.ShowAsync(dialog) == ContentDialogResult.Primary)
        {
            score.CreditDayScoreIfMissing(today);
            score.CreditOverdueAccrualIfMissing(today);
            score.CreditWeeklyComebackIfMissing(today);
            if (reflection.Text.Trim() is { Length: > 0 } text)
                score.SaveReflection(today, text);
            var state = StateService.Load();
            state.LastReview = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            StateService.Save(state);
            window.RefreshScore();
        }
    }
}

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
    // Guards MainWindow's per-minute EOD watcher from re-offering the review
    // every tick once it's actually been shown today — "Later" means later
    // by choice, so this shouldn't nag every minute for the rest of the day.
    // Deliberately separate from StateService.LastReview (only set once the
    // day is actually closed via "Close the day") and only set once the
    // dialog has actually opened, not merely offered — mirrors
    // KickoffDialog's _toastSentOn/MarkShownToday split, for the same
    // reason: a missed toast must not silently burn the day's one review
    // offer for the rest of the day.
    private static string? _offeredOn;

    // Throttles the fallback toast itself to once per day while the window
    // stays hidden, same rationale as KickoffDialog._toastSentOn.
    private static string? _toastSentOn;

    private static bool ShouldOffer()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_offeredOn == today) return false;
        // ">=" via negated "<", not "==": an exact-minute match on a
        // drifting 1-minute timer can skip the minute and never offer the
        // review that day.
        if (DateTime.Now.TimeOfDay < MainWindow.EodTime()) return false;
        return StateService.Load().LastReview != today;
    }

    /// <summary>
    /// Background-watcher entry point: shows the interactive dialog directly
    /// when the window is actually on screen, otherwise raises a toast so the
    /// prompt still reaches the user while the app sits in the tray — opening
    /// this dialog unconditionally (as the EOD watcher used to) risked it
    /// rendering inside a hidden window and then never resolving, which
    /// would wedge every other dialog in the app behind it via DialogGate's
    /// single process-wide queue.
    /// </summary>
    public static async Task Trigger(MainWindow window)
    {
        if (!ShouldOffer()) return;
        if (window.IsOnScreen())
        {
            window.Activate();
            await ShowAsync(window);
            return;
        }
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_toastSentOn == today) return;
        _toastSentOn = today;
        ToastNotifier.Show("Day review ready.",
            "See how today went — click to close out the day.",
            (ToastArgs.Action, ToastArgs.Review));
    }

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
                    await IdleReturnDialog.ShowAsync(window, gap.Minutes, gap.Start,
                        leadIn: "One gap to fill in before today's review —");
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

        // Single source of truth for the formula — see ComputeDayScore's
        // doc comment (2026-07-09 audit finding #4: this used to be
        // recomputed by hand here, independently of ScoreService.DayScore,
        // and could silently drift out of sync with it).
        var breakdown = score.ComputeDayScore(done, total, onMin, offMin, streak);
        var taskPt = breakdown.TaskPoints;
        var multiTaskPt = breakdown.MultiTaskBonus;
        var onPt = breakdown.OnPlanPoints;
        var offPt = breakdown.OffPlanPoints;
        var missPt = breakdown.MissedPoints;
        var streakPt = breakdown.StreakBonus;
        var dayTotal = breakdown.FlooredTotal;
        var floored = dayTotal != breakdown.RawTotal;

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
        // uses, earns a line that says so instead of just numbers — a
        // completed comeback week takes priority, being the rarer event.
        var finalTotal = dayTotal + accrual + comeback;
        var perfect = total > 0 && done == total;
        if (comeback > 0)
            panel.Children.Add(new TextBlock
            {
                Text = "🎉 Comeback week — back in the green after a losing one.",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            });
        else if (perfect || finalTotal >= ScoreService.GreatDayThreshold)
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

        var result = await DialogGate.ShowAsync(dialog);
        _offeredOn = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (result == ContentDialogResult.Primary)
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

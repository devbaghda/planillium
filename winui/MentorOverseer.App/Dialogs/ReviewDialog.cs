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
    // Guards against two review dialogs queuing back-to-back through
    // DialogGate — the per-minute EOD watcher re-invokes Trigger every tick
    // while the review's own dialog is still open (round-3 audit finding:
    // unlike KickoffDialog, ReviewDialog had no equivalent guard, so a
    // review left open past one tick would have a second one pop up the
    // instant the first was dismissed). Checked in ShouldOffer() so the
    // watcher doesn't even try while showing, and enforced again at the top
    // of ShowAsync itself since the manual "Review" button on Today
    // (TodayPage.xaml.cs) calls ShowAsync directly, bypassing ShouldOffer.
    private static bool _showing;

    // Guards MainWindow's per-minute EOD watcher from re-offering the review
    // every tick once it's actually been shown today — "Later" means later
    // by choice, so this shouldn't nag every minute for the rest of the day.
    // Deliberately separate from StateService.LastReview (only set once the
    // day is actually closed via "Close the day") and only set once the
    // dialog has actually opened, not merely offered — mirrors
    // KickoffDialog's _toastSentOn/MarkShownToday split, for the same
    // reason: a missed toast must not silently burn the day's one review
    // offer for the rest of the day.
    //
    // Set only from Trigger() (the automatic watcher path), never from
    // ShowCore itself — 2026-07-15 bug: it used to be set at the end of
    // ShowCore unconditionally, so TodayPage's manual "Evening review"
    // button (a same-day progress preview, ShowAsync called directly,
    // bypassing Trigger/ShouldOffer) also marked the day as "offered." One
    // daytime glance at that button silently disabled both the automatic
    // pop-up and its toast fallback for the rest of the day, with no error
    // and no visible sign anything was suppressed — the user had to notice
    // the day never closed and do it manually.
    private static string? _offeredOn;

    // Throttles the fallback toast itself to once per day while the window
    // stays hidden, same rationale as KickoffDialog._toastSentOn.
    private static string? _toastSentOn;

    private static bool ShouldOffer()
    {
        if (_showing) return false;
        var today = DateTime.Today.ToIsoDate();
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
    public static Task Trigger(MainWindow window)
    {
        if (!ShouldOffer()) return Task.CompletedTask;
        var today = DateTime.Today.ToIsoDate();
        // _offeredOn is set here, not inside ShowCore, so it only ever
        // reflects the automatic watcher having actually put the dialog in
        // front of the user — TodayPage's manual "Evening review" button
        // calls ShowAsync directly (bypassing Trigger/ShouldOffer) so a
        // daytime progress-check glance can't burn the day's one automatic
        // offer. Before this fix, opening that button even once earlier in
        // the day silently disabled both the EOD pop-up and its toast
        // fallback for the rest of the day, since ShowCore set _offeredOn on
        // every close, not just the automatic one.
        return PromptRouter.ShowOrToast(window,
            async () => { await ShowAsync(window); _offeredOn = today; },
            () => _toastSentOn == today, () => _toastSentOn = today,
            "Day review ready.", "See how today went — click to close out the day.",
            ToastArgs.Review, (ToastArgs.Action, ToastArgs.Review));
    }

    public static async Task ShowAsync(MainWindow window)
    {
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

    /// <summary>
    /// The four jobs this used to do in one 187-line block — reconciling a
    /// pending unaccounted-time gap, computing the day's numbers, building the
    /// dialog's UI, and persisting the result — are now each their own named
    /// step, the same shape round 4 split ActivityTracker.PollOnce into
    /// (round-5 audit finding #17: this had grown into the largest, most
    /// tangled function in the app, on the highest-stakes daily action).
    /// </summary>
    private static async Task ShowCore(MainWindow window)
    {
        await ReconcilePendingGap(window);

        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);

        var stats = ComputeReviewStats(score);
        var (panel, reflection) = BuildReviewPanel(stats);

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
            PrimaryButtonText = $"Close the day · {(stats.FinalTotal >= 0 ? "+" : "")}{stats.FinalTotal} pts",
            CloseButtonText = beforeEod ? "Close" : "Later",
            IsPrimaryButtonEnabled = !beforeEod,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await DialogGate.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary && !PersistReview(db, score, stats.Today, reflection, window))
            // The dialog itself is already gone by this point (ShowAsync just
            // returned), so a page-level error bar isn't reachable from here —
            // toast is the same fallback the timed prompts already use for
            // reaching the user outside of an open dialog.
            Services.ToastNotifier.Show("Couldn't close the day",
                "Something went wrong saving today's review — nothing was recorded. Open “Evening review” again to retry.",
                tag: null);
    }

    /// <summary>
    /// Before reviewing, reconcile any stretch where the user finished and
    /// stepped away before the day's end but was never asked about it —
    /// ask "where have you been?" once here so that time is accounted for
    /// instead of vanishing into an unlabelled idle gap.
    /// </summary>
    private static async Task ReconcilePendingGap(MainWindow window)
    {
        if (window.Tracker is not { } tracker) return;
        try
        {
            using var gapDb = new Database();
            if (tracker.PendingDayGap(gapDb) is { } gap)
            {
                await IdleReturnDialog.ShowAsync(window, gap.Minutes, gap.Start,
                    leadIn: "Some unaccounted time to fill in before today's review —");
                // The whole gap is now written to the diary (even a skipped
                // dialog logs it as idle) — stop the poll loop re-asking.
                tracker.MarkAccountedThrough(gap.Start.AddMinutes(gap.Minutes));
            }
        }
        catch (Exception ex) { Log.Error("ReviewDialog.PendingDayGap", ex); }
    }

    /// <summary>Every number the review panel and the persist step both need —
    /// computed once so the two can't drift out of sync with each other.</summary>
    private sealed record ReviewStats(
        DateOnly Today, int Done, int Total, int OnMin, int OffMin, int Streak,
        DayScoreBreakdown Breakdown, bool Floored, int OverdueCapped, int Accrual,
        int Comeback, int FinalTotal, bool Perfect);

    private static ReviewStats ComputeReviewStats(ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var (total, done) = score.DayTaskCounts(today);
        var (onMin, offMin) = score.DayDiaryMinutes(today);
        var streak = score.CurrentStreak();

        // Single source of truth for the formula — see ComputeDayScore's
        // doc comment (2026-07-09 audit finding #4: this used to be
        // recomputed by hand here, independently of ScoreService.DayScore,
        // and could silently drift out of sync with it).
        var breakdown = score.ComputeDayScore(done, total, onMin, offMin, streak);
        var dayTotal = breakdown.FlooredTotal;
        var floored = dayTotal != breakdown.RawTotal;

        var overdueCapped = score.OverdueAsOf(today)
            .Count(x => x.DaysOverdue <= ScoreService.OverdueAccrualCapDays);
        var accrual = overdueCapped * ConfigService.ScoringRate("task_overdue_penalty", -5);

        // Only ever non-zero on a Monday, when last week (now fully closed)
        // recovered from the week before it closing negative.
        var comeback = score.ComputeWeeklyComeback(today);

        var finalTotal = dayTotal + accrual + comeback;
        var perfect = total > 0 && done == total;

        return new ReviewStats(today, done, total, onMin, offMin, streak, breakdown,
            floored, overdueCapped, accrual, comeback, finalTotal, perfect);
    }

    private static (StackPanel Panel, TextBox Reflection) BuildReviewPanel(ReviewStats s)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 460 };

        // The EOD "designed ending" the dialog never actually had: every day
        // got identical neutral framing regardless of how it went. A day
        // that clears every task, or scores the same "great day" bar Reports
        // uses, earns a line that says so instead of just numbers — a
        // completed comeback week takes priority, being the rarer event.
        if (s.Comeback > 0)
            panel.Children.Add(new TextBlock
            {
                Text = "🎉 Comeback week — back in the green after a losing one.",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            });
        else if (s.Perfect || s.FinalTotal >= ScoreService.GreatDayThreshold)
            panel.Children.Add(new TextBlock
            {
                Text = s.Perfect
                    ? "🎉 Perfect day — every task done."
                    : "🎉 Strong day — well above the line.",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            });

        var ring = new ProgressRing
        {
            IsIndeterminate = false,
            Value = s.Total > 0 ? s.Done * 100.0 / s.Total : 0,
            Width = 72, Height = 72,
        };
        var ringRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 18 };
        ringRow.Children.Add(ring);
        var ringText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        ringText.Children.Add(new TextBlock
        {
            Text = $"{s.Done}/{s.Total} tasks done",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        ringText.Children.Add(new TextBlock
        {
            Text = $"{s.OnMin / 60}h {s.OnMin % 60:00}m on-plan · {s.OffMin}m off-plan",
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
        Row($"{s.Done} task(s) completed", s.Breakdown.TaskPoints);
        Row("Multi-task bonus (more than 1 done today)", s.Breakdown.MultiTaskBonus);
        Row("On-plan focus", s.Breakdown.OnPlanPoints);
        Row($"{s.Streak}-day streak bonus", s.Breakdown.StreakBonus);
        Row($"{Math.Max(0, s.Total - s.Done)} task(s) missed today", s.Breakdown.MissedPoints);
        Row($"Overdue carry ({s.OverdueCapped} task(s), 3-day cap)", s.Accrual);
        Row("Weekly comeback bonus (last week recovered)", s.Comeback);
        Row(s.Floored ? "Day total — floored at −10, it can't get worse" : "Day total",
            s.FinalTotal, always: true);
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

        return (panel, reflection);
    }

    /// <summary>
    /// The app's own highest-stakes daily action — closing the day — had
    /// neither a transaction around its four writes nor any failure handling
    /// at all until 2026-07-14 round-6 audit findings #3: an interruption
    /// partway through (day score written, overdue accrual not) could leave
    /// the score ledger half-updated with the dialog closing as if nothing
    /// was wrong. Now one all-or-nothing write (same mechanism as every
    /// other multi-step save in the app since round 5) with the caller
    /// deciding how to tell the user if it fails.
    /// </summary>
    private static bool PersistReview(Database db, ScoreService score, DateOnly today, TextBox reflection,
        MainWindow window)
    {
        try
        {
            db.RunInTransaction(() =>
            {
                score.CreditDayScoreIfMissing(today);
                score.CreditOverdueAccrualIfMissing(today);
                score.CreditWeeklyComebackIfMissing(today);
                if (reflection.Text.Trim() is { Length: > 0 } text)
                    score.SaveReflection(today, text);
            });
        }
        catch (Exception ex)
        {
            Log.Error("ReviewDialog.PersistReview", ex);
            return false;
        }
        var state = StateService.Load();
        state.LastReview = DateTime.Today.ToIsoDate();
        StateService.Save(state);
        window.RefreshScore();
        return true;
    }
}

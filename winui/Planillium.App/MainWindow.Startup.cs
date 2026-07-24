using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Dialogs;
using Planillium.App.Services;

namespace Planillium.App;

// Startup catch-up, the EOD/kickoff watcher timers, and the sidebar's
// score + plan-drift refresh — see MainWindow.xaml.cs for the file split.
public sealed partial class MainWindow
{
    /// <summary>
    /// CatchUpScores (up to 7 days of scoring math) and PruneOldDiary (a
    /// rollup INSERT + DELETE) used to run synchronously on the constructor
    /// before the window ever appeared — real, if usually small, delay
    /// added directly to how long the app takes to become visible on
    /// launch (2026-07-09 audit finding #7). Both already open their own
    /// Database/ScoreService connections (the same per-call-connection
    /// pattern ActivityTracker's background poll already uses), so running
    /// them off the UI thread is safe. RefreshScore runs again afterward in
    /// case catch-up added score for a missed day.
    /// </summary>
    private void RunStartupCatchUp()
    {
        _ = Task.Run(() =>
        {
            // CatchUpScores/PruneOldDiary already guard their own bodies; this wraps the
            // whole lambda too, for the same reason every other fire-and-forget call site
            // in this app does — an unobserved exception here (e.g. from TryEnqueue itself)
            // would otherwise surface nowhere (2026-07-23 audit finding #20).
            try
            {
                CatchUpScores();
                PruneOldDiary();
                _dq.TryEnqueue(RefreshScore);
            }
            catch (Exception ex)
            {
                Log.Error("RunStartupCatchUp", ex);
            }
        });
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
        catch (Exception ex)
        {
            Log.Error("CatchUpScores (db likely locked — next launch retries)", ex);
        }
    }

    private static void PruneOldDiary()
    {
        try
        {
            using var db = new Database();
            // The parameter default is only a fallback — the actual
            // retention window is user-configurable (Settings, 2026-07-09
            // audit finding #34).
            db.PruneAndRollupDiary(ConfigService.DiaryRetentionDays());
        }
        catch (Exception ex)
        {
            Log.Error("PruneOldDiary", ex);
        }
    }

    // Both watchers below poll on the same cadence — one named constant instead of two
    // independently-typed TimeSpan.FromMinutes(1) literals, so a future tuning change
    // can't update one and miss the other (2026-07-18 audit finding R10-11).
    private static readonly TimeSpan WatcherPollInterval = TimeSpan.FromMinutes(1);

    // System.Threading.Timer (not DispatcherQueueTimer) for all three watchers below
    // (2026-07-22 fix). DispatcherQueueTimer.Tick was confirmed to silently never fire
    // for the late-day-task-reminder watcher: timer.IsRunning read back True immediately
    // after Start(), yet diagnostic Log.Info calls placed as the literal first statement
    // of both the setup method and the Tick handler showed zero ticks across 60+ expected
    // intervals over more than an hour of live testing. This is very likely also the
    // unexplained root cause of the 2026-07-20 "end-of-day review never appeared" report,
    // which was never conclusively diagnosed at the time — EOD/Kickoff shared the exact
    // same _dq.CreateTimer() pattern, so switching all three at once rather than just the
    // one under active investigation. System.Threading.Timer is a lower-level, non-WinRT
    // primitive that reliably fires on a thread-pool thread; its callback marshals onto
    // the UI thread via _dq.TryEnqueue, same as every other cross-thread callback in this
    // app. Each MUST be stored in a field, not a local variable — unlike DispatcherQueueTimer
    // (kept alive by the DispatcherQueue itself), a System.Threading.Timer with no other
    // reference is eligible for GC at any time, which would silently stop it from firing.
    private System.Threading.Timer? _eodTimer;
    private System.Threading.Timer? _kickoffTimer;
    private System.Threading.Timer? _lateDayReminderTimer;
    private System.Threading.Timer? _diaryPruneTimer;

    // Guards CheckDiaryPrune the same way _lateDayReminderShownDate guards its own
    // once-a-day check below.
    private DateOnly? _diaryPrunedDate;

    /// <summary>At the configured end-of-day time, offer the evening review once.</summary>
    private void StartEodWatcher()
    {
        _eodTimer = new System.Threading.Timer(_ =>
            _dq.TryEnqueue(async () =>
            {
                try { await ReviewDialog.Trigger(this); }
                catch (Exception ex) { Log.Error("StartEodWatcher.Tick", ex); }
            }), null, WatcherPollInterval, WatcherPollInterval);
    }

    /// <summary>At the configured start-of-day time, show the morning kickoff
    /// once — regardless of which page happens to be open. Today page's own
    /// Loaded check still covers "opened after start time, before this timer's
    /// next tick"; KickoffDialog's _showing guard keeps the two from double-showing.</summary>
    private void StartKickoffWatcher()
    {
        _kickoffTimer = new System.Threading.Timer(_ =>
            _dq.TryEnqueue(async () =>
            {
                try { await KickoffDialog.Trigger(this); }
                catch (Exception ex) { Log.Error("StartKickoffWatcher.Tick", ex); }
            }), null, WatcherPollInterval, WatcherPollInterval);
    }

    // Set once per calendar day the moment the reminder below actually fires (or is
    // decided not to, because nothing's pending) — the guard that keeps it from
    // re-notifying every minute for the rest of the window before day-end.
    private DateOnly? _lateDayReminderShownDate;

    /// <summary>How long before the configured end-of-day time this should warn about
    /// still-open tasks — user-configurable (Settings), 2 hours by default (2026-07-20
    /// request).</summary>
    public static TimeSpan LateDayReminderLead()
    {
        var hours = 2.0;
        if (ConfigService.Root.TryGetProperty("late_day_task_reminder_hours", out var v) &&
            v.TryGetDouble(out var h) && h > 0) hours = h;
        return TimeSpan.FromHours(hours);
    }

    /// <summary>Once per day, once the configured lead time before end-of-day is
    /// reached, nudge if there's still incomplete work due today (todays' tasks not
    /// yet done, plus anything overdue) across every active plan — the same "todays'
    /// tasks" definition TodayPage.BuildPlanView uses, so this can't disagree with
    /// what the Today page itself shows as pending. A day where every relevant plan
    /// is excluded (Plan.IsExcluded — the same recurring-rest-day check the rest of
    /// the app already uses) naturally contributes nothing, so a full day off never
    /// nags. Uses a toast (not a dialog) since the app spends most of its life hidden
    /// in the tray, same reasoning as every other timed prompt in this file.</summary>
    private void StartLateDayTaskReminderWatcher()
    {
        _lateDayReminderTimer = new System.Threading.Timer(_ =>
            _dq.TryEnqueue(CheckLateDayTaskReminder), null, WatcherPollInterval, WatcherPollInterval);
    }

    /// <summary>Re-runs the retention prune once a day while the app keeps running —
    /// previously PruneOldDiary only ran once, at RunStartupCatchUp (app launch), so a
    /// long session with no restart could leave rows past the configured retention window
    /// sitting around until the next launch (2026-07-23 audit finding #18). Same
    /// once-a-minute poll + once-per-calendar-day guard pattern as the watchers above.</summary>
    private void StartDiaryPruneWatcher()
    {
        _diaryPruneTimer = new System.Threading.Timer(_ =>
            _dq.TryEnqueue(CheckDiaryPrune), null, WatcherPollInterval, WatcherPollInterval);
    }

    private void CheckDiaryPrune()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_diaryPrunedDate == today) return;
        _diaryPrunedDate = today;
        PruneOldDiary();
    }

    private void CheckLateDayTaskReminder()
    {
        // Kept as a cheap sanity check after the 2026-07-22 DispatcherQueueTimer ->
        // System.Threading.Timer swap (see StartLateDayTaskReminderWatcher) — this line
        // never once logged under the old timer despite it reporting IsRunning=true, so
        // seeing it fire now on the new mechanism is the actual proof the fix worked.
        Log.Info("CheckLateDayTaskReminder: tick fired");

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_lateDayReminderShownDate == today) return;

        var now = DateTime.Now;
        var dayEnd = DateTime.Today + EodTime();
        var windowStart = dayEnd - LateDayReminderLead();
        if (now < windowStart || now >= dayEnd) return;

        try
        {
            var plans = PlanStore.LoadActivePlans();
            var pending = 0;
            if (plans.Count > 0)
            {
                using var db = new Database();
                var completions = db.LoadCompletions();
                foreach (var plan in plans)
                {
                    if (plan.IsExcluded(today)) continue;
                    var tasks = PlanStore.TasksFor(plan, db, completions);
                    var planDay = plan.PlanDay;
                    pending += tasks.Count(t => t.Overdue || (t.AssignedDay == planDay && !t.Completed));
                }
            }
            _lateDayReminderShownDate = today;
            // Diagnostic (2026-07-21 request): a report that this never fires despite
            // genuinely open tasks, with the log otherwise silent either way — this one
            // line pins whether the check is even being reached and what it computed,
            // same "instrument, don't guess" approach as DialogGate/ReviewDialog's
            // existing 2026-07-20 diagnostics.
            Log.Info($"CheckLateDayTaskReminder: plans={plans.Count} pending={pending} " +
                     $"now={now:HH:mm:ss} windowStart={windowStart:HH:mm} dayEnd={dayEnd:HH:mm}");
            if (pending == 0) return;

            var remaining = dayEnd - now;
            var timeText = remaining.TotalMinutes >= 90
                ? $"{remaining.TotalHours:0.#}h" : $"{Math.Max(1, (int)remaining.TotalMinutes)}m";
            ToastNotifier.Show("Day's winding down",
                $"{timeText} left today and {pending} task{(pending == 1 ? "" : "s")} still open.",
                tag: "late-day-reminder");
        }
        catch (Exception ex)
        {
            Log.Error("CheckLateDayTaskReminder", ex);
        }
    }

    public static TimeSpan EodTime()
    {
        var eod = "20:00";
        if (ConfigService.Root.TryGetProperty("end_of_day_summary_time", out var v) &&
            v.GetString() is { Length: > 0 } s) eod = s;
        // InvariantCulture: same reasoning as ConfigService.WorkStartTime — this is always
        // a fixed HH:mm string, reading it back with the current culture could silently
        // fall back to the default on a locale where ':' isn't the time separator
        // (2026-07-18 audit finding R11-03).
        return TimeSpan.TryParse(eod, CultureInfo.InvariantCulture, out var t) ? t : new TimeSpan(20, 0, 0);
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
        RefreshPlanDrift();
    }

    /// <summary>
    /// Short sidebar status block per active plan, mirroring the Plans page's
    /// own drift readout (originally-due date vs. where the plan will now
    /// actually finish, from reschedules/day-offs pushing it later or early
    /// finishes pulling it earlier). Unlike the Plans page card, every active
    /// plan gets a block here — on-track and ahead-of-plan show green so a
    /// glance at the sidebar confirms things are fine, not just flags when
    /// they aren't. Each block is a truncated plan name (regular sidebar text)
    /// over a colored status line (larger, since that's the part worth
    /// noticing at a glance) and a small "Finishes dd.MM.yyyy" line underneath
    /// (the same projected finish date the status line's drift is measured
    /// against, added 2026-07-15 so the date itself is visible without a trip
    /// to the Plans page). Called from the same places RefreshScore is,
    /// since every action that can move a plan's finish date (reschedule,
    /// replan-overdue, completing/pulling a task) already calls RefreshScore.
    /// </summary>
    private void RefreshPlanDrift()
    {
        PlanDriftPanel.Children.Clear();
        try
        {
            var plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            var completions = db.LoadCompletions();
            foreach (var plan in plans)
            {
                var tasks = PlanStore.TasksFor(plan, db, completions);
                var driftDays = plan.DriftDays(tasks);
                var status = driftDays switch
                {
                    > 0 => $"{driftDays}d late from plan",
                    < 0 => $"{-driftDays}d ahead of plan",
                    _ => "On track",
                };
                // Same ToDisplayDateNumeric formatting the Plans page card uses for
                // its own "Originally due" line, so the two readouts of the
                // same underlying date don't drift apart visually.
                var finishText = "Finishes " + plan.CurrentEndDate(tasks).ToDisplayDateNumeric();

                var nameBlock = new TextBlock
                {
                    Text = plan.Name,
                    FontSize = 11,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                ToolTipService.SetToolTip(nameBlock, plan.Name);

                var block = new StackPanel { Spacing = 1 };
                block.Children.Add(nameBlock);
                block.Children.Add(new TextBlock
                {
                    Text = status,
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources[
                        driftDays > 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"],
                });
                block.Children.Add(new TextBlock
                {
                    Text = finishText,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                });
                // Same subtle-fill chrome the activity pill above it uses, so
                // the footer reads as one deliberate widget stack rather than
                // bare text bolted under two chip-styled ones.
                PlanDriftPanel.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6, 10, 6),
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    Child = block,
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error("RefreshPlanDrift", ex);
        }
    }
}

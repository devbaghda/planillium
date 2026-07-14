using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

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
            CatchUpScores();
            PruneOldDiary();
            _dq.TryEnqueue(RefreshScore);
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

    /// <summary>At the configured end-of-day time, offer the evening review once.</summary>
    private void StartEodWatcher()
    {
        var timer = _dq.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += async (_, _) => await ReviewDialog.Trigger(this);
        timer.Start();
    }

    /// <summary>At the configured start-of-day time, show the morning kickoff
    /// once — regardless of which page happens to be open. Today page's own
    /// Loaded check still covers "opened after start time, before this timer's
    /// next tick"; KickoffDialog's _showing guard keeps the two from double-showing.</summary>
    private void StartKickoffWatcher()
    {
        var timer = _dq.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += async (_, _) => await KickoffDialog.Trigger(this);
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
    /// noticing at a glance). Called from the same places RefreshScore is,
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

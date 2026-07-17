using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Tests;

/// <summary>
/// Covers ScoreService's schedule-shifting operations — the one area of
/// this codebase that (a) encodes genuinely tricky invariants (keyed-record
/// reordering, completion-orphaning avoidance, day-off symmetry) and (b)
/// has demonstrably changed multiple times with no automated safety net,
/// including a real regression (an already-completed task's day getting
/// shifted, silently un-marking it) that shipped and had to be caught and
/// fixed after the fact. Both audit passes on 2026-07-09 named this the
/// single highest-leverage structural addition to the codebase — this file
/// is that addition, deliberately starting minimal rather than exhaustive.
/// </summary>
[Collection("TestRoot")]
public sealed class ScoreServiceSchedulingTests
{
    private static Plan MakePlan(string planId, int startDayOffset, params (int Day, string Text)[] tasks)
    {
        var phase = new Phase { Number = 1, Name = "Phase 1" };
        foreach (var (day, text) in tasks)
            phase.Tasks.Add(new PlanTask { Day = day, Text = text });
        return new Plan
        {
            Id = planId,
            Name = "Test Plan",
            StartDate = DateTime.Today.AddDays(startDayOffset).ToString("yyyy-MM-dd"),
            Phases = new List<Phase> { phase },
        };
    }

    [Fact]
    public void MoveTaskToToday_DoesNotUnmarkAlreadyCompletedTaskOnTargetDay()
    {
        // Regression test for the real bug this guards against: today (plan
        // day 1) has one already-completed task; pulling a later task
        // forward to today must not disturb that completed task's day —
        // doing so previously orphaned its task_completions row (keyed by
        // assigned day) and silently un-marked it.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0,
            (1, "Today's own task"), (3, "Future task to pull forward"));

        using var db = new Database();
        db.SaveCompletion(planId, 1, "Today's own task", true);

        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MoveTaskToToday(plan, "Future task to pull forward");

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        var todayTask = tasks.Single(t => t.Task.Text == "Today's own task");
        Assert.True(todayTask.Completed);
        Assert.Equal(1, todayTask.AssignedDay);

        var pulled = tasks.Single(t => t.Task.Text == "Future task to pull forward");
        Assert.Equal(1, pulled.AssignedDay);
    }

    [Fact]
    public void MoveTaskToToday_ClosesGapWhenSourceDayBecomesEmpty()
    {
        // Pulling day 3's only task to today (day 1) should compress day 4's
        // task back into day 3's now-empty slot, not leave a dead day
        // sitting in the middle of the plan.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0,
            (1, "Day 1 task"), (3, "Day 3 only task"), (4, "Day 4 task"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MoveTaskToToday(plan, "Day 3 only task");

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(1, tasks.Single(t => t.Task.Text == "Day 3 only task").AssignedDay);
        Assert.Equal(3, tasks.Single(t => t.Task.Text == "Day 4 task").AssignedDay);
    }

    [Fact]
    public void RescheduleTask_ShiftsLaterPendingTasksForward()
    {
        // Deliberately the OPPOSITE behavior from MoveTaskToToday — see
        // ScoreService.RescheduleTask's doc comment and CONTEXT.md business
        // rule 7. Rescheduling an overdue task onto a day that already has
        // its own task must push that day (and later ones) forward by one,
        // preserving one-task-per-day, rather than doubling up. This test
        // exists specifically so a future "fix" that makes RescheduleTask
        // match MoveTaskToToday (as one audit pass initially, incorrectly,
        // suggested) fails loudly instead of silently changing behavior.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: -5,
            (1, "Overdue task"), (6, "Existing day-6 task"), (7, "Existing day-7 task"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.RescheduleTask(plan, "Overdue task", originalDay: 1, newAssignedDay: 6);

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(6, tasks.Single(t => t.Task.Text == "Overdue task").AssignedDay);
        Assert.Equal(7, tasks.Single(t => t.Task.Text == "Existing day-6 task").AssignedDay);
        Assert.Equal(8, tasks.Single(t => t.Task.Text == "Existing day-7 task").AssignedDay);
    }

    [Fact]
    public void ReplanOverdueTo_CascadesShiftsWhenTwoTasksTargetTheSameDay()
    {
        // Backs Dialogs/ReplanOverdueDialog (added 2026-07-14, replacing the
        // old automatic time-budget spread): the caller picks a day per
        // overdue task and each goes through RescheduleTask in order, so
        // two overdue tasks independently picked for the same day must
        // cascade correctly rather than colliding — the second assignment's
        // shift has to account for the first assignment having already
        // moved into that slot.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: -5,
            (1, "Overdue task 1"), (2, "Overdue task 2"), (6, "Existing day-6 task"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.ReplanOverdueTo(new List<(Plan, string, int, int)>
        {
            (plan, "Overdue task 1", 1, 6),
            (plan, "Overdue task 2", 2, 6),
        });

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(6, tasks.Single(t => t.Task.Text == "Overdue task 2").AssignedDay);
        Assert.Equal(7, tasks.Single(t => t.Task.Text == "Overdue task 1").AssignedDay);
        Assert.Equal(8, tasks.Single(t => t.Task.Text == "Existing day-6 task").AssignedDay);
    }

    [Fact]
    public void MoveTaskToToday_NeverShiftsCompletedTasksDuringCompaction()
    {
        // The compaction that closes a vacated day's gap only ever touches
        // tasks AFTER the vacated day (AssignedDay > oldDay) — so to
        // actually exercise the completed-task exclusion, the completed
        // task must sit in that range, not before it. Day 3 (only task)
        // gets pulled to today; day 4's completed task is in the shift
        // range and must stay put; day 5's still-pending task should still
        // compact normally, landing wherever day 4 is now vacant-adjacent.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0,
            (1, "Today task"), (3, "Day 3 only task"),
            (4, "Day 4 completed task"), (5, "Day 5 pending task"));

        using var db = new Database();
        db.SaveCompletion(planId, 4, "Day 4 completed task", true);

        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MoveTaskToToday(plan, "Day 3 only task");

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        var day4Task = tasks.Single(t => t.Task.Text == "Day 4 completed task");
        Assert.Equal(4, day4Task.AssignedDay);
        Assert.True(day4Task.Completed);

        // Still-pending work compacts normally around the untouched
        // completed task.
        Assert.Equal(4, tasks.Single(t => t.Task.Text == "Day 5 pending task").AssignedDay);
    }

    [Fact]
    public void RescheduleTask_SkipsOverDayMarkedOff()
    {
        // Regression test for the 2026-07-16 bug report: a naive "+1" shift
        // used to walk a task straight onto a day already marked off (making
        // it look occupied) while a plain working day further along was left
        // looking like an unmarked gap instead. Day 3 is marked off first
        // (pushing C/D to 4/5); rescheduling A onto day 2 must then shift
        // B/C/D forward while still hopping over day 3.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0,
            (1, "A"), (2, "B"), (3, "C"), (4, "D"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MarkDayOff(plan, 3);
        score.RescheduleTask(plan, "A", originalDay: 1, newAssignedDay: 2);

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(2, tasks.Single(t => t.Task.Text == "A").AssignedDay);
        Assert.Equal(4, tasks.Single(t => t.Task.Text == "B").AssignedDay);
        Assert.Equal(5, tasks.Single(t => t.Task.Text == "C").AssignedDay);
        Assert.Equal(6, tasks.Single(t => t.Task.Text == "D").AssignedDay);
        Assert.Contains(3, score.DaysOff(planId));
        Assert.DoesNotContain(3, tasks.Select(t => t.AssignedDay));
    }

    [Fact]
    public void MarkDayOff_SkipsOverAnAlreadyMarkedOffDay()
    {
        // Marking a second day off while an earlier one is still off must
        // hop the forward shift over the first day-off too, not land a task
        // on it.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0,
            (1, "T1"), (2, "T2"), (3, "T3"), (4, "T4"), (5, "T5"), (6, "T6"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MarkDayOff(plan, 3);   // T3..T6 shift to 4..7
        score.MarkDayOff(plan, 5);   // whatever now sits on/after day 5 shifts again, hopping day 3

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(1, tasks.Single(t => t.Task.Text == "T1").AssignedDay);
        Assert.Equal(2, tasks.Single(t => t.Task.Text == "T2").AssignedDay);
        Assert.Equal(4, tasks.Single(t => t.Task.Text == "T3").AssignedDay);
        Assert.Equal(6, tasks.Single(t => t.Task.Text == "T4").AssignedDay);
        Assert.Equal(7, tasks.Single(t => t.Task.Text == "T5").AssignedDay);
        Assert.Equal(8, tasks.Single(t => t.Task.Text == "T6").AssignedDay);
        Assert.Equal(new HashSet<int> { 3, 5 }, score.DaysOff(planId));
        Assert.DoesNotContain(3, tasks.Select(t => t.AssignedDay));
        Assert.DoesNotContain(5, tasks.Select(t => t.AssignedDay));
    }

    [Fact]
    public void UnmarkDayOff_MovesFollowingTaskBackToTheUnmarkedDay()
    {
        // Regression test for the 2026-07-16 bug report: undoing a day-off
        // should pull the task that rolled forward because of it back onto
        // the day being un-marked, restoring the original layout.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0, (1, "A"), (2, "B"), (3, "C"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MarkDayOff(plan, 2);
        score.UnmarkDayOff(plan, 2);

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(1, tasks.Single(t => t.Task.Text == "A").AssignedDay);
        Assert.Equal(2, tasks.Single(t => t.Task.Text == "B").AssignedDay);
        Assert.Equal(3, tasks.Single(t => t.Task.Text == "C").AssignedDay);
        Assert.Empty(score.DaysOff(planId));
    }

    [Fact]
    public void UnmarkDayOff_PreservesAnotherStillMarkedOffDay()
    {
        // Two days marked off (3, then 5); un-marking the earlier one (3)
        // must compact everything below the still-off day (5) back into
        // place while leaving day 5 itself untouched — the backward
        // counterpart of MarkDayOff_SkipsOverAnAlreadyMarkedOffDay.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, startDayOffset: 0,
            (1, "T1"), (2, "T2"), (3, "T3"), (4, "T4"), (5, "T5"), (6, "T6"));

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        score.MarkDayOff(plan, 3);
        score.MarkDayOff(plan, 5);
        score.UnmarkDayOff(plan, 3);

        var completions = db.LoadCompletions();
        var tasks = PlanStore.TasksFor(plan, db, completions);

        Assert.Equal(1, tasks.Single(t => t.Task.Text == "T1").AssignedDay);
        Assert.Equal(2, tasks.Single(t => t.Task.Text == "T2").AssignedDay);
        Assert.Equal(3, tasks.Single(t => t.Task.Text == "T3").AssignedDay);
        Assert.Equal(4, tasks.Single(t => t.Task.Text == "T4").AssignedDay);
        Assert.Equal(6, tasks.Single(t => t.Task.Text == "T5").AssignedDay);
        Assert.Equal(7, tasks.Single(t => t.Task.Text == "T6").AssignedDay);
        Assert.Equal(new HashSet<int> { 5 }, score.DaysOff(planId));
        Assert.DoesNotContain(5, tasks.Select(t => t.AssignedDay));
    }
}

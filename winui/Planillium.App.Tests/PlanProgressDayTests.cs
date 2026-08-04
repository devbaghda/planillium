using Planillium.App.Models;

namespace Planillium.App.Tests;

/// <summary>
/// Plan.ProgressDay — the display-only "Day X of Y" counter (2026-08-04 request). It stops at
/// the earliest plan day still holding unfinished work, so missing day 10's task keeps the
/// header on day 10 the next morning instead of advancing with the calendar. Every scheduling
/// and scoring decision still uses the plain calendar PlanDay; nothing here touches that.
///
/// asOf is passed explicitly throughout: anchoring on DateTime.Today would make these tests
/// pass or fail depending on the date they're run (the same trap ScoreService.CurrentStreak's
/// optional anchor was added to close, 2026-07-18 audit finding R8-01).
/// </summary>
public sealed class PlanProgressDayTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    private static Plan MakePlan(int totalDays = 28, List<int>? excludedWeekdays = null) => new()
    {
        Id = "test",
        Name = "Test Plan",
        StartDate = "2026-01-01",
        TotalDays = totalDays,
        ExcludedWeekdays = excludedWeekdays ?? new List<int>(),
    };

    /// <summary>One task per plan day, days 1..count, completed exactly when the day is in
    /// <paramref name="completedDays"/> — the shape PlanStore.TasksFor produces.
    /// <paramref name="reschedule"/> maps an original day to the day it was moved to, standing
    /// in for a task_overrides row (AssignedDay is init-only, so it has to be set here).</summary>
    private static List<AssignedTask> Tasks(int count, int[]? completedDays = null,
        Dictionary<int, int>? reschedule = null) =>
        Enumerable.Range(1, count).Select(day => new AssignedTask
        {
            Task = new PlanTask { Day = day, Text = $"Task {day}" },
            OriginalDay = day,
            AssignedDay = reschedule is not null && reschedule.TryGetValue(day, out var moved) ? moved : day,
            Completed = completedDays?.Contains(day) ?? false,
        }).ToList();

    /// <summary>The reported case: day 10's task never got done, so day 11 still reads 10.</summary>
    [Fact]
    public void StallsOnTheEarliestUnfinishedDay()
    {
        var plan = MakePlan();
        var tasks = Tasks(28, completedDays: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        Assert.Equal(10, plan.ProgressDay(tasks, 28, Start.AddDays(9)));   // day 10 itself
        Assert.Equal(10, plan.ProgressDay(tasks, 28, Start.AddDays(10)));  // day 11 — still 10
        Assert.Equal(10, plan.ProgressDay(tasks, 28, Start.AddDays(14)));  // a week later — still 10
    }

    /// <summary>Working ahead doesn't release the counter — only closing the day behind it
    /// does. Finishing day 11's task while day 10's is still open keeps the header on 10.</summary>
    [Fact]
    public void WorkingAheadDoesNotAdvancePastAnUnfinishedDay()
    {
        var plan = MakePlan();
        var tasks = Tasks(28, completedDays: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11 });

        Assert.Equal(10, plan.ProgressDay(tasks, 28, Start.AddDays(10)));
    }

    /// <summary>Rescheduling the skipped task forward IS what releases the counter — the
    /// user's own stated workflow for deliberately skipping a day, and the reason stalling
    /// can't strand the counter permanently. Reschedule moves AssignedDay, and the counter
    /// follows.</summary>
    [Fact]
    public void ReschedulingTheOpenTaskForwardReleasesTheCounter()
    {
        var plan = MakePlan();
        var tasks = Tasks(28, completedDays: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            reschedule: new Dictionary<int, int> { [10] = 15 });

        Assert.Equal(11, plan.ProgressDay(tasks, 28, Start.AddDays(10)));
    }

    /// <summary>Nothing outstanding — the counter is just the calendar day.</summary>
    [Fact]
    public void MatchesTheCalendarDayWhenEverythingDueIsDone()
    {
        var plan = MakePlan();
        var tasks = Tasks(28, completedDays: Enumerable.Range(1, 10).ToArray());

        Assert.Equal(11, plan.ProgressDay(tasks, 28, Start.AddDays(10)));
    }

    /// <summary>An overrun plan: 28 days long, calendar day 30, days 27-28 still open. Reads
    /// "Day 27 of 28" rather than today's nonsensical "Day 30 of 28".</summary>
    [Fact]
    public void OverrunPlanReportsTheOpenDayNotTheCalendarDay()
    {
        var plan = MakePlan();
        var tasks = Tasks(28, completedDays: Enumerable.Range(1, 26).ToArray());

        Assert.Equal(27, plan.ProgressDay(tasks, 28, Start.AddDays(29)));
    }

    /// <summary>An overrun plan with nothing left open still can't read past its own length —
    /// the lastDay clamp, which is the only thing standing between a finished-but-late plan
    /// and a "Day 30 of 28" header.</summary>
    [Fact]
    public void NeverExceedsTheDisplayedPlanLength()
    {
        var plan = MakePlan();
        var tasks = Tasks(28, completedDays: Enumerable.Range(1, 28).ToArray());

        Assert.Equal(28, plan.ProgressDay(tasks, 28, Start.AddDays(29)));
    }

    /// <summary>Before the start date the caller renders a "Starts on…" countdown off this
    /// same value, so the zero/negative calendar day must pass straight through untouched.</summary>
    [Fact]
    public void PassesThroughTheNotStartedYetCountdown()
    {
        var plan = MakePlan();

        Assert.Equal(0, plan.ProgressDay(Tasks(28), 28, Start.AddDays(-1)));
        Assert.Equal(-2, plan.ProgressDay(Tasks(28), 28, Start.AddDays(-3)));
    }

    /// <summary>Excluded weekdays are already handled by PlanDayForDate, which the counter
    /// builds on — an excluded day doesn't advance the calendar day, so it can't advance this
    /// either. Plan excludes Saturday+Sunday; 2026-01-03/04 are the first weekend after the
    /// Thursday start, so the Monday after them is still plan day 3.</summary>
    [Fact]
    public void RespectsExcludedWeekdaysThroughPlanDayForDate()
    {
        var plan = MakePlan(excludedWeekdays: new List<int> { 0, 6 });  // Sunday, Saturday
        var tasks = Tasks(28, completedDays: new[] { 1, 2, 3 });

        var monday = new DateOnly(2026, 1, 5);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        Assert.Equal(3, plan.PlanDayForDate(monday));
        Assert.Equal(3, plan.ProgressDay(tasks, 28, monday));
    }

    /// <summary>A plan with no tasks at all (or one whose tasks are all still ahead) falls
    /// back to the calendar day rather than to zero.</summary>
    [Fact]
    public void EmptyTaskListFallsBackToTheCalendarDay()
    {
        var plan = MakePlan();

        Assert.Equal(5, plan.ProgressDay(new List<AssignedTask>(), 28, Start.AddDays(4)));
    }
}

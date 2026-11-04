using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// ScoreService.ManuallyMarkedDaysOff — the raw range query behind ReportData.PeriodStats'
/// DayOffs figure (see ReportPeriodStatsTests for the period-boundary behavior on top of it).
///
/// The one invariant worth pinning here isn't the arithmetic (that's a HashSet.Count) — it's the
/// *scope*: only plan_days_off rows count, never a plan's recurring weekday rest days, and a date
/// marked off in two plans still counts once. Both are easy to get backwards by reusing
/// AllPlansScoringExempt/ScoringExemptDates instead, which deliberately include the recurring
/// case for a different purpose.
/// </summary>
[Collection("TestRoot")]
public sealed class ManuallyMarkedDaysOffTests
{
    private static Plan MakePlan(string planId, int startDayOffset = 0, List<int>? excludedWeekdays = null)
    {
        var phase = new Phase { Number = 1, Name = "Phase 1" };
        phase.Tasks.Add(new PlanTask { Day = 1, Text = "Task A" });
        return new Plan
        {
            Id = planId,
            Name = "Test Plan",
            StartDate = DateTime.Today.AddDays(startDayOffset).ToString("yyyy-MM-dd"),
            Phases = new List<Phase> { phase },
            ExcludedWeekdays = excludedWeekdays ?? new List<int>(),
        };
    }

    /// <summary>A day off outside the queried range must not be counted — the boundary
    /// ReportData.PeriodStats actually depends on to stay accurate rather than just "everything
    /// ever marked off".</summary>
    [Fact]
    public void DayOffOutsideTheQueriedRangeIsExcluded()
    {
        var planId = "dayoff-range-" + Guid.NewGuid();
        var plan = MakePlan(planId);
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // No exclusions, so plan day 11 lands exactly 10 days after plan day 1 (today).
        plan.Phases[0].Tasks.Add(new PlanTask { Day = 11, Text = "Task B" });
        score.MarkDayOff(plan, 11);

        Assert.DoesNotContain(today.AddDays(10), score.ManuallyMarkedDaysOff(today, today));
        Assert.Contains(today.AddDays(10), score.ManuallyMarkedDaysOff(today, today.AddDays(10)));
    }

    /// <summary>The same calendar date marked off in two different plans is one day off lived,
    /// not two mark-off actions — ManuallyMarkedDaysOff answers the former.</summary>
    [Fact]
    public void SameCalendarDateMarkedOffInTwoPlansCountsOnce()
    {
        var planA = MakePlan("dayoff-dedup-a-" + Guid.NewGuid());
        var planB = MakePlan("dayoff-dedup-b-" + Guid.NewGuid());
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { planA, planB }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        score.MarkDayOff(planA, 1);
        score.MarkDayOff(planB, 1);

        Assert.Single(score.ManuallyMarkedDaysOff(today, today));
    }

    /// <summary>A plan's recurring weekday rest day (never explicitly marked off via
    /// MarkDayOff) must NOT appear here, even though it makes the plan scoring-exempt on that
    /// date — that's exactly the distinction ScoringExemptDates blurs and this method exists to
    /// separate out.</summary>
    [Fact]
    public void RecurringWeekdayExclusionAloneIsNotCountedAsManual()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var planId = "dayoff-recurring-" + Guid.NewGuid();
        var plan = MakePlan(planId, excludedWeekdays: new List<int> { (int)today.DayOfWeek });
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        // Never calls MarkDayOff — today is only "off" via the recurring weekly exclusion.
        Assert.True(plan.IsExcluded(today));
        Assert.Empty(score.ManuallyMarkedDaysOff(today, today));
    }
}

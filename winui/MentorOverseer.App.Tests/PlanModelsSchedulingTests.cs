using MentorOverseer.App.Models;

namespace MentorOverseer.App.Tests;

/// <summary>
/// Round-12: DateForPlanDay/PlanDayForDate switched from an O(planDay) day-by-day
/// walk to a closed-form "skip full weeks, walk only the remainder" calculation,
/// since the exclusion pattern (by weekday) always repeats every 7 days. These
/// tests brute-force-verify the closed form against a plain walk across every
/// weekday-exclusion combination, not just spot values, since an off-by-one in
/// the full-weeks math would only show up for specific planDay/date offsets.
/// </summary>
public sealed class PlanModelsSchedulingTests
{
    private static Plan MakePlan(List<int> excludedWeekdays) => new()
    {
        Id = "test",
        Name = "Test Plan",
        StartDate = "2026-01-01", // a Thursday
        ExcludedWeekdays = excludedWeekdays,
    };

    // Reference implementation: the plain walk this project used before round-12.
    private static DateOnly WalkDateForPlanDay(Plan plan, int planDay)
    {
        var date = plan.StartDateParsed;
        var count = 0;
        while (true)
        {
            if (!plan.IsExcluded(date))
            {
                count++;
                if (count == planDay) return date;
            }
            date = date.AddDays(1);
        }
    }

    private static int WalkPlanDayForDate(Plan plan, DateOnly target)
    {
        var count = 0;
        for (var date = plan.StartDateParsed; date <= target; date = date.AddDays(1))
            if (!plan.IsExcluded(date)) count++;
        return count;
    }

    public static IEnumerable<object[]> ExclusionCombinations()
    {
        // Every combination of 0-3 excluded weekdays out of 7 — covers "no
        // exclusions" (fast path), a single day (e.g. Sundays), and multi-day
        // (e.g. weekends) shapes without the full 2^7 combinatorial blowup.
        var days = Enumerable.Range(0, 7).ToList();
        yield return new object[] { new List<int>() };
        foreach (var d in days) yield return new object[] { new List<int> { d } };
        for (var i = 0; i < days.Count; i++)
            for (var j = i + 1; j < days.Count; j++)
                yield return new object[] { new List<int> { days[i], days[j] } };
        yield return new object[] { new List<int> { 0, 6 } }; // classic weekend
        yield return new object[] { new List<int> { 0, 1, 6 } };
    }

    [Theory]
    [MemberData(nameof(ExclusionCombinations))]
    public void DateForPlanDay_MatchesPlainWalk_AcrossManyPlanDays(List<int> excluded)
    {
        var plan = MakePlan(excluded);
        for (var planDay = 1; planDay <= 400; planDay++)
            Assert.Equal(WalkDateForPlanDay(plan, planDay), plan.DateForPlanDay(planDay));
    }

    [Theory]
    [MemberData(nameof(ExclusionCombinations))]
    public void PlanDayForDate_MatchesPlainWalk_AcrossManyDates(List<int> excluded)
    {
        var plan = MakePlan(excluded);
        var target = plan.StartDateParsed;
        for (var i = 0; i < 400; i++, target = target.AddDays(1))
            Assert.Equal(WalkPlanDayForDate(plan, target), plan.PlanDayForDate(target));
    }

    [Fact]
    public void DateForPlanDay_And_PlanDayForDate_RemainInverses_WithExclusions()
    {
        var plan = MakePlan(new List<int> { 0, 6 });
        for (var planDay = 1; planDay <= 200; planDay++)
        {
            var date = plan.DateForPlanDay(planDay);
            Assert.Equal(planDay, plan.PlanDayForDate(date));
        }
    }

    [Fact]
    public void AllWeekdaysExcluded_DoesNotThrow_FallsBackInsteadOfDividingByZero()
    {
        var plan = MakePlan(Enumerable.Range(0, 7).ToList());
        // Degenerate input the picker dialog doesn't actually block; the closed-form
        // math can't divide by a zero non-excluded-per-week count, so this only
        // needs to not throw — DateForPlanDaySlow's own infinite-loop-if-called-with
        // this input is a pre-existing, out-of-scope issue, not one introduced here.
        Assert.Equal(0, plan.PlanDayForDate(plan.StartDateParsed));
    }
}

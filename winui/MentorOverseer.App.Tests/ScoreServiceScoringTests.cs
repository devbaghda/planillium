using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Tests;

/// <summary>
/// Covers ScoreService.ComputeDayScore — the day-score formula three
/// separate UI surfaces (Reports, ReviewDialog, ReportExport) all read from
/// as the single source of truth — plus IsScoringExemptFor's day-off
/// exemption via DayTaskCounts. Companion to
/// ScoreServiceSchedulingTests, which covers schedule-shifting but never
/// touched the formula itself (round-2 audit finding #07, 2026-07-13):
/// the automated suite existed to protect this app's one area with a real
/// shipped regression history, but the actual scoring math had no test at
/// all until now. Rates use ConfigService's built-in fallback defaults
/// (10/3/3/-2/-5/5) since TestRootFixture points MENTOR_ROOT at an empty
/// temp folder with no config.json to override them.
/// </summary>
[Collection("TestRoot")]
public sealed class ScoreServiceScoringTests
{
    private static Plan MakePlan(string planId, params (int Day, string Text)[] tasks)
    {
        var phase = new Phase { Number = 1, Name = "Phase 1" };
        foreach (var (day, text) in tasks)
            phase.Tasks.Add(new PlanTask { Day = day, Text = text });
        return new Plan
        {
            Id = planId,
            Name = "Test Plan",
            StartDate = DateTime.Today.ToString("yyyy-MM-dd"),
            Phases = new List<Phase> { phase },
        };
    }

    [Fact]
    public void ComputeDayScore_MatchesTermByTermFormula()
    {
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        var b = score.ComputeDayScore(done: 3, total: 5, onMin: 120, offMin: 60, streak: 4);

        Assert.Equal(3 * 10, b.TaskPoints);
        Assert.Equal((3 - 1) * 3, b.MultiTaskBonus);
        Assert.Equal((int)(120 / 60.0 * 3), b.OnPlanPoints);
        Assert.Equal((int)(60 / 60.0 * -2), b.OffPlanPoints);
        Assert.Equal((5 - 3) * -5, b.MissedPoints);
        Assert.Equal(4 * 5, b.StreakBonus);

        var expectedRaw = b.TaskPoints + b.MultiTaskBonus + b.OnPlanPoints +
                           b.OffPlanPoints + b.MissedPoints + b.StreakBonus;
        Assert.Equal(expectedRaw, b.RawTotal);
        Assert.Equal(Math.Max(expectedRaw, ScoreService.DailyFloor), b.FlooredTotal);
    }

    [Fact]
    public void ComputeDayScore_FlooredTotal_NeverGoesBelowDailyFloor()
    {
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        // 10 missed tasks at the default -5 each is -50 raw — well past the
        // -10 floor. A bad day is a setback, not a spiral (DailyFloor's own
        // doc comment) — this is the test that actually exercises that.
        var b = score.ComputeDayScore(done: 0, total: 10, onMin: 0, offMin: 0, streak: 0);

        Assert.True(b.RawTotal < ScoreService.DailyFloor);
        Assert.Equal(ScoreService.DailyFloor, b.FlooredTotal);
    }

    [Fact]
    public void ComputeDayScore_MultiTaskBonus_OnlyAppliesBeyondFirstTask()
    {
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        var oneTask = score.ComputeDayScore(done: 1, total: 1, onMin: 0, offMin: 0, streak: 0);
        Assert.Equal(0, oneTask.MultiTaskBonus);

        var threeTasks = score.ComputeDayScore(done: 3, total: 3, onMin: 0, offMin: 0, streak: 0);
        Assert.Equal(2 * 3, threeTasks.MultiTaskBonus);
    }

    [Fact]
    public void GreatDayThreshold_BoundaryMatchesConstant()
    {
        // The three UI surfaces that celebrate a "great day" all compare
        // against this one constant instead of a hardcoded 20 (round-2
        // remediation, 2026-07-13) — this test is the tripwire if it's ever
        // retuned without updating what "great" actually requires.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        // streak_bonus_per_day default 5 * streak=4 = 20 exactly, with no
        // other term contributing — pure integer arithmetic, so no
        // truncation ambiguity about what "exactly at the threshold" means.
        var atThreshold = score.ComputeDayScore(done: 0, total: 0, onMin: 0, offMin: 0, streak: 4);
        Assert.Equal(ScoreService.GreatDayThreshold, atThreshold.FlooredTotal);

        var belowThreshold = score.ComputeDayScore(done: 0, total: 0, onMin: 0, offMin: 0, streak: 3);
        Assert.True(belowThreshold.FlooredTotal < ScoreService.GreatDayThreshold);
    }

    [Fact]
    public void DayTaskCounts_ExcludesDayThePlanRecurringlyExcludes()
    {
        // A plan that excludes today's weekday must contribute nothing to
        // DayTaskCounts for today — IsScoringExemptFor (renamed from
        // IsDayOffFor in the round-2 remediation) skipping the whole day,
        // not just declining to penalize it.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Today's task"));
        plan.ExcludedWeekdays.Add((int)DateTime.Today.DayOfWeek);

        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        var (total, done) = score.DayTaskCounts(DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(0, total);
        Assert.Equal(0, done);
    }
}

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

    [Fact]
    public void DayTaskCounts_StillCountsATaskMovedOntoAManuallyOffDay()
    {
        // 2026-07-17 fix: DayTaskCounts used to skip a plan's WHOLE day if it was
        // manually marked off, which meant a task deliberately moved onto that day
        // (e.g. via Move-to-today) was silently uncounted — contradicting "no points
        // on a day off, UNLESS I brought in and accomplished a task from a plan." A
        // manually-off day's day-number is real and unique (unlike a recurring
        // exclusion's, which is intentionally skipped to avoid double-counting), so
        // it must NOT be skipped here.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        using var db = new Database();
        var today = DateOnly.FromDateTime(DateTime.Today);

        using (var score = new ScoreService(new List<Plan> { plan }, db))
        {
            // Marks day 1 (today) off, which shifts "Task" to day 2 — then pulls it
            // straight back onto today, the same "brought it in" action a user would
            // take via the Schedule page's "Move to today" button.
            score.MarkDayOff(plan, plan.PlanDayForDate(today));
            score.MoveTaskToToday(plan, "Task");
        }
        // ScoreService caches completions at construction — SaveCompletion must happen
        // before the ScoreService that reads it is built, matching every real call site's
        // own ordering (construct fresh right before use).
        db.SaveCompletion(planId, plan.PlanDay, "Task", true);

        using var score2 = new ScoreService(new List<Plan> { plan }, db);
        var (total, done) = score2.DayTaskCounts(today);
        Assert.Equal(1, total);
        Assert.Equal(1, done);
    }

    [Fact]
    public void ComputeDayScore_IsExemptDay_SuppressesPassiveTermsButKeepsTaskCredit()
    {
        // 2026-07-17: on a day off, on/off-plan minutes, the missed-task penalty, and the
        // streak bonus should all be suppressed — but a task actually completed that day
        // still earns its own credit, the one explicit exception requested.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        var b = score.ComputeDayScore(done: 2, total: 3, onMin: 120, offMin: 60, streak: 4, isExemptDay: true);

        Assert.Equal(2 * 10, b.TaskPoints);
        Assert.Equal((2 - 1) * 3, b.MultiTaskBonus);
        Assert.Equal(0, b.OnPlanPoints);
        Assert.Equal(0, b.OffPlanPoints);
        Assert.Equal(0, b.MissedPoints);
        Assert.Equal(0, b.StreakBonus);
    }

    [Fact]
    public void AllPlansScoringExempt_RequiresEveryPlanOff_NotJustOne()
    {
        // Confirmed with the user 2026-07-17: a day off on one plan while another still has
        // real work due should still score normally — only "nothing expected of you
        // anywhere" suppresses the day's passive scoring.
        var planA = MakePlan("test-a-" + Guid.NewGuid(), (1, "Task A"));
        var planB = MakePlan("test-b-" + Guid.NewGuid(), (1, "Task B"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { planA, planB }, db);

        score.MarkDayOff(planA, planA.PlanDayForDate(today));
        Assert.False(score.AllPlansScoringExempt(today));

        score.MarkDayOff(planB, planB.PlanDayForDate(today));
        Assert.True(score.AllPlansScoringExempt(today));
    }

    [Fact]
    public void RecalculateDayScore_OverwritesAnAlreadyCreditedDay()
    {
        // 2026-07-17: editing a diary entry's category after its day was already scored
        // used to leave the stale figure in place forever (daily_score is once-per-date).
        // RecalculateDayScore must delete and recompute, not silently no-op like
        // CreditDayScoreIfMissing does for an already-credited date.
        var planId = "test-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = new Database();

        // ScoreService caches completions at construction — each SaveCompletion below
        // needs its own fresh instance afterward, matching every real call site's own
        // ordering (construct fresh right before use).
        db.SaveCompletion(planId, 1, "Task", true);
        int? firstScore;
        using (var score = new ScoreService(new List<Plan> { plan }, db))
        {
            firstScore = score.CreditDayScoreIfMissing(today);
            Assert.NotNull(firstScore);
            // Already credited — a second call is a no-op, confirming the baseline this
            // test is contrasting against.
            Assert.Null(score.CreditDayScoreIfMissing(today));
        }

        db.SaveCompletion(planId, 1, "Task", false);
        using var score2 = new ScoreService(new List<Plan> { plan }, db);
        var recalculated = score2.RecalculateDayScore(today);
        Assert.NotEqual(firstScore, recalculated);
    }

    [Fact]
    public void RecalculateDayScore_PreservesThatDaysOwnStreakBonus_NotTodays()
    {
        // 2026-07-18 audit finding R8-01: RecomputeDayScoreCore used to hardcode streak=0
        // for any date that wasn't literally today, so editing an old diary entry (which
        // routes through RecalculateDayScore) silently discarded a real streak bonus that
        // day had actually earned, with nothing to warn about it. CurrentStreak(asOf) must
        // be computed relative to the day being recalculated, not relative to today.
        var planId = "test-" + Guid.NewGuid();
        var twoDaysAgo = DateOnly.FromDateTime(DateTime.Today).AddDays(-2);
        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var plan = MakePlan(planId, (1, "Day1 Task"), (2, "Day2 Task"));
        plan.StartDate = twoDaysAgo.ToString("yyyy-MM-dd");
        using var db = new Database();

        // Complete day 1's task (lands on twoDaysAgo) so a real 1-day streak carries into
        // yesterday; leave day 2 (yesterday's own task) incomplete so recalculating
        // yesterday isn't itself a "perfect day" the streak formula would special-case.
        db.SaveCompletion(planId, 1, "Day1 Task", true);

        using var score = new ScoreService(new List<Plan> { plan }, db);
        var expectedStreak = score.CurrentStreak(yesterday);
        Assert.Equal(1, expectedStreak);

        var (total, done) = score.DayTaskCounts(yesterday);
        var (on, off) = score.DayDiaryMinutes(yesterday);
        var expected = score.ComputeDayScore(done, total, on, off, expectedStreak, isExemptDay: false).FlooredTotal;

        var recalculated = score.RecalculateDayScore(yesterday);
        Assert.Equal(expected, recalculated);

        // Before the fix this would have been RecalculateDayScore(yesterday) computed with
        // streak forced to 0 — confirm the streak term is actually the nonzero part of the
        // difference this test is protecting, not a formula that happened to net to zero.
        var withoutStreakBug = score.ComputeDayScore(done, total, on, off, streak: 0, isExemptDay: false).FlooredTotal;
        Assert.NotEqual(withoutStreakBug, recalculated);
    }
}

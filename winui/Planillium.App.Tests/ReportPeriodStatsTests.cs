using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// ReportData.PeriodStats — the aggregation behind Reports' score card and insights panel
/// (2026-08-04: everything above the diary now follows the Day/Week/Month/Year selector; the
/// card previously always showed today and the insights were always computed from this week).
///
/// The invariants worth pinning are the boundaries, because those are where a period aggregate
/// goes subtly wrong: Day must reproduce exactly what the old today-only card showed, Week must
/// agree with the summary table rendered directly beneath it, and a wider period must never
/// report less than a narrower one nested inside it.
///
/// Each test uses a unique plan id (Guid) — the whole assembly shares one SQLite file, see
/// TestRootFixture — and writes its diary rows under dates it owns.
/// </summary>
[Collection("TestRoot")]
public sealed class ReportPeriodStatsTests
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

    private static void AddDiaryRow(Database db, DateOnly date, string category, int minutes)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, '09:00', '10:00', $m, $c, 'TestWindow', NULL)";
        cmd.Parameters.AddWithValue("$d", date.ToIsoDate());
        cmd.Parameters.AddWithValue("$m", minutes);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Day must reproduce the old today-only card exactly — this is the regression
    /// guard on the change itself, since Day is the one period whose meaning didn't move.</summary>
    [Fact]
    public void DayPeriodMatchesTodaysOwnStats()
    {
        var planId = "period-day-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task A"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var totals = ReportData.PeriodStats(ReportPeriod.Day, db.Conn, score);

        var (dayTotal, dayDone) = score.DayTaskCounts(today);
        var (on, off) = score.DayDiaryMinutes(today);
        var isExempt = score.AllPlansScoringExempt(today);
        Assert.Equal(dayTotal, totals.Total);
        Assert.Equal(dayDone, totals.Done);
        Assert.Equal(score.DayScore(dayDone, dayTotal, on, off, score.CurrentStreak(today), isExempt),
            totals.Score);
    }

    /// <summary>Week must agree with the day table rendered directly below it. Two figures for
    /// the same week, a few pixels apart, disagreeing is this project's most-repeated UI
    /// complaint shape.</summary>
    [Fact]
    public void WeekPeriodAgreesWithTheSummaryTableBeneathIt()
    {
        var planId = "period-week-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task A"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);

        var totals = ReportData.PeriodStats(ReportPeriod.Week, db.Conn, score);
        var weekStats = ReportData.WeekStats(db.Conn, score);

        Assert.Equal(weekStats.Sum(s => s.Score), totals.Score);
        Assert.Equal(weekStats.Sum(s => s.Done), totals.Done);
        Assert.Equal(weekStats.Sum(s => s.Total), totals.Total);
        Assert.Equal(weekStats.Sum(s => s.OnMin), totals.OnMin);
        Assert.Equal(weekStats.Sum(s => s.OffMin), totals.OffMin);
    }

    /// <summary>Minutes logged today have to show up in every period that contains today —
    /// catches a period-start off-by-one, which would silently drop the boundary day.</summary>
    [Fact]
    public void TodaysMinutesAreCountedByEveryPeriodContainingToday()
    {
        var planId = "period-mins-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task A"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var before = new[] { ReportPeriod.Day, ReportPeriod.Week, ReportPeriod.Month, ReportPeriod.Year }
            .ToDictionary(p => p, p => ReportData.PeriodStats(p, db.Conn, score));

        AddDiaryRow(db, today, DiaryCategory.OnPlan, 45);
        AddDiaryRow(db, today, DiaryCategory.OffPlan, 20);

        foreach (var period in before.Keys)
        {
            var after = ReportData.PeriodStats(period, db.Conn, score);
            // A day off zeroes passive minutes by design (business rule 10), so only assert the
            // delta when today actually counts — otherwise this test would fail on a rest day.
            if (score.AllPlansScoringExempt(today)) continue;
            Assert.Equal(before[period].OnMin + 45, after.OnMin);
            Assert.Equal(before[period].OffMin + 20, after.OffMin);
        }
    }

    /// <summary>Day ⊆ Week ⊆ Year and Day ⊆ Month ⊆ Year, so a wider period's totals can never
    /// be smaller. Cheap, but it catches a whole class of range and merge mistakes — including
    /// double-counting between raw time_diary rows and the rollup table they age out into.
    ///
    /// Week vs. Month is deliberately NOT asserted: they aren't nested. A week beginning in the
    /// previous month legitimately covers days this month's total excludes, so "month ≥ week"
    /// is false for real on the first days of a month, and asserting it would produce a test
    /// that fails a few mornings a year for no reason.</summary>
    [Fact]
    public void WiderPeriodsAreNeverSmallerThanTheOnesNestedInsideThem()
    {
        var planId = "period-nest-" + Guid.NewGuid();
        var plan = MakePlan(planId, (1, "Task A"));
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { plan }, db);
        AddDiaryRow(db, DateOnly.FromDateTime(DateTime.Today), DiaryCategory.OnPlan, 30);

        var day = ReportData.PeriodStats(ReportPeriod.Day, db.Conn, score);
        var week = ReportData.PeriodStats(ReportPeriod.Week, db.Conn, score);
        var month = ReportData.PeriodStats(ReportPeriod.Month, db.Conn, score);
        var year = ReportData.PeriodStats(ReportPeriod.Year, db.Conn, score);

        Assert.True(week.OnMin >= day.OnMin, $"week {week.OnMin} < day {day.OnMin}");
        Assert.True(month.OnMin >= day.OnMin, $"month {month.OnMin} < day {day.OnMin}");
        Assert.True(year.OnMin >= week.OnMin, $"year {year.OnMin} < week {week.OnMin}");
        Assert.True(year.OnMin >= month.OnMin, $"year {year.OnMin} < month {month.OnMin}");
        Assert.True(year.Total >= month.Total && year.Total >= week.Total);
    }
}

using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// The five-category columns the summary tables, the bars and the exports all show
/// (2026-08-05 request: "add all the categories and summary for the rows as well").
///
/// Two things are worth pinning here, and neither is visible by looking at the screen:
/// that the category *lists* can't drift apart as a sixth category is added, and that a row's
/// Total really is its own categories added up — the figure changed meaning in this change
/// (it used to be on-plan + off-plan only), so a silent regression to the old sum would look
/// entirely plausible.
///
/// Each test uses a unique plan id (Guid) — the whole assembly shares one SQLite file, see
/// TestRootFixture — and writes its diary rows under dates it owns.
/// </summary>
[Collection("TestRoot")]
public sealed class ReportCategoryColumnTests
{
    private static Plan MakePlan(string planId)
    {
        var phase = new Phase { Number = 1, Name = "Phase 1" };
        phase.Tasks.Add(new PlanTask { Day = 1, Text = "Task A" });
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

    /// <summary>The dropdown order and the report order are deliberately different lists, but
    /// they must always hold the same five categories. Adding a sixth to one and not the other
    /// would drop a column from every table, bar and export with nothing to notice it.</summary>
    [Fact]
    public void EditableOptionsAndReportOrderHoldTheSameCategories()
    {
        Assert.Equal(
            DiaryCategory.EditableOptions.Select(o => o.Value).OrderBy(v => v, StringComparer.Ordinal),
            DiaryCategory.ReportOrder.Select(o => o.Value).OrderBy(v => v, StringComparer.Ordinal));
        // Same category must also carry the same label in both, or the tables and the edit
        // dialog would name the same thing differently.
        foreach (var (label, value) in DiaryCategory.ReportOrder)
            Assert.Equal(label, DiaryCategory.EditableOptions.Single(o => o.Value == value).Label);
    }

    /// <summary>Every category the report order names must be one CategoryMinutes actually
    /// stores — Of() returns 0 for anything unknown, which as a column would read as "you spent
    /// no time on this" rather than as a missing wiring.</summary>
    [Fact]
    public void EveryReportedCategoryIsCarriedByCategoryMinutes()
    {
        var one = new ReportData.CategoryMinutes(1, 2, 3, 4, 5);
        foreach (var (_, value) in DiaryCategory.ReportOrder)
            Assert.True(one.Of(value) > 0, $"CategoryMinutes has no minutes for '{value}'.");
        Assert.Equal(15, one.TotalMin);
        Assert.Equal(0, one.Of("not_a_category"));
    }

    /// <summary>A day's Total column is all five categories, not the on/off pair it used to be —
    /// the neutral/paid/idle minutes below would have been invisible before this change.</summary>
    [Fact]
    public void DayRowTotalCountsEveryCategoryNotJustOnAndOffPlan()
    {
        var planId = "cat-day-" + Guid.NewGuid();
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { MakePlan(planId) }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var before = ReportData.WeekStats(db.Conn, score).Single(s => s.Date == today).Minutes;
        AddDiaryRow(db, today, DiaryCategory.OnPlan, 10);
        AddDiaryRow(db, today, DiaryCategory.OffPlan, 20);
        AddDiaryRow(db, today, DiaryCategory.Neutral, 30);
        AddDiaryRow(db, today, DiaryCategory.Paid, 40);
        AddDiaryRow(db, today, DiaryCategory.Idle, 50);
        var after = ReportData.WeekStats(db.Conn, score).Single(s => s.Date == today).Minutes;

        Assert.Equal(10, after.On - before.On);
        Assert.Equal(20, after.Off - before.Off);
        Assert.Equal(30, after.Neutral - before.Neutral);
        Assert.Equal(40, after.Paid - before.Paid);
        Assert.Equal(50, after.Idle - before.Idle);
        Assert.Equal(150, after.TotalMin - before.TotalMin);
    }

    /// <summary>The bucket tables (Month/Year) carry the same five categories as the day table —
    /// they were reading only on/off-plan out of the same rows, so neutral, paid and idle time
    /// simply didn't exist as far as those two views were concerned.</summary>
    [Fact]
    public void YearBucketsCarryEveryCategory()
    {
        var planId = "cat-year-" + Guid.NewGuid();
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { MakePlan(planId) }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        static ReportData.CategoryMinutes Sum(List<ReportData.BucketStat> b) =>
            b.Aggregate(ReportData.CategoryMinutes.Zero, (acc, x) => acc.Plus(x.Minutes));

        var before = Sum(ReportData.YearBuckets(db.Conn, score));
        AddDiaryRow(db, today, DiaryCategory.Neutral, 25);
        AddDiaryRow(db, today, DiaryCategory.Paid, 35);
        AddDiaryRow(db, today, DiaryCategory.Idle, 45);
        var after = Sum(ReportData.YearBuckets(db.Conn, score));

        Assert.Equal(25, after.Neutral - before.Neutral);
        Assert.Equal(35, after.Paid - before.Paid);
        Assert.Equal(45, after.Idle - before.Idle);
        Assert.Equal(105, after.TotalMin - before.TotalMin);
    }

    /// <summary>The Score column added to the bucket tables must total to exactly what the score
    /// card above them shows for the same period (2026-08-05). Two figures for the same thing a
    /// few pixels apart, disagreeing, is this project's most-repeated complaint shape — and the
    /// two paths differ in a way that makes it easy: the buckets used to skip day-off dates
    /// outright, while the card counts their score.</summary>
    [Fact]
    public void BucketScoresTotalToTheScoreCardForTheSamePeriod()
    {
        var planId = "cat-score-" + Guid.NewGuid();
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { MakePlan(planId) }, db);
        AddDiaryRow(db, DateOnly.FromDateTime(DateTime.Today), DiaryCategory.OnPlan, 90);

        Assert.Equal(
            ReportData.PeriodStats(ReportPeriod.Year, db.Conn, score).Score,
            ReportData.YearBuckets(db.Conn, score).Sum(b => b.Score));
        Assert.Equal(
            ReportData.PeriodStats(ReportPeriod.Month, db.Conn, score).Score,
            ReportData.MonthBuckets(db.Conn, score).Sum(b => b.Score));
        // And the day table's own score column, which the card has always had to match.
        Assert.Equal(
            ReportData.PeriodStats(ReportPeriod.Week, db.Conn, score).Score,
            ReportData.WeekStats(db.Conn, score).Sum(s => s.Score));
    }

    /// <summary>Task counts follow the same rule as the score — the bucket tables gained the
    /// column at the same time and from the same walk.</summary>
    [Fact]
    public void BucketTaskCountsTotalToTheScoreCardForTheSamePeriod()
    {
        var planId = "cat-tasks-" + Guid.NewGuid();
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { MakePlan(planId) }, db);

        var year = ReportData.PeriodStats(ReportPeriod.Year, db.Conn, score);
        var buckets = ReportData.YearBuckets(db.Conn, score);
        Assert.Equal(year.Done, buckets.Sum(b => b.Done));
        Assert.Equal(year.Total, buckets.Sum(b => b.Total));
    }

    /// <summary>Durations read as decimal hours everywhere on Reports except the diary
    /// (2026-08-05 request: "instead of 5 h30 min show 5,5 hours").</summary>
    [Fact]
    public void DurationsFormatAsDecimalHours()
    {
        var sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat
            .NumberDecimalSeparator;
        Assert.Equal($"5{sep}5 h", ReportData.FmtHours(330));
        // A whole number of hours carries no decimal at all, and neither does zero.
        Assert.Equal("4 h", ReportData.FmtHours(240));
        Assert.Equal("0 h", ReportData.FmtHours(0));
        // Minutes never appear, however small the figure.
        Assert.DoesNotContain("m", ReportData.FmtHours(12));
        Assert.Equal($"0{sep}2 h", ReportData.FmtHours(12));
    }

    /// <summary>An unrecognised category string must not land in any column or in the row total.
    /// The DB column is free text, and a value written by a future version (or a hand-edit)
    /// silently inflating "Total" would be very hard to trace back.</summary>
    [Fact]
    public void UnknownCategoriesAreExcludedFromEveryColumn()
    {
        var planId = "cat-unknown-" + Guid.NewGuid();
        using var db = new Database();
        using var score = new ScoreService(new List<Plan> { MakePlan(planId) }, db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var before = ReportData.WeekStats(db.Conn, score).Single(s => s.Date == today).Minutes;
        AddDiaryRow(db, today, "some_future_category", 99);
        var after = ReportData.WeekStats(db.Conn, score).Single(s => s.Date == today).Minutes;

        Assert.Equal(before.TotalMin, after.TotalMin);
    }
}

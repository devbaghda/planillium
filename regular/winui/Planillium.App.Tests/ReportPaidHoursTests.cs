using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>2026-11-05 (Reports money columns): DailyFigures carries the minutes of payable-tagged work next to
/// the euro figures, so a Reports row can show Paid hours, Paid-work income, Unearned income (= Balance - Paid
/// work) and Balance, and the days of a period add up to the period.</summary>
[Collection("TestRoot")]
public sealed class ReportPaidHoursTests
{
    [Fact]
    public void DailyFigures_CarryPaidMinutes_AndPaidWorkIsMinutesTimesRate()
    {
        var tag = "q" + Guid.NewGuid().ToString("N")[..8];
        var today = DateOnly.FromDateTime(DateTime.Today);
        PaidTags.Save(new[] { new PaidTag(tag, "Q", 1680) });   // 10 EUR/h
        using var db = new Database();
        db.InsertDiaryEntry(today.ToIsoDate(), "09:00", "10:30", 90, "on_plan", "w", null, tag);
        db.InsertDiaryEntry(today.ToIsoDate(), "11:00", "11:30", 30, "on_plan", "w2", null, tag);
        db.InsertDiaryEntry(today.ToIsoDate(), "12:00", "12:45", 45, "on_plan", "w3", null, null);   // untagged: not paid work
        using var svc = new IncomeService(db);
        var score = new ScoreService(new List<Planillium.App.Models.Plan>(), db);

        var day = svc.DailyFigures(score, today)[today];
        Assert.Equal(120, day.PaidMin);
        Assert.Equal(20.0, day.Paid, 6);
        // Balance (Income) already contains the paid work, so Unearned = Income - Paid is what was not earned.
        Assert.Equal(day.Income - 20.0, day.Income - day.Paid, 6);
    }

    [Fact]
    public void DailyFigures_NoPayableTime_PaidMinutesZero()
    {
        PaidTags.Save(Array.Empty<PaidTag>());
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = new Database();
        using var svc = new IncomeService(db);
        var score = new ScoreService(new List<Planillium.App.Models.Plan>(), db);
        var day = svc.DailyFigures(score, today)[today];
        Assert.Equal(0, day.PaidMin);
        Assert.Equal(0.0, day.Paid, 6);
    }
}

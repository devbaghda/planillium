using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>QA-owned (B1, 2026-10-24): acceptance lines 3 and 4 at the service level. The Reports strip
/// shows IncomeService.SumForPeriod (first figure) and PaidEarningsForPeriod.Sum (second). Option A:
/// the first figure already contains the second.</summary>
[Collection("TestRoot")]
public sealed class B1IncomeStripTests
{
    [Fact]
    public void NoPayableTag_PaidWorkIsZero_NoError()
    {
        PaidTags.Save(Array.Empty<PaidTag>());
        using var db = new Database();
        using var svc = new IncomeService(db);
        Assert.Equal(0.0, svc.PaidEarningsForPeriod(ReportPeriod.Month).Sum(e => e.Eur));
    }

    [Theory]
    [InlineData(ReportPeriod.Day)] [InlineData(ReportPeriod.Week)]
    [InlineData(ReportPeriod.Month)] [InlineData(ReportPeriod.Year)]
    public void PaidWork_EqualsTaggedMinutesTimesRate_AndIsInsideFirstFigure(ReportPeriod period)
    {
        var tag = "q" + Guid.NewGuid().ToString("N")[..8];
        var today = DateOnly.FromDateTime(DateTime.Today);
        PaidTags.Save(new[] { new PaidTag(tag, "Studioshoo", 1680) });   // 10 EUR/h
        using var db = new Database();
        db.InsertDiaryEntry(today.ToIsoDate(), "09:00", "10:30", 90, "work", "w", null, tag);
        using var svc = new IncomeService(db);
        var score = new ScoreService(new List<Planillium.App.Models.Plan>(), db);

        var paid = svc.PaidEarningsForPeriod(period).Sum(e => e.Eur);
        Assert.Equal(15.0, paid, 6);
        var combined = svc.SumForPeriod(period, score);

        PaidTags.Save(new[] { new PaidTag(tag, "Studioshoo", 0) });   // re-price to 0
        var unearnedOnly = svc.SumForPeriod(period, score);
        Assert.Equal(combined - paid, unearnedOnly, 6);
    }
}

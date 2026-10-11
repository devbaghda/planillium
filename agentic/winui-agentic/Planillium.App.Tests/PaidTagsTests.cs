using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>Payable tags (2026-10-01): paid hours = tagged diary minutes × monthly ÷ 168 h.
/// Each test uses its own unique tag so rows left in the shared test DB can't collide.</summary>
[Collection("TestRoot")]
public sealed class PaidTagsTests
{
    private static string UniqueTag() => "t" + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public void Earnings_AreTaggedMinutesTimesMonthlyOver168()
    {
        var tag = UniqueTag();
        PaidTags.Save(new[] { new PaidTag(tag, "Test", 1680) });   // 10 EUR/h
        using var db = new Database();
        db.InsertDiaryEntry("2026-03-10", "09:00", "10:30", 90, "work", "w", null, tag);
        db.InsertDiaryEntry("2026-03-11", "09:00", "10:00", 60, "work", "w", null, tag);
        db.InsertDiaryEntry("2026-03-10", "11:00", "12:00", 60, "work", "w", null, null);   // untagged: ignored
        using var svc = new IncomeService(db);

        var e = Assert.Single(svc.PaidEarnings(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        Assert.Equal(150, e.Minutes);
        Assert.Equal(25.0, e.Eur, precision: 6);
    }

    [Fact]
    public void Earnings_RespectRangeEnds()
    {
        var tag = UniqueTag();
        PaidTags.Save(new[] { new PaidTag(tag, "Test", 1680) });
        using var db = new Database();
        db.InsertDiaryEntry("2026-03-10", "09:00", "10:00", 60, "work", "w", null, tag);
        using var svc = new IncomeService(db);

        Assert.Empty(svc.PaidEarnings(new DateOnly(2026, 3, 11), new DateOnly(2026, 3, 31)));
        Assert.Single(svc.PaidEarnings(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void CustomPaidTag_BecomesADiaryOption()
    {
        var tag = UniqueTag();
        PaidTags.Save(new[] { new PaidTag(tag, "Consulting", 500) });

        Assert.Contains(DiaryTag.Options, o => o.Value == tag && o.Label == "Consulting");
        Assert.Equal(5, DiaryTag.Options.Count(o => o.Value != tag));   // built-ins untouched
    }

    [Fact]
    public void Slug_KeepsLettersAndDigitsLowercase() =>
        Assert.Equal("studioshoo2", PaidTags.Slug(" Studio-Shoo 2 "));
}

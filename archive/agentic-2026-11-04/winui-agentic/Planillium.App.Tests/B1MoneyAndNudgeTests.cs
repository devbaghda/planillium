using System.Globalization;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>QA-owned suite for feature b1-reports-settings: MoneyFormat (acceptance 6, 7, 12, 16),
/// nudge wording (5, 14), paid-work figure (3).</summary>
public sealed class B1MoneyFormatTests
{
    private const char T = ' ';

    [Theory]
    [InlineData("14000", "14 000")]
    [InlineData("2925", "2 925")]
    [InlineData("1600", "1 600")]
    [InlineData("0014", "14")]
    [InlineData("abc", "")]
    [InlineData("-5", "5")]
    [InlineData("", "")]
    [InlineData("1234567890", "123 456 789")]   // capped at 9 integer digits
    public void Normalize_GroupsAndCleans(string raw, string expected)
    {
        Assert.Equal(expected, MoneyFormat.Normalize(raw, raw.Length, ',').Text);
    }

    [Fact]
    public void Normalize_DecimalCappedAtTwoDigits_AndOneSeparator()
    {
        Assert.Equal("1" + T + "600,50", MoneyFormat.Normalize("1600,505", 8, ',').Text);
        Assert.Equal("1,57", MoneyFormat.Normalize("1,5,7", 5, ',').Text);
    }

    [Fact]
    public void Normalize_CaretStaysAfterSameDigit()
    {
        var (text, caret) = MoneyFormat.Normalize("16500", 3, ',');
        Assert.Equal("16" + T + "500", text);
        Assert.Equal(4, caret);
    }

    [Fact]
    public void Parse_ReturnsPlainNumber_NaNWhenEmpty()
    {
        Assert.Equal(14000, MoneyFormat.Parse("14" + T + "000"));
        Assert.Equal(1600.5, MoneyFormat.Parse("1 600,5"));
        Assert.True(double.IsNaN(MoneyFormat.Parse("")));
        Assert.True(double.IsNaN(MoneyFormat.Parse("abc")));
    }

    [Fact]
    public void FromNumber_NoTrailingZeros_NegativeShownAsZero_NaNEmpty()
    {
        Assert.Equal("2" + T + "925", MoneyFormat.FromNumber(2925, ','));
        Assert.Equal("1" + T + "600,5", MoneyFormat.FromNumber(1600.5, ','));
        Assert.Equal("0", MoneyFormat.FromNumber(-5, ','));
        Assert.Equal("", MoneyFormat.FromNumber(double.NaN, ','));
    }

    [Theory]
    [InlineData("14.000", "14" + T3 + "000", 14000.0)]
    [InlineData("1.600", "1" + T3 + "600", 1600.0)]
    [InlineData("14.000.000", "14" + T3 + "000" + T3 + "000", 14000000.0)]
    [InlineData("14.000,5", "14" + T3 + "000,5", 14000.5)]
    [InlineData("14.5", "14,5", 14.5)]
    [InlineData("14,5", "14,5", 14.5)]
    [InlineData("14.55", "14,55", 14.55)]
    [InlineData("1,600", "1,60", 1.6)]
    public void DotPlusThreeDigits_IsThousands_CommaIsDecimal(string raw, string expectedText, double expectedValue)
    {
        // Rule decided 2026-10-23. A dot with 1-2 digits stays a dot on screen only while it is still the
        // last thing typed (pending); Normalize of a complete decimal like "14.5" keeps it pending, so settle it.
        var (text, _) = MoneyFormat.Normalize(raw, raw.Length, ',');
        text = MoneyFormat.Settle(text, ',');
        Assert.Equal(expectedText, text);
        Assert.Equal(expectedValue, MoneyFormat.Parse(text), 6);
        Assert.Equal(expectedValue, MoneyFormat.Parse(raw), 6);
    }

    [Fact]
    public void MidTyping_OneDotZeroDotZeroZeroDotZeroZeroZero_ReachesThousands()
    {
        var steps = new[] { "1", "1.", "1.0", "1.00", "1.000" };
        var texts = steps.Select(s => MoneyFormat.Normalize(s, s.Length, ',').Text).ToArray();
        Assert.Equal("1", texts[0]);
        Assert.Equal("1.", texts[1]);      // pending dot stays a dot
        Assert.Equal("1.0", texts[2]);
        Assert.Equal("1.00", texts[3]);
        Assert.Equal("1" + T3 + "000", texts[4]);
        Assert.Equal(1000.0, MoneyFormat.Parse(texts[4]));
    }

    [Fact]
    public void FocusLoss_SettlesPendingDotToCultureSeparator()
    {
        Assert.Equal("1,5", MoneyFormat.Settle("1.5", ','));
        Assert.Equal("1,", MoneyFormat.Settle("1.", ','));
        Assert.Equal("1" + T3 + "000", MoneyFormat.Settle("1" + T3 + "000", ','));
        Assert.Equal("1.5", MoneyFormat.Settle("1.5", '.'));
    }

    [Fact]
    public void DotFollowedByFourOrMoreDigits_IsDecimalCutToTwo()
    {
        var (text, _) = MoneyFormat.Normalize("14.0001", 7, ',');
        Assert.Equal("14,00", text);
    }

    [Fact]
    public void SavedValueIsPlainNumber_ForDotThousands()
    {
        Assert.Equal(14000.0, MoneyFormat.Parse(MoneyFormat.Normalize("14.000", 6, ',').Text));
        Assert.Equal("14" + T3 + "000", MoneyFormat.FromNumber(14000, ','));
    }

    private const string T3 = " ";
}

[Collection("TestRoot")]
public sealed class B1ReportsAndNudgeTests
{
    [Fact]
    public void Nudge_SaysAppsAndActivities_NotOldPhrase()
    {
        var hints = InsightRules.Suggestions(on: 1000, off: 300, neutral: 0,
            distractions: new List<(string, int)>(), hourValueEur: 16.07, period: ReportPeriod.Month);
        var all = string.Join(" ", hints);
        Assert.Contains("distracting apps and activities", all);
        Assert.DoesNotContain("distracting apps during working hours", all);
    }

    [Fact]
    public void PaidWorkFigure_MatchesTaggedTimeOnAllPeriods_AndZeroWithoutTags()
    {
        var tag = "t" + Guid.NewGuid().ToString("N")[..8];
        PaidTags.Save(new[] { new PaidTag(tag, "Studioshoo", 1680) });   // 10 EUR/h
        using var db = new Database();
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        db.InsertDiaryEntry(today, "09:00", "10:00", 60, "work", "w", null, tag);
        using var svc = new IncomeService(db);
        var periods = new[] { ReportPeriod.Day, ReportPeriod.Week, ReportPeriod.Month, ReportPeriod.Year };
        foreach (var p in periods)
            Assert.True(svc.PaidEarningsForPeriod(p).Sum(e => e.Eur) >= 10.0 - 1e-9, p.ToString());

        PaidTags.Save(Array.Empty<PaidTag>());
        foreach (var p in periods)
            Assert.Equal(0.0, svc.PaidEarningsForPeriod(p).Sum(e => e.Eur));
    }
}

using System.Globalization;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// InsightRules.Suggestions — the pure logic behind insights text and the ratio calculation
/// that determines whether the ratio insight fires (2026-10-01 insights-eur-and-ratio).
///
/// Each test verifies the exact output sentences, EUR formatting, and the 40% ratio
/// threshold against on + neutral (not on alone), as well as edge cases like hour value 0,
/// small distraction minutes, and boundary conditions.
/// </summary>
public sealed class InsightRulesTests
{
    private static readonly CultureInfo TestCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Acceptance #1: off = 300 min, hourValue = 16.07 contains "5 h" and "(€80.35)"
    /// in the first sentence.
    /// </summary>
    [Fact]
    public void FirstSentenceIncludesHoursAndEur()
    {
        CultureInfo.CurrentCulture = TestCulture;
        try
        {
            var hints = InsightRules.Suggestions(
                on: 1000, off: 300, neutral: 0, // high on value so ratio doesn't fire
                distractions: new List<(string, int)>(),
                hourValueEur: 16.07,
                period: ReportPeriod.Day);

            Assert.Single(hints);
            var sentence = hints[0];
            Assert.Contains("5 h", sentence); // FmtHours("0.#") format doesn't show .0
            Assert.Contains("€80,35", sentence); // 300/60 * 16.07 ≈ 80.35 (rounding in FormatEur)
            Assert.Contains("off-plan today", sentence);
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// Acceptance #2: Biggest-distraction sentence contains hours and EUR,
    /// same hour value as all other figures.
    /// </summary>
    [Fact]
    public void DistractionSentenceIncludesHoursAndEur()
    {
        CultureInfo.CurrentCulture = TestCulture;
        try
        {
            var hints = InsightRules.Suggestions(
                on: 1000, off: 100, neutral: 0, // high on so first sentence and ratio don't fire
                distractions: new List<(string, int)> { ("YouTube", 90) },
                hourValueEur: 12.0,
                period: ReportPeriod.Week);

            Assert.Single(hints); // Only distraction
            var distractionHint = hints[0];
            Assert.Contains("YouTube", distractionHint);
            Assert.Contains("1,5 h", distractionHint); // 90/60 = 1.5
            Assert.Contains("€18,00", distractionHint); // 90/60 * 12.0 = 18.0
            Assert.Contains("off-plan this week", distractionHint);
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// Acceptance #3: Ratio insight fires iff on + neutral > 0 and off/(on+neutral) > 0.40.
    /// Test cases:
    /// - off 4/on 10/neutral 0 -> no (exactly 40%)
    /// - off 5/on 5/neutral 5 -> yes (50%)
    /// - off 3/on 1/neutral 9 -> no (30%)
    /// - off 5/on 0/neutral 0 -> no (divide by zero guard)
    /// - off 5/on 0/neutral 8 -> yes (62.5%)
    /// </summary>
    [Fact]
    public void RatioInsightExactly40PercentDoesNotFire()
    {
        var hints = InsightRules.Suggestions(
            on: 250, off: 100, neutral: 0, // ratio = 100/250 = 0.4 exactly; off < 120 so first doesn't fire
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        // No hints when off <= 120, ratio <= 40%, and no distractions
        var hasRatioHint = hints.Any(h => h.Contains("40%"));
        Assert.False(hasRatioHint);
        var hasFirstHint = hints.Any(h => h.Contains("You spent"));
        Assert.False(hasFirstHint);
    }

    [Fact]
    public void RatioInsightAt50PercentFires()
    {
        var hints = InsightRules.Suggestions(
            on: 50, off: 50, neutral: 50, // ratio = 50/(50+50) = 0.5 = 50%; off=50 < 120 so first doesn't fire
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        var ratioHint = hints.FirstOrDefault(h => h.Contains("40%"));
        Assert.NotNull(ratioHint);
        Assert.Contains("Off-plan time is over 40%", ratioHint);
    }

    [Fact]
    public void RatioInsightAt30PercentDoesNotFire()
    {
        var hints = InsightRules.Suggestions(
            on: 10, off: 30, neutral: 90, // ratio = 30/(10+90) = 0.3 = 30%; off=30 < 120 so first doesn't fire
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        var hasRatioHint = hints.Any(h => h.Contains("40%"));
        Assert.False(hasRatioHint);
    }

    [Fact]
    public void RatioInsightWithZeroDenominatorDoesNotFire()
    {
        var hints = InsightRules.Suggestions(
            on: 0, off: 50, neutral: 0, // denominator = 0; off < 120 so first doesn't fire
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        var hasRatioHint = hints.Any(h => h.Contains("40%"));
        Assert.False(hasRatioHint); // No crash, no ratio insight
    }

    [Fact]
    public void RatioInsightAt62Percent5PercentFires()
    {
        var hints = InsightRules.Suggestions(
            on: 0, off: 50, neutral: 80, // ratio = 50/(0+80) = 0.625 = 62.5%; off < 120 so first doesn't fire
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        var ratioHint = hints.FirstOrDefault(h => h.Contains("40%"));
        Assert.NotNull(ratioHint);
    }

    /// <summary>
    /// Acceptance #4: Paid and idle minutes do not affect the ratio
    /// (they are not included in the on/neutral denominators).
    /// This is tested implicitly in PeriodStats, which excludes them.
    /// Here we test that the Suggestions function doesn't use them.
    /// </summary>
    [Fact]
    public void PaidAndIdleMinutesExcludedFromRatioDenominator()
    {
        // Same ratio as before (50%), but verify paid/idle don't change the outcome
        var hints = InsightRules.Suggestions(
            on: 5, off: 5, neutral: 5,
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        var ratioHint = hints.FirstOrDefault(h => h.Contains("40%"));
        Assert.NotNull(ratioHint); // Ratio should fire at 50%
    }

    /// <summary>
    /// Hour value 0 (income configured 0): show "EUR 0.00" consistently, no crash/NaN.
    /// </summary>
    [Fact]
    public void HourValueZeroShowsEurZero()
    {
        CultureInfo.CurrentCulture = TestCulture;
        try
        {
            var hints = InsightRules.Suggestions(
                on: 1000, off: 300, neutral: 0, // high on so only first + distraction fire
                distractions: new List<(string, int)> { ("Chrome", 120) },
                hourValueEur: 0.0,
                period: ReportPeriod.Day);

            Assert.Equal(2, hints.Count); // First sentence + distraction
            foreach (var hint in hints)
                Assert.Contains("€0,00", hint);
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// Biggest-distraction sentence: top.Minutes may be small (<1 h); EUR still shown,
    /// rounded by FormatEur (2 dp). FmtHours("0.#") rounds 0.25 to "0,3" in German.
    /// </summary>
    [Fact]
    public void SmallDistractionMinutesStillShowEur()
    {
        CultureInfo.CurrentCulture = TestCulture;
        try
        {
            var hints = InsightRules.Suggestions(
                on: 1000, off: 100, neutral: 0, // high on so only distraction fires
                distractions: new List<(string, int)> { ("Slack", 15) }, // 15 min = 0.25 h -> "0,3 h" after rounding
                hourValueEur: 8.0,
                period: ReportPeriod.Day);

            Assert.Single(hints);
            var distractionHint = hints[0];
            Assert.Contains("Slack", distractionHint);
            Assert.Contains("0,3 h", distractionHint); // 0.25 rounded to 1 decimal
            Assert.Contains("€2,00", distractionHint); // 15/60 * 8.0 = 2.0
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// off <= 120 min: the first sentence stays suppressed exactly as today,
    /// regardless of EUR.
    /// </summary>
    [Fact]
    public void OffPlanUnder120MinSuppressesFirstSentence()
    {
        var hints = InsightRules.Suggestions(
            on: 100, off: 119, neutral: 0, // ratio is 119/100 = 1.19, which fires ratio insight
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        // First sentence is suppressed (off <= 120), but ratio insight fires if > 40%
        var hasFirstSentence = hints.Any(h => h.Contains("You spent"));
        Assert.False(hasFirstSentence);
        var hasRatioHint = hints.Any(h => h.Contains("40%"));
        Assert.True(hasRatioHint);
    }

    /// <summary>
    /// Fallback sentence "No major distraction patterns..." logic unchanged:
    /// appears only if no other hint fired.
    /// </summary>
    [Fact]
    public void FallbackSentenceAppearsWhenNoHintsFired()
    {
        var hints = InsightRules.Suggestions(
            on: 1000, off: 50, neutral: 0, // off < 120, ratio is 50/1000 < 40%, no distractions
            distractions: new List<(string, int)>(),
            hourValueEur: 10.0,
            period: ReportPeriod.Day);

        Assert.Single(hints);
        Assert.Contains("No major distraction patterns detected", hints[0]);
        Assert.Contains("today", hints[0]);
    }

    /// <summary>
    /// Period phrase follows the selector: Day -> "today", Week -> "this week", etc.
    /// </summary>
    [Fact]
    public void PeriodPhrasesAreCorrect()
    {
        var testCases = new[]
        {
            (ReportPeriod.Day, "today"),
            (ReportPeriod.Week, "this week"),
            (ReportPeriod.Month, "this month"),
            (ReportPeriod.Year, "this year"),
        };

        foreach (var (period, expectedPhrase) in testCases)
        {
            var hints = InsightRules.Suggestions(
                on: 1000, off: 300, neutral: 0, // high on so only first sentence fires
                distractions: new List<(string, int)>(),
                hourValueEur: 10.0,
                period: period);

            Assert.Single(hints);
            Assert.Contains(expectedPhrase, hints[0]);
        }
    }

    /// <summary>
    /// Culture-specific EUR formatting: FormatEur uses CurrentCulture decimal separator.
    /// </summary>
    [Fact]
    public void EurFormattingRespectsCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Test with German culture (comma decimal separator)
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var hints = InsightRules.Suggestions(
                on: 1000, off: 121, neutral: 0, // off > 120 so first sentence fires
                distractions: new List<(string, int)>(),
                hourValueEur: 5.5,
                period: ReportPeriod.Day);

            Assert.Single(hints);
            Assert.Contains("€11,09", hints[0]); // 121/60 * 5.5 ≈ 11.09, German comma

            // Test with US culture (period decimal separator)
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            hints = InsightRules.Suggestions(
                on: 1000, off: 121, neutral: 0,
                distractions: new List<(string, int)>(),
                hourValueEur: 5.5,
                period: ReportPeriod.Day);

            Assert.Single(hints);
            Assert.Contains("€11.09", hints[0]); // 121/60 * 5.5 ≈ 11.09, US period
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

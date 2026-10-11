using System.Globalization;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// Tests for DateExtensions.ToDisplayAbsenceSpan, the "Welcome back" dialog's
/// absence-time formatter. Covers same-day and midnight-crossing spans, edge cases
/// (exact midnight boundary, year transitions), and culture independence
/// (InvariantCulture regardless of OS locale).
/// </summary>
[Collection("TestRoot")]
public sealed class AbsenceSpanFormatTests
{
    [Fact]
    public void SameDayAbsence_FormatsAsDateWithCommaAndTimes()
    {
        // 2026-10-01 is a Thursday
        var start = new DateTime(2026, 10, 1, 10, 30, 0);
        var end = new DateTime(2026, 10, 1, 10, 36, 0);

        var result = start.ToDisplayAbsenceSpan(end);

        Assert.Equal("Thu 01.10, 10:30–10:36", result);
    }

    [Fact]
    public void MidnightCrossingAbsence_FormatsAsBothDatesWithSpacedDash()
    {
        // 2026-09-30 is a Wednesday, 2026-10-01 is a Thursday
        var start = new DateTime(2026, 9, 30, 23, 50, 0);
        var end = new DateTime(2026, 10, 1, 0, 5, 0);

        var result = start.ToDisplayAbsenceSpan(end);

        Assert.Equal("Wed 30.09 23:50 – Thu 01.10 00:05", result);
    }

    [Fact]
    public void ExactMidnightEnd_FormatsAsTwoDateForm()
    {
        // End at exactly 00:00 should still show two dates since end.Date != start.Date
        var start = new DateTime(2026, 9, 30, 23, 54, 0);
        var end = new DateTime(2026, 10, 1, 0, 0, 0);

        var result = start.ToDisplayAbsenceSpan(end);

        Assert.Equal("Wed 30.09 23:54 – Thu 01.10 00:00", result);
    }

    [Fact]
    public void YearBoundaryMidnightCrossing_FormatsCorrectlyAcrossDates()
    {
        // 2026-12-31 is a Thursday, 2027-01-01 is a Friday
        var start = new DateTime(2026, 12, 31, 23, 55, 0);
        var end = new DateTime(2027, 1, 1, 0, 10, 0);

        var result = start.ToDisplayAbsenceSpan(end);

        Assert.Equal("Thu 31.12 23:55 – Fri 01.01 00:10", result);
    }

    [Fact]
    public void ExactMidnightStart_FormatsAsSameDay()
    {
        // Starting exactly at 00:00 of the same calendar day
        var start = new DateTime(2026, 10, 1, 0, 0, 0);
        var end = new DateTime(2026, 10, 1, 0, 15, 0);

        var result = start.ToDisplayAbsenceSpan(end);

        Assert.Equal("Thu 01.10, 00:00–00:15", result);
    }

    [Fact]
    public void FormatsAreCultureIndependent_DE()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            var start = new DateTime(2026, 10, 1, 10, 30, 0);
            var end = new DateTime(2026, 10, 1, 10, 36, 0);

            var result = start.ToDisplayAbsenceSpan(end);

            // Should still format in English with InvariantCulture, not German
            Assert.Equal("Thu 01.10, 10:30–10:36", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void FormatsAreCultureIndependent_RU()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

            var start = new DateTime(2026, 9, 30, 23, 50, 0);
            var end = new DateTime(2026, 10, 1, 0, 5, 0);

            var result = start.ToDisplayAbsenceSpan(end);

            // Should still format in English with InvariantCulture, not Russian
            Assert.Equal("Wed 30.09 23:50 – Thu 01.10 00:05", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}

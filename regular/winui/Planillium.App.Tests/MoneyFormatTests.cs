using System.Globalization;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>QA-owned (B1, 2026-10-24): acceptance lines 6, 7, 12 and the user's separator decisions.</summary>
public sealed class MoneyFormatTests
{
    private static readonly CultureInfo It = new("it-IT");   // decimal comma
    private static readonly CultureInfo En = new("en-US");   // decimal dot
    private const char T = ' ';

    private static string N(string raw, CultureInfo c) => MoneyFormat.Normalize(raw, raw.Length, c, out _);

    [Fact] public void Typing14000_GroupsWithThinSpace() => Assert.Equal($"14{T}000", N("14000", It));
    [Fact] public void Typing2925() => Assert.Equal($"2{T}925", N("2925", It));
    [Fact] public void Typing1600() => Assert.Equal($"1{T}600", N("1600", It));
    [Fact] public void Under1000_NotGrouped() => Assert.Equal("999", N("999", It));
    [Fact] public void Parse_Grouped_Is14000() => Assert.Equal(14000.0, MoneyFormat.Parse($"14{T}000"));

    [Theory]
    [InlineData("14.000", 14000.0)]
    [InlineData("1.600", 1600.0)]
    [InlineData("14,5", 14.5)]
    [InlineData("14.5", 14.5)]
    [InlineData("14.50", 14.5)]
    [InlineData("1.234.567", 1234567.0)]
    public void Parse_SeparatorRules(string raw, double expected) => Assert.Equal(expected, MoneyFormat.Parse(raw));

    [Fact] public void DotThreeDigits_ShownAsThousands() => Assert.Equal($"14{T}000", MoneyFormat.Settle("14.000", It));
    [Fact] public void DotOneDigit_SettlesToCultureComma() => Assert.Equal("14,5", MoneyFormat.Settle("14.5", It));
    [Fact] public void DotOneDigit_EnglishCultureKeepsDot() => Assert.Equal("14.5", MoneyFormat.Settle("14.5", En));
    [Fact] public void Comma_IsDecimal() => Assert.Equal(14.5, MoneyFormat.Parse("14,5"));

    [Fact]
    public void MinusSign_IsDropped()
    {
        Assert.Equal("5", N("-5", It));
        Assert.Equal(5.0, MoneyFormat.Parse("-5"));
    }

    [Theory] [InlineData("")] [InlineData("abc")] [InlineData("-")] [InlineData(" ")]
    public void NonNumberOrEmpty_IsNaN_NoThrow(string raw)
    {
        Assert.Equal("", N(raw, It));
        Assert.True(double.IsNaN(MoneyFormat.Parse(raw)));
    }

    [Fact] public void Letters_AreStripped() => Assert.Equal($"1{T}200", N("1a2b0c0", It));

    [Fact]
    public void TooManyDigits_Capped() =>
        Assert.Equal(9, N(new string('9', 20), It).Count(char.IsAsciiDigit));

    [Fact] public void MoreThanTwoDecimals_Capped() => Assert.Equal(1.23, MoneyFormat.Parse("1,2345"));

    [Fact]
    public void FromNumber_RoundTrips()
    {
        Assert.Equal($"14{T}000", MoneyFormat.FromNumber(14000, It));
        Assert.Equal($"2{T}925", MoneyFormat.FromNumber(2925, It));
        Assert.Equal("14,5", MoneyFormat.FromNumber(14.5, It));
        Assert.Equal("0", MoneyFormat.FromNumber(0, It));
        Assert.Equal("", MoneyFormat.FromNumber(double.NaN, It));
        Assert.Equal("5", MoneyFormat.FromNumber(-5, It));
        Assert.Equal(14.05, MoneyFormat.Parse(MoneyFormat.FromNumber(14.05, It)), 6);
    }

    [Fact]
    public void CaretStaysAfterSameDigit()
    {
        var s = MoneyFormat.Normalize("14000", 2, It, out var caret);
        Assert.Equal($"14{T}000", s);
        Assert.Equal(2, caret);
    }
}

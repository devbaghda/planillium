using Planillium.App.Services;

namespace Planillium.App.Tests;

[Collection("TestRoot")]
public sealed class SignHelperTests
{
    private const double HourValue = 25.0;  // Example hour value in EUR

    [Fact]
    public void SignedHourValue_PositiveSum_ReturnsPositiveHourValue()
    {
        var result = SignHelper.SignedHourValue(100.0, HourValue);
        Assert.Equal(HourValue, result);
    }

    [Fact]
    public void SignedHourValue_NegativeSum_ReturnsNegativeHourValue()
    {
        var result = SignHelper.SignedHourValue(-50.0, HourValue);
        Assert.Equal(-HourValue, result);
    }

    [Fact]
    public void SignedHourValue_ZeroSum_ReturnsPositiveHourValue()
    {
        // When sum is 0 (no gain or loss), returns positive (neutral colour)
        var result = SignHelper.SignedHourValue(0.0, HourValue);
        Assert.Equal(HourValue, result);
    }

    [Fact]
    public void SignedHourValue_SmallNegativeSum_ReturnsNegativeHourValue()
    {
        var result = SignHelper.SignedHourValue(-0.01, HourValue);
        Assert.Equal(-HourValue, result);
    }

    [Fact]
    public void SignedHourValue_LargePositiveSum_ReturnsPositiveHourValue()
    {
        var result = SignHelper.SignedHourValue(1000.0, HourValue);
        Assert.Equal(HourValue, result);
    }

    [Fact]
    public void SignedHourValue_DifferentHourValue_UsesProvidedValue()
    {
        var customHourValue = 50.0;
        var result = SignHelper.SignedHourValue(-10.0, customHourValue);
        Assert.Equal(-customHourValue, result);
    }

    [Fact]
    public void SignedHourValue_ZeroHourValue_ReturnsZero()
    {
        var result = SignHelper.SignedHourValue(100.0, 0.0);
        Assert.Equal(0.0, result);
    }
}

using Xunit;
namespace Planillium.App.Tests;
public class B1bMinusTests
{
    [Theory]
    [InlineData("-", true)] [InlineData("5-0", true)] [InlineData("-500", true)]
    [InlineData("−", true)] [InlineData("–", true)]
    [InlineData("", false)] [InlineData("500", false)] [InlineData("3 000,5", false)]
    public void ContainsMinus_detects_minus_variants(string raw, bool expected)
        => Assert.Equal(expected, Planillium.App.Services.MoneyFormat.ContainsMinus(raw));

    [Theory]
    [InlineData("-500", "500")] [InlineData("5-0", "50")]
    public void Normalize_still_drops_minus(string raw, string expected)
        => Assert.Equal(expected, Planillium.App.Services.MoneyFormat.Normalize(raw, raw.Length, ',').Text.Replace(" ","").Replace(" ",""));
}

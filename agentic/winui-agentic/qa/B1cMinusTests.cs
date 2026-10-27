using Xunit;
using Planillium.App.Services;
namespace Planillium.App.Tests;
// QA-owned (B1c). The minus sign is KEPT as one leading '-' (reverses B1b).
public class B1cMinusTests
{
    [Theory]
    [InlineData("-", "-")] [InlineData("-500", "-500")] [InlineData("5-0", "-50")] [InlineData("--5", "-5")]
    [InlineData("−5", "-5")] [InlineData("–5", "-5")] [InlineData("500", "500")] [InlineData("", "")]
    [InlineData("-3000", "-3 000")]
    public void NormalizeKeepingSign_keeps_one_leading_minus(string raw, string expected)
    {
        var t = MoneyFormat.NormalizeKeepingSign(raw, raw.Length, ',').Text;
        Assert.Equal(expected.Replace(' ',' ').Replace(' ',' ').Replace(' ',' '),
                     t.Replace(' ',' ').Replace(' ',' ').Replace(' ',' '));
    }

    [Theory]
    [InlineData("-", 1)] [InlineData("-5", 2)] [InlineData("5-", 2)]
    public void NormalizeKeepingSign_caret_never_before_sign(string raw, int caret)
    {
        var r = MoneyFormat.NormalizeKeepingSign(raw, caret, ',');
        Assert.True(r.Caret >= 1 && r.Caret <= r.Text.Length, $"caret {r.Caret} text '{r.Text}'");
    }

    [Fact] public void NormalizeKeepingSign_without_minus_equals_Normalize()
        => Assert.Equal(MoneyFormat.Normalize("12345,5", 7, ','), MoneyFormat.NormalizeKeepingSign("12345,5", 7, ','));

    [Fact] public void Deleting_the_minus_gives_a_valid_number()
    {
        var t = MoneyFormat.NormalizeKeepingSign("500", 3, ',').Text;
        Assert.False(MoneyFormat.ContainsMinus(t));
        Assert.Equal(500, MoneyFormat.Parse(t));
    }

    [Fact] public void FromNumber_never_emits_sign() => Assert.DoesNotContain("-", MoneyFormat.FromNumber(0, ','));

    // Documents why MoneyBox.Value must check ContainsMinus first: Parse alone ignores the sign.
    [Fact] public void Parse_ignores_sign_so_callers_must_check_ContainsMinus()
        => Assert.Equal(500, MoneyFormat.Parse("-500"));
}

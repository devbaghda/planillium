using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// LearnActivityRule refuses to teach a bare browser name (2026-07-28 request: differentiate
/// "Chrome - LinkedIn" from "Chrome - Synology" rather than lumping bare "Chrome" into one
/// on/off-plan bucket) — a browser hosts both on-plan and off-plan content depending on the
/// tab, so teaching "Chrome" itself would silently misclassify every kind of browsing.
/// </summary>
[Collection("TestRoot")]
public sealed class ConfigServiceTests
{
    [Fact]
    public void LearnActivityRule_RefusesBareBrowserName()
    {
        var learned = ConfigService.LearnActivityRule("Chrome", DiaryCategory.OnPlan);

        Assert.False(learned);
        var onPlan = ConfigService.Root.GetProperty("activity_rules").GetProperty("on_plan");
        Assert.DoesNotContain(onPlan.EnumerateArray(), v => v.GetString() == "Chrome");
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("Google Chrome")]
    [InlineData("Firefox")]
    [InlineData("Microsoft Edge")]
    public void LearnActivityRule_RefusesEveryKnownBrowserNameCaseInsensitively(string browserName)
    {
        Assert.False(ConfigService.LearnActivityRule(browserName, DiaryCategory.OnPlan));
    }

    [Fact]
    public void LearnActivityRule_AllowsACompoundSiteSpecificKeyword()
    {
        var learned = ConfigService.LearnActivityRule("Chrome - LinkedIn", DiaryCategory.OnPlan);

        Assert.True(learned);
        var onPlan = ConfigService.Root.GetProperty("activity_rules").GetProperty("on_plan");
        Assert.Contains(onPlan.EnumerateArray(), v => v.GetString() == "Chrome - LinkedIn");
    }
}

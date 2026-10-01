using Planillium.App.Services;

namespace Planillium.App.Tests;

[Collection("TestRoot")]
public sealed class DiaryFilterTests
{
    [Fact]
    public void Matches_EmptySet_ReturnsTrue()
    {
        var selected = new HashSet<string>();
        Assert.True(DiaryFilter.Matches(selected, "test", StringComparer.Ordinal));
        Assert.True(DiaryFilter.Matches(selected, null, StringComparer.Ordinal));
    }

    [Fact]
    public void Matches_OneValue_MatchesOnly()
    {
        var selected = new HashSet<string> { "off_plan" };
        Assert.True(DiaryFilter.Matches(selected, "off_plan", StringComparer.Ordinal));
        Assert.False(DiaryFilter.Matches(selected, "on_plan", StringComparer.Ordinal));
        Assert.False(DiaryFilter.Matches(selected, null, StringComparer.Ordinal));
    }

    [Fact]
    public void Matches_TwoValues_MatchesEither()
    {
        var selected = new HashSet<string> { "off_plan", "on_plan" };
        Assert.True(DiaryFilter.Matches(selected, "off_plan", StringComparer.Ordinal));
        Assert.True(DiaryFilter.Matches(selected, "on_plan", StringComparer.Ordinal));
        Assert.False(DiaryFilter.Matches(selected, "neutral", StringComparer.Ordinal));
        Assert.False(DiaryFilter.Matches(selected, null, StringComparer.Ordinal));
    }

    [Fact]
    public void Matches_NullValue_NeverMatches()
    {
        var selected = new HashSet<string> { "tag1", "tag2" };
        Assert.False(DiaryFilter.Matches(selected, null, StringComparer.Ordinal));
    }

    [Fact]
    public void Matches_AppPageCaseInsensitive()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Chrome" };
        Assert.True(DiaryFilter.Matches(selected, "chrome", StringComparer.OrdinalIgnoreCase));
        Assert.True(DiaryFilter.Matches(selected, "CHROME", StringComparer.OrdinalIgnoreCase));
        Assert.True(DiaryFilter.Matches(selected, "Chrome", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Matches_CategoryTagCaseSensitive()
    {
        var selected = new HashSet<string>(StringComparer.Ordinal) { "off_plan" };
        Assert.True(DiaryFilter.Matches(selected, "off_plan", StringComparer.Ordinal));
        Assert.False(DiaryFilter.Matches(selected, "Off_Plan", StringComparer.Ordinal));
        Assert.False(DiaryFilter.Matches(selected, "OFF_PLAN", StringComparer.Ordinal));
    }

    [Fact]
    public void ButtonLabel_EmptySet_ReturnsAllLabel()
    {
        var result = DiaryFilter.ButtonLabel("All categories", new List<string>());
        Assert.Equal("All categories", result);
    }

    [Fact]
    public void ButtonLabel_OneLabel_ReturnsThatLabel()
    {
        var result = DiaryFilter.ButtonLabel("All categories", new List<string> { "Off-plan" });
        Assert.Equal("Off-plan", result);
    }

    [Fact]
    public void ButtonLabel_TwoLabels_ReturnsFirstPlusCount()
    {
        var result = DiaryFilter.ButtonLabel("All categories", new List<string> { "Off-plan", "On-plan" });
        Assert.Equal("Off-plan +1", result);
    }

    [Fact]
    public void ButtonLabel_ThreeLabels_ReturnsFirstPlusCount()
    {
        var result = DiaryFilter.ButtonLabel("All categories",
            new List<string> { "Off-plan", "On-plan", "Neutral" });
        Assert.Equal("Off-plan +2", result);
    }

    [Fact]
    public void PruneToAvailable_RemovesAbsent_KeepsSurvivors()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Chrome", "Firefox", "Safari" };
        var available = new[] { "Chrome", "Firefox" };
        var result = DiaryFilter.PruneToAvailable(selected, available);

        Assert.Equal(2, result.Count);
        Assert.True(result.Contains("Chrome", StringComparer.OrdinalIgnoreCase));
        Assert.True(result.Contains("Firefox", StringComparer.OrdinalIgnoreCase));
        Assert.False(result.Contains("Safari", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void PruneToAvailable_AllAbsent_ReturnsEmpty()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Chrome", "Firefox" };
        var available = new[] { "Safari", "Edge" };
        var result = DiaryFilter.PruneToAvailable(selected, available);

        Assert.Empty(result);
    }

    [Fact]
    public void PruneToAvailable_EmptySelected_ReturnsEmpty()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var available = new[] { "Chrome", "Firefox" };
        var result = DiaryFilter.PruneToAvailable(selected, available);

        Assert.Empty(result);
    }

    [Fact]
    public void PruneToAvailable_CaseInsensitive()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Chrome", "Firefox" };
        var available = new[] { "chrome", "firefox" };
        var result = DiaryFilter.PruneToAvailable(selected, available);

        Assert.Equal(2, result.Count);
        Assert.True(result.Contains("Chrome"));
        Assert.True(result.Contains("Firefox"));
    }

    [Fact]
    public void SameOptionSet_IdenticalLists_ReturnsTrue()
    {
        var current = new List<string> { "Chrome", "Firefox", "Safari" };
        var newOptions = new List<string> { "Chrome", "Firefox", "Safari" };
        Assert.True(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameOptionSet_DifferentOrder_ReturnsTrue()
    {
        var current = new List<string> { "Chrome", "Firefox", "Safari" };
        var newOptions = new List<string> { "Safari", "Chrome", "Firefox" };
        Assert.True(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameOptionSet_DifferentCount_ReturnsFalse()
    {
        var current = new List<string> { "Chrome", "Firefox" };
        var newOptions = new List<string> { "Chrome", "Firefox", "Safari" };
        Assert.False(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameOptionSet_DifferentValues_ReturnsFalse()
    {
        var current = new List<string> { "Chrome", "Firefox" };
        var newOptions = new List<string> { "Chrome", "Safari" };
        Assert.False(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameOptionSet_CaseInsensitive()
    {
        var current = new List<string> { "Chrome", "Firefox" };
        var newOptions = new List<string> { "chrome", "firefox" };
        Assert.True(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameOptionSet_CaseSensitive()
    {
        var current = new List<string> { "Chrome", "Firefox" };
        var newOptions = new List<string> { "chrome", "firefox" };
        Assert.False(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.Ordinal));
    }

    [Fact]
    public void SameOptionSet_Empty()
    {
        var current = new List<string>();
        var newOptions = new List<string>();
        Assert.True(DiaryFilter.SameOptionSet(current, newOptions, StringComparer.Ordinal));
    }
}

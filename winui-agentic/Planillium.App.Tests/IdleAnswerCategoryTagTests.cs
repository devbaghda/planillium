using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// ActivityTracker.LogIdleAnswer/LogIdleAnswers now take an explicit category and tag
/// (2026-08-07 request: IdleReturnDialog gave no field for either — category was silently
/// re-derived from the typed text with ClassifyIdleText, and tag didn't exist there at all).
/// Covers the one thing that isn't visible by reading the code: that what's passed in is what
/// actually gets written, not re-guessed from the description on the way to the database.
/// </summary>
[Collection("TestRoot")]
public sealed class IdleAnswerCategoryTagTests
{
    [Fact]
    public void LogIdleAnswer_PersistsTheCategoryPassedIn_NotAReguessFromText()
    {
        var desc = "idle-single-" + Guid.NewGuid();
        var tracker = new ActivityTracker(ConfigService.Root);
        var start = DateTime.Today.AddHours(9);

        // Plain text with no keyword-list match would auto-classify to Idle under the old
        // ClassifyIdleText-inside-LogIdleAnswer behavior — passing OnPlan explicitly and
        // getting OnPlan back proves the category now comes from the caller.
        tracker.LogIdleAnswer(start, 30, desc, DiaryCategory.OnPlan, DiaryTag.SelfDev);

        var row = ReportData.DiaryInRange(DateOnly.FromDateTime(start), DateOnly.FromDateTime(start))
            .Single(e => e.Desc == desc);
        Assert.Equal(DiaryCategory.OnPlan, row.Cat);
        Assert.Equal(DiaryTag.SelfDev, row.Tag);
    }

    [Fact]
    public void LogIdleAnswer_DefaultsToNullTag_WhenNotSpecified()
    {
        var desc = "idle-single-notag-" + Guid.NewGuid();
        var tracker = new ActivityTracker(ConfigService.Root);
        var start = DateTime.Today.AddHours(9).AddMinutes(31);

        tracker.LogIdleAnswer(start, 10, desc, DiaryCategory.Neutral);

        var row = ReportData.DiaryInRange(DateOnly.FromDateTime(start), DateOnly.FromDateTime(start))
            .Single(e => e.Desc == desc);
        Assert.Equal(DiaryCategory.Neutral, row.Cat);
        Assert.Null(row.Tag);
    }

    [Fact]
    public void LogIdleAnswers_PersistsEachSegmentsOwnCategoryAndTag()
    {
        var desc1 = "idle-split-1-" + Guid.NewGuid();
        var desc2 = "idle-split-2-" + Guid.NewGuid();
        var tracker = new ActivityTracker(ConfigService.Root);
        var start = DateTime.Today.AddHours(10);

        tracker.LogIdleAnswers(new (DateTime, int, string, string, string?)[]
        {
            (start, 15, desc1, DiaryCategory.OffPlan, DiaryTag.Procrastination),
            (start.AddMinutes(15), 15, desc2, DiaryCategory.Neutral, null),
        });

        var rows = ReportData.DiaryInRange(DateOnly.FromDateTime(start), DateOnly.FromDateTime(start));
        var row1 = rows.Single(e => e.Desc == desc1);
        var row2 = rows.Single(e => e.Desc == desc2);
        Assert.Equal(DiaryCategory.OffPlan, row1.Cat);
        Assert.Equal(DiaryTag.Procrastination, row1.Tag);
        Assert.Equal(DiaryCategory.Neutral, row2.Cat);
        Assert.Null(row2.Tag);
    }
}

using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// Covers the tray unread-dot recap added 2026-07-22: clicking the tray icon used to just open
/// the app with no way to see what the red dot had actually been about. Record()/TakePending()
/// are the two operations that fix that — this exercises them directly rather than through the
/// WinUI dialog, which needs a XamlRoot this project can't construct (see the csproj's own
/// comment on why it links plain-C# files instead of referencing the WinUI project).
/// StateService's AppState is cached in a static field for the process lifetime, shared by every
/// test in this collection (same reasoning as TestRootFixture's doc comment for ScoreService) —
/// each test below drains any leftover state first rather than assuming a clean slate.
/// </summary>
[Collection("TestRoot")]
public sealed class NotificationCenterTests
{
    public NotificationCenterTests(TestRootFixture _) => NotificationCenter.TakePending();

    [Fact]
    public void Record_MarksUnreadAndPreservesOrderAndContent()
    {
        NotificationCenter.Record("Off-plan alert", "You've been off-plan for 15 minutes.");
        NotificationCenter.Record("Off-plan alert", "You've been off-plan for 25 minutes.");

        Assert.True(NotificationCenter.HasUnread);

        var pending = NotificationCenter.TakePending();
        Assert.Equal(2, pending.Count);
        Assert.Equal("You've been off-plan for 15 minutes.", pending[0].Message);
        Assert.Equal("You've been off-plan for 25 minutes.", pending[1].Message);
    }

    [Fact]
    public void TakePending_ClearsBothUnreadFlagAndList()
    {
        NotificationCenter.Record("Test", "Body");
        Assert.True(NotificationCenter.HasUnread);

        var first = NotificationCenter.TakePending();
        Assert.Single(first);
        Assert.False(NotificationCenter.HasUnread);

        // A second call with nothing new recorded in between must come back empty —
        // this is what stops the recap dialog from reopening on every later activation.
        var second = NotificationCenter.TakePending();
        Assert.Empty(second);
    }

    [Fact]
    public void Record_CapsPendingListAndDropsOldestFirst()
    {
        for (var i = 0; i < 15; i++)
            NotificationCenter.Record($"Alert {i}", $"Message {i}");

        var pending = NotificationCenter.TakePending();
        Assert.Equal(10, pending.Count);
        Assert.Equal("Message 5", pending[0].Message);
        Assert.Equal("Message 14", pending[^1].Message);
    }
}

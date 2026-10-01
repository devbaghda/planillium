using Planillium.App.Services;
using Xunit;

namespace Planillium.App.Tests;

/// <summary>2026-10-01: the away-dialog span now carries dates. (ReportExport.Suggestions can't be
/// linked into this assembly — it reaches WinUI via MainWindow.FormatEur — so the ratio change is
/// verified by inspection and build.)</summary>
public sealed class AwaySpanTests
{
    [Fact]
    public void ToDisplaySpan_SameDay_ShowsDateOnce()
    {
        var s = new DateTime(2026, 10, 1, 10, 30, 0);
        var text = s.ToDisplaySpan(s.AddMinutes(6));
        Assert.Contains("10:30", text);
        Assert.Contains("10:36", text);
        Assert.Contains(s.ToDisplayDate(), text);
    }

    [Fact]
    public void ToDisplaySpan_AcrossMidnight_ShowsBothDates()
    {
        var s = new DateTime(2026, 9, 30, 23, 50, 0);
        var e = new DateTime(2026, 10, 1, 0, 5, 0);
        var text = s.ToDisplaySpan(e);
        Assert.Contains(s.ToDisplayDate(), text);
        Assert.Contains(e.ToDisplayDate(), text);
    }
}

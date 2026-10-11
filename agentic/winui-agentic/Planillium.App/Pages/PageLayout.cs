using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Planillium.App.Pages;

/// <summary>
/// Keeps a page's content column at one fixed width, centered, regardless of what's rendered
/// inside it (2026-08-05 request: "fix width on all of the pages").
///
/// The pattern every page but Reports and Settings used — a <c>StackPanel</c> with
/// <c>HorizontalAlignment="Center"</c> and a <c>MaxWidth</c>, inside a <c>ScrollViewer</c> — does
/// NOT do this: a StackPanel's natural width is its own content's desired width, and MaxWidth only
/// caps that, it doesn't force it. So the column's actual width, and therefore its centered
/// position, silently tracks whatever happens to be on screen: Schedule's day cards, Today's task
/// list, Plans' cards. Switch to a page with taller/wider content and the whole column visibly
/// shifts — the "not fixed" the user is pointing at. This was already root-caused and fixed once,
/// for Reports (2026-07-28, ReportsPage's own RootScroller_SizeChanged): a WinUI ScrollViewer/
/// ScrollContentPresenter quirk where a Stretch- or Center-aligned child's arrange width isn't
/// fully independent of its content's extent. The one thing that DOES hold still is the
/// ScrollViewer's own ActualWidth, set top-down by the window/nav pane — this computes width and
/// centering from THAT, never from the content, and is now the one implementation every page
/// shares instead of four copies of the same SizeChanged math (Reports' own version folded into
/// this at the same time).
///
/// Wire-up, per page:
///   1. The ScrollViewer needs an x:Name and SizeChanged="..." wired to a one-line handler here.
///   2. Its content StackPanel needs HorizontalAlignment="Left" (NOT Center) and no MaxWidth of
///      its own — both would fight this class's own Width/Margin assignment below.
///   3. The handler body is just: <c>PageLayout.CenterContent(Scroller, ContentColumn, MaxWidth, e.NewSize);</c>
/// </summary>
internal static class PageLayout
{
    internal static void CenterContent(ScrollViewer scroller, FrameworkElement content,
        double maxWidth, Windows.Foundation.Size newSize)
    {
        var available = newSize.Width - scroller.Padding.Left - scroller.Padding.Right;
        var width = Math.Min(maxWidth, Math.Max(0, available));
        content.Width = width;
        content.Margin = new Thickness(Math.Max(0, (available - width) / 2), 0, 0, 0);
    }
}

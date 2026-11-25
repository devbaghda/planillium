namespace Planillium.App.Pages;

// Code replacements for the WinUI .xaml files of the small pages: they create the elements the
// page code refers to by name and wire the same handlers. Layout values mirror the XAML.
internal static class Shell
{
    public static InfoBar SaveError(double bottom) => new()
    {
        Severity = InfoBarSeverity.Error, IsOpen = false, IsClosable = true,
        Title = "Couldn't save that change",
        Message = "The database was briefly busy — try again in a moment.",
        Margin = new Thickness(0, 0, 0, bottom),
    };

    public static TextBlock SectionLabel(string text) => new()
    {
        Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold, CharacterSpacing = 60,
        Margin = new Thickness(2, 26, 0, 8), Foreground = Ui.Muted,
    };

    public static (ScrollViewer scroller, StackPanel column) Frame(Thickness padding, double spacing)
    {
        var column = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Spacing = spacing };
        var scroller = new ScrollViewer { Padding = padding, Content = column };
        return (scroller, column);
    }
}

public sealed partial class TodayPage
{
    private ScrollViewer RootScroller = null!;
    private StackPanel ContentColumn = null!;
    private TextBlock Subtitle = null!;
    private InfoBar OverdueBar = null!;
    private Button ReplanButton = null!;
    private InfoBar SaveErrorBar = null!;
    private StackPanel Sections = null!;
    private StackPanel TickTickHost = null!;

    private void InitializeComponent()
    {
        var (scroller, col) = Shell.Frame(new Thickness(28, 8, 28, 28), 0);
        RootScroller = scroller; ContentColumn = col;
        var header = new Grid { Margin = new Thickness(0, 8, 0, 2), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Ui.Title("Today"));
        var review = new Button { Content = "Evening review", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(review, 1);
        review.Click += Review_Click;
        header.Children.Add(review);
        Subtitle = new TextBlock { Foreground = Ui.Secondary, Margin = new Thickness(0, 0, 0, 16) };
        ReplanButton = new Button { Content = "Replan all overdue…" };
        ReplanButton.Click += Replan_Click;
        OverdueBar = new InfoBar
        {
            Severity = InfoBarSeverity.Warning, IsOpen = false, IsClosable = false,
            Margin = new Thickness(0, 0, 0, 8), ActionButton = ReplanButton,
        };
        SaveErrorBar = Shell.SaveError(8);
        Sections = new StackPanel();
        TickTickHost = new StackPanel();
        foreach (var c in new Control[] { header, Subtitle, OverdueBar, SaveErrorBar, Sections, TickTickHost })
            col.Children.Add(c);
        scroller.SizeChanged += (s, e) => RootScroller_SizeChanged(s!, e);
        Content = scroller;
    }
}

public sealed partial class SchedulePage
{
    private ScrollViewer Scroller = null!;
    private StackPanel ContentColumn = null!;
    private TextBlock Subtitle = null!;
    private InfoBar SaveErrorBar = null!;
    private StackPanel Sections = null!;

    private void InitializeComponent()
    {
        var (scroller, col) = Shell.Frame(new Thickness(32, 24, 32, 24), 8);
        Scroller = scroller; ContentColumn = col;
        Subtitle = new TextBlock { Foreground = Ui.Secondary, Margin = new Thickness(0, 0, 0, 12) };
        SaveErrorBar = Shell.SaveError(8);
        Sections = new StackPanel { Spacing = 12 };
        foreach (var c in new Control[] { Ui.Title("Schedule"), Subtitle, SaveErrorBar, Sections })
            col.Children.Add(c);
        scroller.SizeChanged += (s, e) => Scroller_SizeChanged(s!, e);
        Content = scroller;
    }
}

public sealed partial class PlansPage
{
    private ScrollViewer RootScroller = null!;
    private StackPanel ContentColumn = null!;
    private Button AddButton = null!;
    private InfoBar LoadErrorBar = null!;
    private InfoBar SaveErrorBar = null!;
    private StackPanel ActiveList = null!;
    private StackPanel QueuedList = null!;
    private StackPanel ArchivedList = null!;

    private void InitializeComponent()
    {
        var (scroller, col) = Shell.Frame(new Thickness(28, 8, 28, 28), 0);
        RootScroller = scroller; ContentColumn = col;
        var header = new Grid { Margin = new Thickness(0, 8, 0, 16), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Ui.Title("Plans"));
        AddButton = new Button { Content = "+ Add Plan" };
        AddButton.Classes.Add("accent");
        Grid.SetColumn(AddButton, 1);
        AddButton.Click += Add_Click;
        header.Children.Add(AddButton);
        LoadErrorBar = new InfoBar { Severity = InfoBarSeverity.Warning, IsClosable = true, Margin = new Thickness(0, 0, 0, 12) };
        SaveErrorBar = Shell.SaveError(12);
        ActiveList = new StackPanel { Spacing = 10 };
        QueuedList = new StackPanel { Spacing = 6 };
        ArchivedList = new StackPanel { Spacing = 6 };
        foreach (var c in new Control[] { header, LoadErrorBar, SaveErrorBar, ActiveList,
                     Shell.SectionLabel("QUEUED IDEAS"), QueuedList, Shell.SectionLabel("ARCHIVED"), ArchivedList })
            col.Children.Add(c);
        scroller.SizeChanged += (s, e) => RootScroller_SizeChanged(s!, e);
        Content = scroller;
    }
}

public sealed partial class ReportsPage
{
    private ScrollViewer RootScroller = null!;
    private StackPanel ContentColumn = null!;
    private MenuFlyoutItem ExportCsvItem = null!;
    private InfoBar SaveErrorBar = null!;
    private RadioButtons PeriodBar = null!;
    private StackPanel Body = null!;

    private void InitializeComponent()
    {
        var (scroller, col) = Shell.Frame(new Thickness(28, 8, 28, 28), 0);
        RootScroller = scroller; ContentColumn = col;
        var header = new Grid { Margin = new Thickness(0, 8, 0, 10) };
        header.Children.Add(Ui.Title("Reports"));
        var html = new MenuFlyoutItem { Text = "HTML report (week)" };
        html.Click += ExportHtml_Click;
        ExportCsvItem = new MenuFlyoutItem { Text = "CSV (this period)" };
        ExportCsvItem.Click += ExportCsv_Click;
        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Items.Add(html);
        flyout.Items.Add(ExportCsvItem);
        header.Children.Add(new DropDownButton
        {
            Content = "Export", Flyout = flyout,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
        });
        SaveErrorBar = Shell.SaveError(8);
        PeriodBar = new RadioButtons { MaxColumns = 4, Margin = new Thickness(0, 0, 0, 4) };
        Body = new StackPanel();
        foreach (var c in new Control[] { header, SaveErrorBar, PeriodBar, Body })
            col.Children.Add(c);
        scroller.SizeChanged += (s, e) => RootScroller_SizeChanged(s!, e);
        Content = scroller;
    }
}

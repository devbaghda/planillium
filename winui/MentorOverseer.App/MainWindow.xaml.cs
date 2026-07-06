using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Pages;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Mentor Overseer";

        // Mica ground; falls back to the theme's solid color on unsupported OS.
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));

        ContentFrame.Navigate(typeof(TodayPage));
        RefreshScore();
    }

    public void RefreshScore()
    {
        try
        {
            using var db = new Database();
            ScoreValue.Text = db.ScoreBalance().ToString();
        }
        catch
        {
            ScoreValue.Text = "—";
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(StubPage), "Settings");
            return;
        }
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        switch (tag)
        {
            case "today":    ContentFrame.Navigate(typeof(TodayPage)); break;
            case "schedule": ContentFrame.Navigate(typeof(StubPage), "Schedule"); break;
            case "reports":  ContentFrame.Navigate(typeof(StubPage), "Reports"); break;
            case "plans":    ContentFrame.Navigate(typeof(StubPage), "Plans"); break;
        }
    }
}

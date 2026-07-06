using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _initialising = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var theme = StateService.Load().Theme;
            ThemeChoice.SelectedIndex = theme switch
            {
                "light" => 1, "dark" => 2, _ => 0,
            };
            _initialising = false;

            var win = App.MainWindow as MainWindow;
            TrackerInfo.Text = win?.Tracker is { Running: true }
                ? "This app is tracking your activity (60s polls, diary 06:00–20:00)."
                : "Tracking is off in this app — the Python app was already running " +
                  "and only one tracker may write the diary at a time.";
            DataInfo.Text = "Shared data folder: " + AppPaths.Root;
        };
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initialising) return;
        var tag = (ThemeChoice.SelectedItem as RadioButton)?.Tag as string ?? "default";

        var state = StateService.Load();
        state.Theme = tag;
        StateService.Save(state);

        if ((App.MainWindow as MainWindow)?.Content is FrameworkElement root)
            root.RequestedTheme = tag switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
    }
}

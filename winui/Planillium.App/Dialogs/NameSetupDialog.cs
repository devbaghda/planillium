using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// First-launch only: how the app should address you. Kickoff and other
/// mentor-voice moments read this back via ConfigService.UserName.
/// </summary>
public static class NameSetupDialog
{
    // Two independent code paths need this dialog to have finished before
    // they proceed: MainWindow's constructor (tracking must not start until
    // the disclosure in this dialog has been shown/dismissed) and TodayPage's
    // first navigation (Kickoff should follow Name, not race ahead of it).
    // Caching the in-flight Task means whichever path gets here first is the
    // one that actually shows the dialog; the other awaits the same
    // completion instead of triggering a second, duplicate dialog.
    private static Task? _pending;

    public static bool ShouldShow() =>
        ConfigService.UserName.Length == 0 && !StateService.Load().NameAsked;

    public static Task EnsureShownAsync(MainWindow window) =>
        ShouldShow() ? _pending ??= ShowAsync(window) : Task.CompletedTask;

    public static async Task ShowAsync(MainWindow window)
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = "One thing first — what should I call you?",
            TextWrapping = TextWrapping.Wrap,
        });
        var input = new TextBox { PlaceholderText = "e.g. Alex", Header = "Your name" };
        panel.Children.Add(input);
        panel.Children.Add(new TextBlock
        {
            Text = "One more thing: this app tracks your active window every 60s to " +
                   "measure on-plan time. Everything it tracks stays local on this " +
                   "machine — reminder notifications may briefly appear in Windows' " +
                   "own notification area, so turn off notification sync to other " +
                   "devices if that matters to you. Right-click the tray icon and " +
                   "choose \"Pause tracking\" to stop temporarily without closing the " +
                   "app, or quit from the tray icon to stop it entirely. Diary detail " +
                   "ages out after 90 days by default (adjustable in Settings, which " +
                   "also has Export/Clear actions for everything tracked).",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var dialog = DialogControls.Build(window.Content.XamlRoot, $"Welcome to {AppInfo.DisplayName}", panel,
            primaryButtonText: "Continue", defaultButton: ContentDialogButton.Primary);

        await DialogGate.ShowAsync(dialog);

        // A blank answer isn't a hard stop — kickoff/etc. fall back to a
        // neutral greeting, and NameAsked (not UserName) is what prevents
        // this from nagging again every launch.
        var name = input.Text.Trim();
        try
        {
            if (name.Length > 0)
                ConfigService.Mutate(cfg => cfg["user_name"] = name);
        }
        catch (Exception ex)
        {
            Log.Error("NameSetupDialog", ex);
        }

        var state = StateService.Load();
        state.NameAsked = true;
        StateService.Save(state);
    }
}

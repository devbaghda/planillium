using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// First-launch only: how the app should address you. Kickoff and other
/// mentor-voice moments read this back via ConfigService.UserName.
/// </summary>
public static class NameSetupDialog
{
    public static bool ShouldShow() =>
        ConfigService.UserName.Length == 0 && !StateService.Load().NameAsked;

    public static async Task ShowAsync(MainWindow window)
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = "One thing first — what should I call you?",
            TextWrapping = TextWrapping.Wrap,
        });
        var input = new TextBox { PlaceholderText = "e.g. the user" };
        panel.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = $"Welcome to {AppInfo.DisplayName}",
            Content = panel,
            PrimaryButtonText = "Continue",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

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

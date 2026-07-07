using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// "Welcome back" — the softened replacement for the Python app's blocking
/// idle interrogation: one-tap chips for the common cases, typing optional,
/// dismissible. Logs through ActivityTracker.LogIdleAnswer, so the idle-
/// answer library still reclassifies matched text.
/// </summary>
public static class IdleReturnDialog
{
    private static readonly string[] QuickChips =
        { "Lunch", "Break", "Errand", "Work off-screen" };

    public static async Task ShowAsync(MainWindow window, int idleMinutes, DateTime idleStart)
    {
        if (window.Tracker is not { } tracker) return;

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(new TextBlock
        {
            Text = $"You were away {idleMinutes} min. What was it, roughly?",
            TextWrapping = TextWrapping.Wrap,
        });

        var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var input = new TextBox
        {
            PlaceholderText = "…or type it (matches your idle-answer library)",
        };

        string? chosen = null;
        ContentDialog dialog = null!;
        foreach (var chip in QuickChips)
        {
            var b = new Button { Content = chip };
            b.Click += (_, _) => { chosen = chip; dialog.Hide(); };
            chipRow.Children.Add(b);
        }
        panel.Children.Add(chipRow);
        panel.Children.Add(input);

        dialog = new ContentDialog
        {
            Title = "Welcome back",
            Content = panel,
            PrimaryButtonText = "Log it",
            CloseButtonText = "Skip",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await DialogGate.ShowAsync(dialog);
        var text = chosen ?? (result == ContentDialogResult.Primary ? input.Text.Trim() : "");
        tracker.LogIdleAnswer(idleStart, idleMinutes,
            text.Length > 0 ? text : "dismissed");
    }
}

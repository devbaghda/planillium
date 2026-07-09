using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Models;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// The task's full explanation — "detail" (the concrete how-to) and
/// "mentor_note" (why it matters / the common mistake), the two fields the
/// plan-generation templates always ask for but that never got a dedicated
/// view of their own (the Today row only ever had room for a one-line
/// caption). Opened from a "Details" link, shown only when there's more to
/// read than the row already fits.
/// </summary>
public static class TaskDetailDialog
{
    public static async Task ShowAsync(XamlRoot xamlRoot, PlanTask task)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 380, MaxWidth = 480 };

        if (task.Category is { Length: > 0 } cat || task.DurationMin is int mins)
        {
            var meta = new List<string>();
            if (task.Category is { Length: > 0 } c) meta.Add(c);
            if (task.DurationMin is int m) meta.Add($"{m} min");
            panel.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", meta),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
        }

        if (task.Detail is { Length: > 0 } detail)
        {
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(new TextBlock
            {
                Text = "WHAT TO DO",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 60,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
            block.Children.Add(new TextBlock
            {
                Text = detail,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            });
            panel.Children.Add(block);
        }

        if (task.MentorNote is { Length: > 0 } note)
        {
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(new TextBlock
            {
                Text = "MENTOR'S NOTE",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 60,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            });
            block.Children.Add(new TextBlock
            {
                Text = note,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            });
            panel.Children.Add(block);
        }

        var dialog = new ContentDialog
        {
            Title = task.Text,
            Content = panel,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        await DialogGate.ShowAsync(dialog);
    }
}

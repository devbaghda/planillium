using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Models;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// The plan-level strategic briefing (high-leverage items, what to ignore,
/// common time-wasters, realistic timeline) that the Claude wizard generates
/// alongside every plan — carried in the plan JSON but, until now, had no
/// view of its own in the WinUI app (the Python app's
/// _show_plan_briefing_dialog equivalent).
/// </summary>
public static class BriefingDialog
{
    public static async Task ShowAsync(XamlRoot xamlRoot, Plan plan)
    {
        var b = plan.Briefing;
        if (b is null) return;

        var panel = new StackPanel { Spacing = 14, MinWidth = 420, MaxWidth = 520 };

        void Section(string heading, string? text)
        {
            if (text is not { Length: > 0 }) return;
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(new TextBlock
            {
                Text = heading,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 60,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
            block.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(block);
        }

        if (b.HighLeverage.Count > 0)
        {
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(new TextBlock
            {
                Text = "THE 20% THAT MATTERS",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 60,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            });
            foreach (var item in b.HighLeverage)
                block.Children.Add(new TextBlock { Text = "• " + item, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(block);
        }
        Section("SKIP THIS ENTIRELY", b.IgnoreCompletely);
        Section("WHERE MOST PEOPLE WASTE TIME", b.CommonTimeWasters);
        Section("REALISTIC TIMELINE", b.RealisticTimeline);

        var dialog = new ContentDialog
        {
            Title = $"{plan.Name} — briefing",
            Content = new ScrollViewer { Content = panel, MaxHeight = 480 },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        await DialogGate.ShowAsync(dialog);
    }
}

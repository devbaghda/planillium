using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Planillium.App.Views;

/// <summary>
/// One sidebar card in MainWindow's plan-drift panel — pulled out of
/// MainWindow.Startup.cs's RefreshPlanDrift, which otherwise mixes UI-building
/// code into a file whose job is background-job/watcher coordination, unlike
/// every other chunk of built-up UI in this app (EmptyPlansState, TaskDetailsLink,
/// TaskNoteView all already live here) (2026-07-24 audit finding #10).
/// </summary>
public static class PlanDriftCard
{
    public static Border Build(string planName, int driftDays, string finishDateText)
    {
        var status = driftDays switch
        {
            > 0 => $"{driftDays}d late from plan",
            < 0 => $"{-driftDays}d ahead of plan",
            _ => "On track",
        };

        var nameBlock = new TextBlock
        {
            Text = planName,
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(nameBlock, planName);

        var block = new StackPanel { Spacing = 1 };
        block.Children.Add(nameBlock);
        block.Children.Add(new TextBlock
        {
            Text = status,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources[
                driftDays > 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"],
        });
        block.Children.Add(new TextBlock
        {
            Text = "Finishes " + finishDateText,
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        // Same subtle-fill chrome the activity pill above it uses, so the
        // footer reads as one deliberate widget stack rather than bare text
        // bolted under two chip-styled ones.
        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            Child = block,
        };
    }
}

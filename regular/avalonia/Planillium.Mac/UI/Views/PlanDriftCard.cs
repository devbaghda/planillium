
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
        var nameBlock = new TextBlock
        {
            Text = planName,
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Ui.SetTip(nameBlock, planName);

        var block = new StackPanel { Spacing = 1 };
        block.Children.Add(nameBlock);
        // Only "Nd late" / "Nd ahead" carries the status colour; "from plan" stays regular text.
        var statusBlock = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold };
        var ok = "SystemFillColorSuccessBrush";
        Ui.StatusRuns(statusBlock, "TextFillColorPrimaryBrush", driftDays switch
        {
            > 0 => new[] { ($"{driftDays}d late", (string?)"SystemFillColorCriticalBrush"), (" from plan", null) },
            < 0 => new[] { ($"{-driftDays}d ahead", (string?)ok), (" of plan", null) },
            _ => new[] { ("On track", (string?)ok) },
        });
        block.Children.Add(statusBlock);
        block.Children.Add(new TextBlock
        {
            Text = "Finishes " + finishDateText,
            FontSize = 11,
            Foreground = Ui.Brush("TextFillColorSecondaryBrush"),
        });

        // Same subtle-fill chrome the activity pill above it uses, so the
        // footer reads as one deliberate widget stack rather than bare text
        // bolted under two chip-styled ones.
        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Background = Ui.Brush("SubtleFillColorSecondaryBrush"),
            Child = block,
        };
    }
}

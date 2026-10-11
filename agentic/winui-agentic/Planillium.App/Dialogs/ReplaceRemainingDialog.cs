using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Planillium.App.Dialogs;

/// <summary>
/// "Replace remaining tasks…" on an active plan (spec replace-remaining-tasks, Design D2-D5).
/// Step 1 shows a prompt listing the ticked tasks and takes Claude's reply. Step 2 shows the
/// preview and asks for confirmation. Nothing is written until step 2's "Replace tasks".
/// </summary>
public static class ReplaceRemainingDialog
{
    // Same width constants and reasoning as AddPlanDialog.
    private const double DialogWidth = 640;
    private const double DialogContentWidth = DialogWidth - 64;

    private const string ApplyFailedMessage =
        "Couldn't write the plan file — nothing was changed. Check the log and try again.";
    private const string StartChangedMessage =
        "The date changed while this was open. The figures above are updated — check them and press Replace tasks again.";

    private sealed record Step1Result(string ReplyText, PlanReplacement.Replacement Replacement);

    private enum Step2Outcome { Cancel, Back, Applied }

    /// <returns>True once the plan has been replaced; the caller must then re-render the page.</returns>
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, string planId)
    {
        PlanReplacement.Situation situation;
        using (var db = new Database())
            situation = PlanReplacement.Assess(planId, db);

        if (!PlanReplacement.HasSomethingToReplace(situation))
        {
            var refused = DialogControls.Build(xamlRoot, "Nothing to replace",
                $"Every task in '{situation.Plan.Name}' is already done, so there is nothing left to replace.",
                closeButtonText: "OK");
            await DialogGate.ShowAsync(refused);
            return false;
        }

        var replyText = "";
        while (true)
        {
            using (var db = new Database())
                situation = PlanReplacement.Assess(planId, db);

            var step1 = await ShowStep1Async(xamlRoot, situation, replyText);
            if (step1 is null) return false;
            replyText = step1.ReplyText;

            var step2 = await ShowStep2Async(xamlRoot, planId, step1.Replacement);
            if (step2 == Step2Outcome.Back) continue;
            return step2 == Step2Outcome.Applied;
        }
    }

    /// <summary>Step 1: prompt, copy, paste. Returns null when cancelled. An invalid paste keeps
    /// the dialog open with the reason shown (args.Cancel) and the typed text untouched.</summary>
    private static async Task<Step1Result?> ShowStep1Async(XamlRoot xamlRoot,
        PlanReplacement.Situation situation, string replyText)
    {
        var summary = new TextBlock
        {
            Text = SummaryText(situation),
            FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = DialogContentWidth - 40,
        };

        var caption1 = new TextBlock
        {
            Text = "1. Copy this prompt into claude.ai.",
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };

        var prompt = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 110,
            Header = "Prompt for Claude",
            Text = PlanTemplates.ReplaceRemainder(situation.Plan.Name, situation.Done, situation.StartDay),
        };
        AutomationProperties.SetName(prompt, "Prompt for Claude");

        var copyBtn = new Button { Content = "Copy", HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(copyBtn, "Copy");
        copyBtn.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(prompt.Text);
            Clipboard.SetContent(dp);
            copyBtn.Content = "Copied ✓";
        };

        var caption2 = new TextBlock
        {
            Text = "2. Paste Claude's reply below.",
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };

        var reply = new TextBox
        {
            AcceptsReturn = true,
            Height = 110,
            TextWrapping = TextWrapping.Wrap,
            Header = "Claude's reply",
            PlaceholderText = "Paste Claude's whole reply (with the ```json block) here",
            Text = replyText,
        };
        AutomationProperties.SetName(reply, "Claude's reply");

        var error = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        // Editing the reply clears the previous problem, as the user is now fixing it.
        reply.TextChanged += (_, _) => error.Visibility = Visibility.Collapsed;

        var panel = new StackPanel { Spacing = 10, MinWidth = DialogContentWidth };
        panel.Children.Add(summary);
        panel.Children.Add(caption1);
        panel.Children.Add(prompt);
        panel.Children.Add(copyBtn);
        panel.Children.Add(caption2);
        panel.Children.Add(reply);
        panel.Children.Add(error);

        var doneTitles = situation.Done.Select(d => d.Title).ToList();
        var dialog = DialogControls.Build(xamlRoot, "Replace remaining tasks",
            new ScrollViewer { Content = panel, MaxHeight = 560 },
            primaryButtonText: "Preview", closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Primary);
        dialog.Resources["ContentDialogMaxWidth"] = DialogWidth;

        PlanReplacement.Replacement? parsed = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var problem = PlanReplacement.ParseReplacement(reply.Text, doneTitles, out var replacement);
            if (problem is not null || replacement is null)
            {
                ShowError(error, problem ?? "Couldn't read the reply. Paste it again.");
                args.Cancel = true;
                return;
            }
            parsed = replacement;
        };

        var result = await DialogGate.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary || parsed is null) return null;
        return new Step1Result(reply.Text, parsed);
    }

    /// <summary>Step 2: preview and confirm. The default button is Cancel, so Enter cannot
    /// confirm a destructive change. Figures are recomputed on open and again at confirm.</summary>
    private static async Task<Step2Outcome> ShowStep2Async(XamlRoot xamlRoot, string planId,
        PlanReplacement.Replacement replacement)
    {
        PlanReplacement.Situation current;
        using (var db = new Database())
            current = PlanReplacement.Assess(planId, db);

        var headline = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var keptValue = FactValue();
        var startValue = FactValue();
        var oldValue = FactValue();
        var newValue = FactValue();

        var facts = new Grid { ColumnSpacing = 16, RowSpacing = 4 };
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddFactRow(facts, 0, "Kept as they are", keptValue);
        AddFactRow(facts, 1, "New tasks start", startValue);
        AddFactRow(facts, 2, "Old finish date", oldValue);
        AddFactRow(facts, 3, "New finish date", newValue);

        var consequence = new TextBlock
        {
            Text = "Ticked tasks, your score history and your notes are not changed. The \"late from plan\" " +
                   "count starts again from 0. This can't be undone.",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var error = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        void Refresh(PlanReplacement.Situation situation)
        {
            current = situation;
            var preview = PlanReplacement.BuildPreview(situation, replacement);
            headline.Text = PlanReplacement.Headline(preview.Removed, preview.Added);
            AutomationProperties.SetName(headline, headline.Text);
            keptValue.Text = PlanReplacement.KeptText(preview.DoneCount);
            startValue.Text = $"day {preview.StartDay}, {preview.StartDate.ToDisplayDateNumeric()}";
            oldValue.Text = preview.OldFinish.ToDisplayDateNumeric();
            newValue.Text = $"{preview.NewFinish.ToDisplayDateNumeric()} " +
                            $"({PlanReplacement.DescribeFinishChange(preview.OldFinish, preview.NewFinish)})";
        }
        Refresh(current);

        var panel = new StackPanel { Spacing = 10, MinWidth = DialogContentWidth };
        panel.Children.Add(headline);
        panel.Children.Add(facts);
        panel.Children.Add(consequence);
        panel.Children.Add(error);

        var dialog = DialogControls.Build(xamlRoot, "Replace remaining tasks?", panel,
            primaryButtonText: "Replace tasks", secondaryButtonText: "Back", closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Close);
        dialog.Resources["ContentDialogMaxWidth"] = DialogWidth;

        var applied = false;
        var cleanupFailed = false;

        dialog.PrimaryButtonClick += (_, args) =>
        {
            // Cancelled until the write has finished, so a failure keeps the dialog open.
            args.Cancel = true;
            PlanReplacement.ApplyResult result;
            try
            {
                using var db = new Database();
                result = PlanReplacement.Apply(planId, replacement, current.StartDay, db);
            }
            catch (Exception ex)
            {
                Log.Error("ReplaceRemainingDialog.Apply", ex);
                ShowError(error, ApplyFailedMessage);
                return;
            }

            switch (result.Outcome)
            {
                case PlanReplacement.ApplyOutcome.Applied:
                    applied = true;
                    cleanupFailed = result.CleanupFailed;
                    args.Cancel = false;
                    break;
                case PlanReplacement.ApplyOutcome.StartChanged:
                    Refresh(result.Situation);
                    ShowError(error, StartChangedMessage);
                    break;
                case PlanReplacement.ApplyOutcome.NothingToReplace:
                    ShowError(error, "Nothing to replace: every task is already done.");
                    break;
            }
        };
        var dialogResult = await DialogGate.ShowAsync(dialog);
        if (applied && dialogResult == ContentDialogResult.Primary)
        {
            if (cleanupFailed) await ShowCleanupFailedAsync(xamlRoot);
            return Step2Outcome.Applied;
        }
        return dialogResult == ContentDialogResult.Secondary ? Step2Outcome.Back : Step2Outcome.Cancel;
    }

    private static Task ShowCleanupFailedAsync(XamlRoot xamlRoot)
    {
        var warn = DialogControls.Build(xamlRoot, "Plan updated, clean-up incomplete",
            "The plan was replaced, but old reschedule data for the removed tasks could not be cleared. " +
            "Check the log. Some new tasks may appear on the wrong day until it is.",
            closeButtonText: "OK");
        return DialogGate.ShowAsync(warn);
    }

    /// <summary>The step 1 summary line (Design D2 item 1).</summary>
    private static string SummaryText(PlanReplacement.Situation situation)
    {
        var name = situation.Plan.Name;
        if (situation.Done.Count == 0)
            return $"{name}: no tasks are done yet, so the whole plan will be replaced.";
        var kept = situation.Done.Count == 1
            ? "1 task done stays exactly as they are."
            : $"{situation.Done.Count} tasks done stay exactly as they are.";
        var replaced = situation.RemovedCount == 1
            ? "The other 1 task will be replaced."
            : $"The other {situation.RemovedCount} tasks will be replaced.";
        return $"{name}: {kept} {replaced}";
    }

    private static TextBlock FactValue() => new()
    {
        FontSize = 13,
        Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
        TextWrapping = TextWrapping.Wrap,
    };

    private static void AddFactRow(Grid grid, int row, string label, TextBlock value)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);
        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(value);
    }

    private static void ShowError(TextBlock error, string message)
    {
        error.Text = message;
        error.Visibility = Visibility.Visible;
    }
}

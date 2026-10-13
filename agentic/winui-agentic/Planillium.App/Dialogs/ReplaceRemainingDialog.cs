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

    /// <summary>Step 1's values. ChangeText and WordsText are per-use only: kept in memory for Back,
    /// never written anywhere (spec replace-remaining-v2, AC 10).</summary>
    private sealed record Step1Result(string ReplyText, PlanReplacement.Replacement Replacement,
        string ChangeText, string WordsText);

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
        var changeText = "";
        var wordsText = "";
        while (true)
        {
            using (var db = new Database())
                situation = PlanReplacement.Assess(planId, db);

            var step1 = await ShowStep1Async(xamlRoot, situation, replyText, changeText, wordsText);
            if (step1 is null) return false;
            replyText = step1.ReplyText;
            changeText = step1.ChangeText;
            wordsText = step1.WordsText;

            var step2 = await ShowStep2Async(xamlRoot, planId, step1.Replacement, step1.WordsText);
            if (step2 == Step2Outcome.Back) continue;
            return step2 == Step2Outcome.Applied;
        }
    }

    /// <summary>Step 1: prompt, copy, paste. Returns null when cancelled. An invalid paste keeps
    /// the dialog open with the reason shown (args.Cancel) and the typed text untouched.</summary>
    private static async Task<Step1Result?> ShowStep1Async(XamlRoot xamlRoot,
        PlanReplacement.Situation situation, string replyText, string changeText, string wordsText)
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
            Text = "1. Say what should change.",
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };

        var change = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 72,
            Header = "What should change?",
            PlaceholderText = "For example: remove everything about Reddit. Leave empty to let Claude improve the remaining work.",
            Text = changeText,
        };
        AutomationProperties.SetName(change, "What should change?");

        var prompt = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 110,
            Header = "Prompt for Claude",
            Text = BuildPrompt(situation, change.Text),
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
            // With a key saved, the same step also offers Ask Claude (ai-layer spec, D2.3).
            Text = AiKey.Has()
                ? "2. Ask Claude here, or copy this prompt into claude.ai."
                : "2. Copy this prompt into claude.ai.",
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };

        var caption3 = new TextBlock
        {
            Text = "3. Paste Claude's reply below.",
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
        // The problem under the reply box. It is about the reply text it was raised for: editing the
        // box to anything else clears it, but the fill of an AI reply must not (defect D1).
        var replyError = new ReplyErrorState();
        void ShowReplyError(string message)
        {
            replyError.Show(reply.Text, message);
            ShowError(error, message);
        }
        reply.TextChanged += (_, _) =>
        {
            replyError.OnReplyChanged(reply.Text);
            if (replyError.Message is null) error.Visibility = Visibility.Collapsed;
        };

        var doneTitles = situation.Done.Select(d => d.Title).ToList();
        PlanReplacement.Replacement? parsed = null;
        var advancedByAi = false;
        ContentDialog? open = null;

        // Ask Claude (ai-layer spec). On a reply, the same ParseReplacement path as Preview runs:
        // a valid reply closes this step and goes on to the preview; an invalid one stays here.
        var ai = new AiAskControls(reply, "Replace remaining tasks: the plan's name, the titles and details of its " +
                                          "tasks, and what you typed in \"What should change?\".",
            () => BuildPrompt(situation, change.Text),
            locked => change.IsReadOnly = locked,
            text =>
            {
                var problem = PlanReplacement.ParseReplacement(text, doneTitles, out var replacement);
                if (problem is not null || replacement is null)
                {
                    ShowReplyError(problem ?? "Couldn't read the reply. Paste it again.");
                    return "Claude replied, but the reply isn't usable yet — see the message below. You can edit it, or clear the box and ask again.";
                }
                parsed = replacement;
                advancedByAi = true;
                open?.Hide();
                return null;
            });

        // The prompt is rebuilt on every edit of the change field, so Copy always copies the current
        // text. A "Copied ✓" left over from the previous text would mislead, so it resets to "Copy".
        change.TextChanged += (_, _) =>
        {
            prompt.Text = BuildPrompt(situation, change.Text);
            if (Equals(copyBtn.Content, "Copied ✓")) copyBtn.Content = "Copy";
            ai.ClearReason();
        };

        // Optional. Editing it does not clear the reply error: that message is about the reply.
        var words = new TextBox
        {
            Header = "Words that must be gone",
            PlaceholderText = "Reddit, LinkedIn",
            Text = wordsText,
        };
        AutomationProperties.SetName(words, "Words that must be gone");

        var wordsHelper = new TextBlock
        {
            Text = "Optional. Separate with commas. If any of them is still in Claude's new tasks, the preview warns you.",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = DialogContentWidth };
        panel.Children.Add(summary);
        panel.Children.Add(caption1);
        panel.Children.Add(change);
        panel.Children.Add(caption2);
        panel.Children.Add(prompt);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        actions.Children.Add(copyBtn);
        actions.Children.Add(ai.Ask);
        panel.Children.Add(actions);
        panel.Children.Add(ai.Progress);
        panel.Children.Add(ai.Notice);
        panel.Children.Add(ai.Reason);
        panel.Children.Add(caption3);
        panel.Children.Add(reply);
        panel.Children.Add(words);
        panel.Children.Add(wordsHelper);
        panel.Children.Add(error);

        var dialog = DialogControls.Build(xamlRoot, "Replace remaining tasks",
            new ScrollViewer { Content = panel, MaxHeight = 560 },
            primaryButtonText: "Preview", closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Primary);
        dialog.Resources["ContentDialogMaxWidth"] = DialogWidth;
        open = dialog;
        ai.Attach(dialog);
        // Focus on open: the change field, so the user types first.
        dialog.Loaded += (_, _) => change.Focus(FocusState.Programmatic);

        dialog.PrimaryButtonClick += (_, args) =>
        {
            var problem = PlanReplacement.ParseReplacement(reply.Text, doneTitles, out var replacement);
            if (problem is not null || replacement is null)
            {
                ShowReplyError(problem ?? "Couldn't read the reply. Paste it again.");
                args.Cancel = true;
                return;
            }
            parsed = replacement;
        };

        var result = await DialogGate.ShowAsync(dialog);
        if (parsed is null) return null;
        if (result != ContentDialogResult.Primary && !advancedByAi) return null;
        return new Step1Result(reply.Text, parsed, change.Text, words.Text);
    }

    private static string BuildPrompt(PlanReplacement.Situation situation, string changeText) =>
        PlanTemplates.ReplaceRemainder(situation.Plan.Name, situation.Done, situation.Remaining,
            situation.StartDay, changeText);

    /// <summary>Step 2: preview and confirm. The default button is Cancel, so Enter cannot
    /// confirm a destructive change. Figures are recomputed on open and again at confirm.</summary>
    private static async Task<Step2Outcome> ShowStep2Async(XamlRoot xamlRoot, string planId,
        PlanReplacement.Replacement replacement, string wordsText)
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

        // The words check is advisory (spec replace-remaining-v2): it never changes the buttons or
        // blocks Replace tasks. No usable words means no block at all (State A).
        var words = PlanReplacement.ParseWords(wordsText);
        var mentions = PlanReplacement.FindMentions(replacement, words);

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
        var mentionsBlock = BuildMentionsBlock(words, mentions);
        if (mentionsBlock is not null) panel.Children.Add(mentionsBlock);
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

    /// <summary>The words-check block (spec replace-remaining-v2, Design D3). Null when no usable
    /// words were entered. State B is one neutral line; State C is a bordered warning with a
    /// scrollable list of every hit.</summary>
    private static FrameworkElement? BuildMentionsBlock(IReadOnlyList<string> words,
        IReadOnlyList<PlanReplacement.Mention> mentions)
    {
        var summary = PlanReplacement.MentionsSummary(words, mentions);
        if (summary is null) return null;

        if (mentions.Count == 0)
        {
            var found = new TextBlock
            {
                Text = summary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            };
            AutomationProperties.SetName(found, summary);
            return found;
        }

        var heading = new TextBlock
        {
            Text = summary,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetName(heading, summary);

        var lines = new StackPanel { Spacing = 4 };
        foreach (var mention in mentions)
        {
            var line = PlanReplacement.MentionLine(mention);
            var lineBlock = new TextBlock
            {
                Text = line,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            };
            AutomationProperties.SetName(lineBlock, line);
            lines.Children.Add(lineBlock);
        }
        var list = new ScrollViewer
        {
            Content = lines,
            MaxHeight = 150,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = true,
        };

        var footer = new TextBlock
        {
            Text = "You can still replace the tasks, or press Back and ask Claude to remove these.",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(heading);
        content.Children.Add(list);
        content.Children.Add(footer);

        var border = new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = content,
        };
        AutomationProperties.SetName(border, "Words that must be gone check");
        return border;
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

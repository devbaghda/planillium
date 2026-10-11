using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Models;
using Planillium.App.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Planillium.App.Dialogs;

/// <summary>
/// Replace the not-yet-done part of an active plan (2026-10-09 request — e.g. dropping Reddit
/// tasks from a plan that no longer needs them). Done tasks stay exactly where they are;
/// every other task is removed and the pasted Claude reply takes its place. Two steps so
/// nothing changes by accident: paste → preview ("N removed, M added") → confirm.
/// </summary>
public static class ReplaceRemainingDialog
{
    /// <returns>null if cancelled, true once replaced, false if the save failed.</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot, Plan plan)
    {
        List<AssignedTask> tasks;
        try
        {
            using var db = new Database();
            tasks = PlanStore.TasksFor(plan, db, db.LoadCompletions());
        }
        catch (Exception ex)
        {
            Log.Error("ReplaceRemainingDialog (load)", ex);
            return false;
        }

        if (!PlanRemainder.HasUnfinished(tasks))
        {
            await DialogGate.ShowAsync(DialogControls.Build(xamlRoot, "Nothing to replace",
                new TextBlock { Text = "Every task in this plan is already done.", TextWrapping = TextWrapping.Wrap },
                closeButtonText: "OK"));
            return null;
        }

        var intro = new TextBlock
        {
            Text = "Your done tasks stay exactly as they are. Everything else in this plan is replaced by what " +
                   "you paste below. 1) Say what should change, 2) copy the prompt into claude.ai, " +
                   "3) paste Claude's reply here.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.8,
        };
        var wish = new TextBox
        {
            Header = "What should change?",
            PlaceholderText = "e.g. Remove everything about Reddit; spend that time on LinkedIn instead",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 70,
        };
        var prompt = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            Height = 110,
            TextWrapping = TextWrapping.Wrap,
            Header = "Prompt for Claude",
        };
        void RefreshPrompt() => prompt.Text = PlanRemainder.BuildPrompt(plan, tasks, wish.Text);
        RefreshPrompt();
        wish.TextChanged += (_, _) => RefreshPrompt();

        var copy = new Button { Content = "Copy prompt" };
        copy.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(prompt.Text);
            Clipboard.SetContent(dp);
            copy.Content = "Copied ✓";
        };
        wish.TextChanged += (_, _) => copy.Content = "Copy prompt";

        var reply = new TextBox
        {
            Header = "Claude's reply",
            PlaceholderText = "Paste Claude's whole reply (with the ```json block) here",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 110,
        };
        var error = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 460 };
        panel.Children.Add(intro);
        panel.Children.Add(wish);
        panel.Children.Add(copy);
        panel.Children.Add(prompt);
        panel.Children.Add(reply);
        panel.Children.Add(error);

        PlanRemainder.Change? change = null;
        var dialog = DialogControls.Build(xamlRoot, $"Replace remaining tasks — {plan.Name}",
            new ScrollViewer { Content = panel, MaxHeight = 560 },
            primaryButtonText: "Preview", closeButtonText: "Cancel", defaultButton: ContentDialogButton.Primary);
        // Keep the dialog open on a bad reply, show the problem inline (same pattern as AddPlanDialog).
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var problem = PlanRemainder.Prepare(plan, tasks, reply.Text, out change);
            if (problem != null)
            {
                error.Text = problem;
                error.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };
        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary || change is null) return null;

        var oldEnd = plan.DateForPlanDay(plan.TotalDaysComputed);
        var newEnd = plan.DateForPlanDay(change.LastNewDay);
        var confirm = DialogControls.Build(xamlRoot, "Replace remaining tasks?",
            new TextBlock
            {
                Text = $"{change.DoneCount} done task(s) stay untouched.\n" +
                       $"{change.RemovedTexts.Count} unfinished task(s) will be removed.\n" +
                       $"{change.AddedCount} new task(s) will be added, days {change.StartDay}–{change.LastNewDay}.\n" +
                       $"The plan will now finish on {newEnd:dd.MM.yyyy} (was {oldEnd:dd.MM.yyyy}); " +
                       "its \"late from plan\" count restarts from zero.\n\nThis can't be undone.",
                TextWrapping = TextWrapping.Wrap,
            },
            primaryButtonText: "Replace", closeButtonText: "Cancel", defaultButton: ContentDialogButton.Close);
        if (await DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return null;

        try
        {
            using var db = new Database();
            PlanStore.ReplaceRemainingTasks(plan.Id, change, db);
        }
        catch (Exception ex)
        {
            Log.Error("ReplaceRemainingDialog (apply)", ex);
            return false;
        }
        return true;
    }
}

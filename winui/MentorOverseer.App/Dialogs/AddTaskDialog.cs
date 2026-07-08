using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Manually add a new step to an existing plan — for tasks the user thinks
/// of themselves, alongside whatever Claude/import originally generated.
/// Appends straight to the plan JSON (PlanStore.AddTask). No mentor_note
/// field here — that's specifically Claude-authored commentary, not
/// something to fake for a manually-typed task.
/// </summary>
public static class AddTaskDialog
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, Plan plan)
    {
        var picker = new CalendarDatePicker
        {
            Header = "Day",
            MinDate = new DateTimeOffset(plan.StartDateParsed.ToDateTime(TimeOnly.MinValue)),
            Date = new DateTimeOffset(DateTime.Today),
            PlaceholderText = "Pick a date",
            FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday,
        };
        var title = new TextBox { Header = "Task", PlaceholderText = "What needs doing" };
        var detail = new TextBox
        {
            Header = "Detail (optional)",
            PlaceholderText = "The concrete how-to",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
        };
        var duration = new NumberBox
        {
            Header = "Duration, minutes (optional)",
            Minimum = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        var category = new TextBox { Header = "Category (optional)" };
        var error = new TextBlock
        {
            Text = "Pick a day and enter a task title.",
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Visibility = Visibility.Collapsed,
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 320 };
        panel.Children.Add(picker);
        panel.Children.Add(title);
        panel.Children.Add(detail);
        panel.Children.Add(duration);
        panel.Children.Add(category);
        panel.Children.Add(error);

        var dialog = new ContentDialog
        {
            Title = $"Add a step to {plan.Name}",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        // Keep the dialog open on a missing title/date instead of closing
        // and silently doing nothing (same pattern as AddPlanDialog's
        // import-failure handling).
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (title.Text.Trim().Length == 0 || picker.Date is null)
            {
                error.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return false;

        var day = plan.PlanDayForDate(DateOnly.FromDateTime(picker.Date!.Value.DateTime));
        try
        {
            PlanStore.AddTask(plan.Id, day, title.Text.Trim(),
                detail.Text.Trim() is { Length: > 0 } d ? d : null,
                category.Text.Trim() is { Length: > 0 } c ? c : null,
                double.IsNaN(duration.Value) ? null : (int)duration.Value);
        }
        catch (Exception ex)
        {
            Log.Error("AddTaskDialog", ex);
            return false;
        }
        return true;
    }
}

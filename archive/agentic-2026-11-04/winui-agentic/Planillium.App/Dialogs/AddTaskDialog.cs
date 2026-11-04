using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// Manually add a new step to an existing plan — for tasks the user thinks
/// of themselves, alongside whatever Claude/import originally generated.
/// Appends straight to the plan JSON (PlanStore.AddTask). No mentor_note
/// field here — that's specifically Claude-authored commentary, not
/// something to fake for a manually-typed task.
/// </summary>
public static class AddTaskDialog
{
    /// <returns>null if the user cancelled, true once the task is added,
    /// false if the save itself failed (2026-07-14 round-6 audit finding #6:
    /// callers used to treat false the same as a cancel).</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot, Plan plan)
    {
        var picker = new CalendarDatePicker
        {
            Header = "Day",
            MinDate = new DateTimeOffset(plan.StartDateParsed.ToDateTime(TimeOnly.MinValue)),
            Date = new DateTimeOffset(DateTime.Today),
            PlaceholderText = "Pick a date",
            FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday,
            // Numeric-only, zero-padded dd.MM.yyyy — avoids Cyrillic month
            // names on a non-English OS locale (app language is English;
            // same fix as ReportsPage's diary date picker).
            DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}",
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

        var dialog = DialogControls.Build(xamlRoot, $"Add a task to {plan.Name}", panel,
            primaryButtonText: "Add", closeButtonText: "Cancel", defaultButton: ContentDialogButton.Primary);

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

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return null;

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

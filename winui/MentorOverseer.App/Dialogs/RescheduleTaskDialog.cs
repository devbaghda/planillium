using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Move one overdue task to a specific future date without touching any
/// other task's slot — the WinUI port of main.py's
/// _reschedule_task_dialog / _reassign_task_day.
/// </summary>
public static class RescheduleTaskDialog
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, Plan plan, AssignedTask task)
    {
        var minDate = plan.DateForPlanDay(plan.PlanDay + 1);  // tomorrow, exclusion-aware
        var minOffset = new DateTimeOffset(minDate.ToDateTime(TimeOnly.MinValue));

        var picker = new CalendarDatePicker
        {
            Header = $"Reschedule '{task.Task.Text}' to:",
            MinDate = minOffset,
            Date = minOffset,
            PlaceholderText = "Pick a date",
        };

        var dialog = new ContentDialog
        {
            Title = "Reschedule task",
            Content = picker,
            PrimaryButtonText = "Move",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return false;
        if (picker.Date is not { } picked) return false;

        var newDay = plan.PlanDayForDate(DateOnly.FromDateTime(picked.DateTime));

        try
        {
            var plans = PlanStore.LoadActivePlans();
            var p = plans.Find(x => x.Id == plan.Id) ?? plan;
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            score.RescheduleTask(p, task.Task.Text, task.OriginalDay, newDay);
        }
        catch (Exception ex)
        {
            Log.Error("RescheduleTaskDialog", ex);
            return false;
        }
        return true;
    }
}

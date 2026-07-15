using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Move one task — overdue, today's, or any future one — to a specific
/// future date, pushing whatever's already on that day (and everything
/// after it) forward by one instead of doubling up. Originally the WinUI
/// port of main.py's _reschedule_task_dialog / _reassign_task_day
/// (overdue-only); opened up to every open task per the user's request
/// 2026-07-14, since "Move to today" only ever targets today and a future
/// task's own day can turn out to be inconvenient too.
/// </summary>
public static class RescheduleTaskDialog
{
    /// <returns>null if the user cancelled, true on a successful move, false
    /// if the save itself failed — callers used to treat false identically
    /// to a cancel, so a failed reschedule showed no error at all (2026-07-14
    /// round-6 audit finding #6).</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot, Plan plan, AssignedTask task)
    {
        var minDate = plan.DateForPlanDay(plan.PlanDay + 1);  // tomorrow, exclusion-aware
        var minOffset = new DateTimeOffset(minDate.ToDateTime(TimeOnly.MinValue));

        var picker = new CalendarDatePicker
        {
            Header = $"Reschedule '{task.Task.Text}' to:",
            MinDate = minOffset,
            Date = minOffset,
            PlaceholderText = "Pick a date",
            FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday,
            // Numeric-only, zero-padded dd.MM.yyyy — avoids Cyrillic month
            // names on a non-English OS locale (app language is English;
            // same fix as ReportsPage's diary date picker).
            DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}",
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

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return null;
        if (picker.Date is not { } picked) return null;

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

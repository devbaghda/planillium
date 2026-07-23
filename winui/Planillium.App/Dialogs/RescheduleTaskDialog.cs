using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

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
        // Today, not tomorrow — this dialog opens for overdue/due tasks too (any
        // open task, per the doc comment above), and those should be movable to
        // today. "Move to today" already covers future tasks; a due/overdue task
        // has no other path to "today" than this dialog (2026-07-16 bug report).
        var minDate = plan.DateForPlanDay(plan.PlanDay);
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

        var panel = new StackPanel { Spacing = 10 };
        // Same disclosure ReplanOverdueDialog already gives for the identical shift
        // mechanic — this dialog was the one sibling that never got it, so moving a task
        // onto an occupied day was a surprise you only discovered after the fact
        // (2026-07-18 audit finding R8-14).
        panel.Children.Add(new TextBlock
        {
            Text = "Whatever's already on the day you pick — and everything after it — " +
                   "shifts forward by one, so this task gets its own day instead of doubling up.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        panel.Children.Add(picker);

        var dialog = new ContentDialog
        {
            Title = "Reschedule task",
            Content = panel,
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

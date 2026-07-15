using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// "Replan all overdue" — one dialog listing every overdue task with its
/// own date-picker, all picked before anything is saved. Replaced
/// ScoreService's old automatic time-budget spread per the user's request
/// 2026-07-14: he wants to choose exactly which day each task lands on,
/// not have it assigned for him. Whatever's already on the day picked for
/// a task — and everything after it — shifts forward by one, the same
/// "insert, don't overlap" rule RescheduleTaskDialog already uses for a
/// single task; this is just that rule applied once per row, in order,
/// before one flat fee covers the whole batch.
/// </summary>
public static class ReplanOverdueDialog
{
    /// <returns>null if the user cancelled, true once replanned, false if
    /// the save itself failed (2026-07-14 round-6 audit finding #6: callers
    /// used to treat false the same as a cancel).</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot,
        List<Plan> plans, List<(Plan Plan, AssignedTask Task, int DaysOverdue)> overdue)
    {
        var multiPlan = overdue.Select(o => o.Plan.Id).Distinct().Count() > 1;

        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Pick a day for each of the {overdue.Count} overdue task(s). Whatever's " +
                   "already on the day you pick — and everything after it — shifts forward by " +
                   $"one. Penalties already taken for the late days stand; one flat " +
                   $"{ScoreService.ReplanFlatFee} pts covers replanning all of them.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var rows = new List<(Plan Plan, AssignedTask Task, CalendarDatePicker Picker)>();
        var listPanel = new StackPanel { Spacing = 10 };
        foreach (var (plan, task, daysOverdue) in overdue)
        {
            var minDate = plan.DateForPlanDay(plan.PlanDay + 1);
            var minOffset = new DateTimeOffset(minDate.ToDateTime(TimeOnly.MinValue));
            var picker = new CalendarDatePicker
            {
                Header = multiPlan
                    ? $"{task.Task.Text} ({plan.Name}) — {daysOverdue}d overdue"
                    : $"{task.Task.Text} — {daysOverdue}d overdue",
                MinDate = minOffset,
                Date = minOffset,
                PlaceholderText = "Pick a date",
                FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday,
                // Numeric-only, zero-padded dd.MM.yyyy — avoids Cyrillic month
                // names on a non-English OS locale, same fix as elsewhere.
                DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}",
            };
            rows.Add((plan, task, picker));
            listPanel.Children.Add(picker);
        }
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 420,
            Content = listPanel,
        });

        var dialog = new ContentDialog
        {
            Title = "Replan overdue tasks",
            Content = panel,
            PrimaryButtonText = $"Replan · {ScoreService.ReplanFlatFee} pts",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return null;

        try
        {
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            var assignments = rows
                .Where(r => r.Picker.Date is not null)
                .Select(r => (r.Plan, r.Task.Task.Text, r.Task.OriginalDay,
                    r.Plan.PlanDayForDate(DateOnly.FromDateTime(r.Picker.Date!.Value.DateTime))))
                .ToList();
            score.ReplanOverdueTo(assignments);
        }
        catch (Exception ex)
        {
            Log.Error("ReplanOverdueDialog", ex);
            return false;
        }
        return true;
    }
}

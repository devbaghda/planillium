using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Per-plan recurring day-of-week exclusion (e.g. weekends). When day N's
/// tasks would land on an excluded weekday, they — and everything after —
/// shift to the next non-excluded date, cascading if that's excluded too
/// (Plan.DateForPlanDay / PlanDayForDate do the actual mapping). A day off
/// this way costs nothing: ScoreService exempts it the same as a manually
/// marked day off.
/// </summary>
public static class ExcludedWeekdaysDialog
{
    private static readonly (string Label, DayOfWeek Day)[] Days =
    {
        ("Monday", DayOfWeek.Monday), ("Tuesday", DayOfWeek.Tuesday),
        ("Wednesday", DayOfWeek.Wednesday), ("Thursday", DayOfWeek.Thursday),
        ("Friday", DayOfWeek.Friday), ("Saturday", DayOfWeek.Saturday),
        ("Sunday", DayOfWeek.Sunday),
    };

    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, Plan plan)
    {
        var panel = new StackPanel { Spacing = 8, MinWidth = 280 };
        panel.Children.Add(new TextBlock
        {
            Text = "Days this plan never schedules anything on. Existing tasks " +
                   "on those days shift to the next available day.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var boxes = new List<(CheckBox Box, int Value)>();
        foreach (var (label, day) in Days)
        {
            var box = new CheckBox
            {
                Content = label,
                IsChecked = plan.ExcludedWeekdays.Contains((int)day),
            };
            boxes.Add((box, (int)day));
            panel.Children.Add(box);
        }

        var dialog = new ContentDialog
        {
            Title = $"{plan.Name} — excluded days",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return false;

        var selected = boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Value).ToList();
        try
        {
            PlanStore.SetExcludedWeekdays(plan.Id, selected);
        }
        catch (Exception ex)
        {
            Log.Error("ExcludedWeekdaysDialog", ex);
            // The picker dialog above has already closed by this point (the
            // user clicked Save), so failing silently here would leave no
            // visible sign the choice wasn't actually persisted — a second,
            // small dialog is the only way left to say so.
            await DialogGate.ShowAsync(new ContentDialog
            {
                Title = "Couldn't save",
                Content = "The excluded days didn't save — nothing changed. Try again in a moment.",
                CloseButtonText = "OK",
                XamlRoot = xamlRoot,
            });
            return false;
        }
        return true;
    }
}

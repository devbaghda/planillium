using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

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

        var dialog = DialogControls.Build(xamlRoot, $"{plan.Name} — excluded days", panel,
            primaryButtonText: "Save", closeButtonText: "Cancel", defaultButton: ContentDialogButton.Primary);

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
            // DefaultButton explicitly None here, matching this dialog's original un-set
            // behavior (the app's stock ContentDialog default) rather than the factory's own
            // Close default used everywhere else — this one dialog never had Enter wired to
            // its "OK" button, and this migration isn't the place to silently change that.
            await DialogGate.ShowAsync(DialogControls.Build(xamlRoot, "Couldn't save",
                "The excluded days didn't save — nothing changed. Try again in a moment.",
                closeButtonText: "OK", defaultButton: ContentDialogButton.None));
            return false;
        }
        return true;
    }
}

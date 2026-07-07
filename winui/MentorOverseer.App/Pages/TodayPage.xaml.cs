using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class TodayPage : Page
{
    public TodayPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            Render();
            _ = Views.TickTickSection.LoadAsync(TickTickHost);
            if (KickoffDialog.ShouldShow() && App.MainWindow is MainWindow win)
                await KickoffDialog.ShowAsync(win);
        };
    }

    private async void Review_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow win)
        {
            await ReviewDialog.ShowAsync(win);
            Render();
        }
    }

    private async void Replan_Click(object sender, RoutedEventArgs e)
    {
        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);
        var overdue = score.OverdueAsOf(DateOnly.FromDateTime(DateTime.Today));
        if (overdue.Count == 0) return;

        var confirm = new ContentDialog
        {
            Title = "Replan all overdue?",
            Content = $"{overdue.Count} overdue task(s) will be spread across the coming days " +
                      $"(≤{ScoreService.ReplanDailyBudgetMin / 60}h of planned work per day), " +
                      $"for one flat {ScoreService.ReplanFlatFee} pts. " +
                      "Penalties already taken are not refunded — but the daily bleeding stops.",
            PrimaryButtonText = $"Replan · {ScoreService.ReplanFlatFee} pts",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        score.ReplanAllOverdue();
        (App.MainWindow as MainWindow)?.RefreshScore();
        Render();
    }

    // TickTick section lives in Views/TickTickSection (network I/O out of
    // this page's rendering code).

    private void Render()
    {
        Sections.Children.Clear();
        // App language is English — don't let the OS locale mix in Cyrillic
        // day names (audit finding #16).
        Subtitle.Text = DateTime.Today.ToString("dddd dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture);

        List<Plan> plans;
        Dictionary<(string, int, string), bool> completions;
        try
        {
            plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            completions = db.LoadCompletions();

            var totalOverdue = 0;

            if (plans.Count == 0)
            {
                Sections.Children.Add(EmptyState());
                return;
            }

            foreach (var plan in plans)
            {
                var tasks = PlanStore.TasksFor(plan, db, completions);
                var planDay = plan.PlanDay;

                Sections.Children.Add(SectionHeader(
                    $"{plan.Name}  ·  Day {planDay} of {plan.TotalDaysComputed}"));

                if (planDay <= 0)
                {
                    Sections.Children.Add(Muted(
                        $"Starts {plan.StartDateParsed:dd.MM.yyyy} — {1 - planDay} day(s) to go."));
                    continue;
                }

                var overdue = tasks.Where(t => t.Overdue)
                                   .OrderBy(t => t.AssignedDay).ToList();
                var today = tasks.Where(t => t.AssignedDay == planDay).ToList();
                totalOverdue += overdue.Count;

                if (overdue.Count > 0)
                {
                    Sections.Children.Add(GroupLabel($"OVERDUE · {overdue.Count}", danger: true));
                    Sections.Children.Add(TaskCard(overdue, plan));
                }

                if (today.Count > 0)
                {
                    var done = today.Count(t => t.Completed);
                    Sections.Children.Add(GroupLabel($"TODAY · {done} OF {today.Count} DONE"));
                    Sections.Children.Add(TaskCard(today, plan));
                }
                else if (overdue.Count == 0)
                {
                    Sections.Children.Add(Muted("All done for today — great work!"));
                }
            }

            if (totalOverdue > 0)
            {
                OverdueBar.Message =
                    $"{totalOverdue} overdue task{(totalOverdue == 1 ? " is" : "s are")} costing you points daily.";
                OverdueBar.IsOpen = true;
            }
            else
            {
                OverdueBar.IsOpen = false;
            }
        }
        catch (Exception ex)
        {
            Log.Error("TodayPage.Render", ex);
            Sections.Children.Add(Muted(
                "Couldn't load plan data.\n" + ex.Message +
                "\nIf the app isn't next to the data folder, set MENTOR_ROOT."));
        }
    }

    // ── section building blocks ──────────────────────────────────────────

    private static TextBlock SectionHeader(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        Margin = new Thickness(0, 18, 0, 2),
    };

    private static TextBlock GroupLabel(string text, bool danger = false) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 60,
        Foreground = (Brush)Application.Current.Resources[
            danger ? "SystemFillColorCriticalBrush" : "TextFillColorTertiaryBrush"],
        Margin = new Thickness(2, 12, 0, 6),
    };

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        Margin = new Thickness(2, 8, 0, 8),
    };

    private StackPanel EmptyState()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 24, 0, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = "No active plans yet.",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        panel.Children.Add(Muted("Generate one with Claude, paste your own, or load a plan file."));
        var add = new Button
        {
            Content = "+ Add Plan",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin = new Thickness(0, 8, 0, 0),
        };
        add.Click += async (_, _) =>
        {
            if (await Dialogs.AddPlanDialog.ShowAsync(this)) Render();
        };
        panel.Children.Add(add);
        return panel;
    }

    private Border TaskCard(List<AssignedTask> tasks, Plan plan)
    {
        var list = new StackPanel();
        for (var i = 0; i < tasks.Count; i++)
        {
            if (i > 0)
                list.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                });
            list.Children.Add(TaskRow(tasks[i], plan));
        }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = list,
        };
    }

    private Grid TaskRow(AssignedTask item, Plan plan)
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 10, 16, 10),
            ColumnSpacing = 12,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox
        {
            IsChecked = item.Completed,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Without a name a screen reader announces 75 bare "checkbox"es.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(check, item.Task.Text);
        check.Checked += (_, _) => Toggle(item, plan, true);
        check.Unchecked += (_, _) => Toggle(item, plan, false);
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock
        {
            Text = item.Task.Text,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = item.Completed ? FontWeights.Normal : FontWeights.SemiBold,
        };
        if (item.Completed)
            name.Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        else if (item.Overdue)
            name.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        textCol.Children.Add(name);

        var metaText = item.Overdue
            ? $"{plan.PlanDay - item.AssignedDay} day(s) late — from day {item.AssignedDay}"
            : item.Task.MentorNote is { Length: > 0 } note ? "💡 " + note : null;
        if (metaText != null)
            textCol.Children.Add(new TextBlock
            {
                Text = metaText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
        Grid.SetColumn(textCol, 1);
        grid.Children.Add(textCol);

        if (item.Task.Category is { Length: > 0 } cat)
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 3, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                Child = new TextBlock
                {
                    Text = cat,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                },
            };
            Grid.SetColumn(chip, 2);
            grid.Children.Add(chip);
        }

        if (item.Task.DurationMin is int mins && !item.Completed)
        {
            var dur = new TextBlock
            {
                Text = $"{mins}m",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            };
            Grid.SetColumn(dur, 3);
            grid.Children.Add(dur);
        }

        return grid;
    }

    private void Toggle(AssignedTask item, Plan plan, bool done)
    {
        try
        {
            using var db = new Database();
            db.SaveCompletion(plan.Id, item.AssignedDay, item.Task.Text, done);
            item.Completed = done;
            Render();
            (App.MainWindow as MainWindow)?.RefreshScore();
        }
        catch (Exception ex)
        {
            // Likely: DB briefly locked by the Python app. The next render
            // self-heals visually, but a failed completion write must not
            // vanish without a trace.
            Log.Error($"Toggle completion '{item.Task.Text}'", ex);
        }
    }
}

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class TodayPage : Page
{
    public TodayPage()
    {
        InitializeComponent();
    }

    // NavigationCacheMode="Enabled" (see XAML) keeps this instance alive
    // across menu switches instead of reconstructing the whole page — and
    // its C#-built UI tree plus a DB open — every single time. OnNavigatedTo
    // (not Loaded) fires on every navigation TO this page, cached or not,
    // so the content still refreshes each visit.
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render();
        _ = Views.TickTickSection.LoadAsync(TickTickHost);
        if (App.MainWindow is MainWindow win)
        {
            // Render() already succeeded (or was caught) above — a failure
            // in either of these await calls must not look identical to
            // "the prompt just isn't due today," so it gets its own log
            // context instead of only reaching the app-wide handler.
            try
            {
                await NameSetupDialog.EnsureShownAsync(win);
                await KickoffDialog.Trigger(win);
            }
            catch (Exception ex)
            {
                Log.Error("TodayPage.OnNavigatedTo (first-run/kickoff prompts)", ex);
            }
        }
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
        List<(Plan Plan, AssignedTask Task, int DaysOverdue)> overdue;
        using (var db = new Database())
        using (var score = new ScoreService(plans, db))
            overdue = score.OverdueAsOf(DateOnly.FromDateTime(DateTime.Today));
        if (overdue.Count == 0) return;

        if (await ReplanOverdueDialog.ShowAsync(XamlRoot, plans, overdue))
        {
            (App.MainWindow as MainWindow)?.RefreshScore();
            Render();
        }
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
                var notes = db.LoadTaskNotes(plan.Id);
                var planDay = plan.PlanDay;

                Sections.Children.Add(SectionHeader(
                    $"{plan.Name}  ·  Day {planDay} of {plan.TotalDaysComputed}"));

                if (planDay <= 0)
                {
                    Sections.Children.Add(Muted(
                        $"Starts {plan.StartDateParsed:dd.MM.yyyy} — {1 - planDay} day(s) to go."));
                    continue;
                }

                // Today itself is an excluded weekday — nothing is due, and
                // the day counter hasn't advanced, so without this check
                // "today's tasks" would just re-show yesterday's (already
                // completed) ones instead of a clean day-off state.
                if (plan.IsExcluded(DateOnly.FromDateTime(DateTime.Today)))
                {
                    Sections.Children.Add(Muted("🌴 Day off — no tasks scheduled today."));
                    var overdueToday = tasks.Where(t => t.Overdue).OrderBy(t => t.AssignedDay).ToList();
                    if (overdueToday.Count > 0)
                    {
                        totalOverdue += overdueToday.Count;
                        Sections.Children.Add(GroupLabel($"OVERDUE · {overdueToday.Count}", danger: true));
                        Sections.Children.Add(TaskCard(overdueToday, plan, notes));
                    }
                    continue;
                }

                var overdue = tasks.Where(t => t.Overdue)
                                   .OrderBy(t => t.AssignedDay).ToList();
                var today = tasks.Where(t => t.AssignedDay == planDay).ToList();
                totalOverdue += overdue.Count;

                if (overdue.Count > 0)
                {
                    Sections.Children.Add(GroupLabel($"OVERDUE · {overdue.Count}", danger: true));
                    Sections.Children.Add(TaskCard(overdue, plan, notes));
                }

                var done = today.Count(t => t.Completed);
                if (today.Count > 0)
                {
                    Sections.Children.Add(GroupLabel($"TODAY · {done} OF {today.Count} DONE"));
                    Sections.Children.Add(TaskCard(today, plan, notes));
                }
                else if (overdue.Count == 0)
                {
                    // today.Count == 0 here means no task was ever scheduled
                    // for this plan day (a gap day, not a completed one) —
                    // "great work!" would be congratulating for nothing.
                    Sections.Children.Add(Muted("No tasks scheduled for today."));
                }

                // No overdue work, and at least one task today is already
                // done (or there was never anything scheduled) — offer to
                // pull tomorrow's tasks forward instead of making the user
                // dig for "Move to today" on the Schedule page. Used to
                // require *every* task today to be done first; now any
                // amount of progress is enough to ask.
                if (overdue.Count == 0 && (today.Count == 0 || done > 0))
                {
                    var tomorrowDay = planDay + 1;
                    if (!plan.IsExcluded(plan.DateForPlanDay(tomorrowDay)))
                    {
                        var tomorrowTasks = tasks
                            .Where(t => t.AssignedDay == tomorrowDay && !t.Completed).ToList();
                        if (tomorrowTasks.Count > 0)
                        {
                            // Today.Count == 0 or done == today.Count means
                            // today is genuinely clear — confident copy.
                            // Otherwise this is firing on partial progress
                            // (as little as one task done), so the header
                            // shouldn't read as if today were finished
                            // (2026-07-09 audit finding #13).
                            var todayClear = today.Count == 0 || done == today.Count;
                            Sections.Children.Add(GroupLabel(todayClear
                                ? "GET A HEAD START ON TOMORROW?"
                                : "ALSO ON DECK FOR TOMORROW"));
                            Sections.Children.Add(GetAheadCard(tomorrowTasks, plan));
                        }
                    }
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

    private Border TaskCard(List<AssignedTask> tasks, Plan plan, Dictionary<string, string> notes)
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
            list.Children.Add(TaskRow(tasks[i], plan, notes.GetValueOrDefault(tasks[i].Task.Text)));
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

    private Border GetAheadCard(List<AssignedTask> tomorrowTasks, Plan plan)
    {
        var list = new StackPanel();
        for (var i = 0; i < tomorrowTasks.Count; i++)
        {
            if (i > 0)
                list.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                });
            list.Children.Add(GetAheadRow(tomorrowTasks[i], plan));
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

    private Grid GetAheadRow(AssignedTask item, Plan plan)
    {
        var grid = new Grid { Padding = new Thickness(16, 10, 16, 10), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textCol.Children.Add(new TextBlock { Text = item.Task.Text, TextWrapping = TextWrapping.Wrap });
        if (item.Task.MentorNote is { Length: > 0 } mentorNote)
            textCol.Children.Add(new TextBlock
            {
                Text = "💡 " + mentorNote,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
        Grid.SetColumn(textCol, 0);
        grid.Children.Add(textCol);

        if (item.Task.DurationMin is int mins)
        {
            var dur = new TextBlock
            {
                Text = $"{mins}m",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            };
            Grid.SetColumn(dur, 1);
            grid.Children.Add(dur);
        }

        var start = new Button { Content = "Start now", VerticalAlignment = VerticalAlignment.Center };
        start.Click += (_, _) => PlanScoreAction.Run(plan,
            (score, p) => score.MoveTaskToToday(p, item.Task.Text), "TodayPage.GetAhead",
            () => { Render(); (App.MainWindow as MainWindow)?.RefreshScore(); });
        Grid.SetColumn(start, 2);
        grid.Children.Add(start);

        if (item.Task.Detail is { Length: > 0 })
        {
            var details = TaskDetailsLink.Build(XamlRoot, item.Task,
                new Thickness(6, 0, 0, 0), VerticalAlignment.Center);
            Grid.SetColumn(details, 3);
            grid.Children.Add(details);
        }

        return grid;
    }

    private Grid TaskRow(AssignedTask item, Plan plan, string? note)
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
            Foreground = Views.TaskRowStyle.TitleForeground(item),
        };
        textCol.Children.Add(name);

        // Overdue status and the mentor note are independent facts — an
        // overdue task still deserves its "why this matters" line, not one
        // or the other.
        if (item.Overdue)
            textCol.Children.Add(new TextBlock
            {
                Text = $"{plan.PlanDay - item.AssignedDay} day(s) late — from day {item.AssignedDay}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
        if (item.Task.MentorNote is { Length: > 0 } mentorNote)
            textCol.Children.Add(new TextBlock
            {
                Text = "💡 " + mentorNote,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
        textCol.Children.Add(Views.TaskNoteView.Build(note, plan.Id, item.Task.Text, "TodayPage.SetTaskNote"));
        Grid.SetColumn(textCol, 1);
        grid.Children.Add(textCol);

        if (item.Task.DurationMin is int mins && !item.Completed)
        {
            var dur = new TextBlock
            {
                Text = $"{mins}m",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            };
            Grid.SetColumn(dur, 2);
            grid.Children.Add(dur);
        }

        // The "explanation of tasks" — detail is the concrete how-to, which
        // (unlike the mentor note) never had anywhere to display at all.
        if (item.Task.Detail is { Length: > 0 })
        {
            var details = TaskDetailsLink.Build(XamlRoot, item.Task,
                new Thickness(6, 0, 0, 0), VerticalAlignment.Center);
            Grid.SetColumn(details, 3);
            grid.Children.Add(details);
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
            // Likely: the poll thread's own connection briefly held the
            // write lock. Logged either way, but a failed completion write
            // must not vanish silently — the checkbox would otherwise just
            // revert on next render with no explanation. Plan ID + day, not
            // the task text — the log file has no retention/redaction of
            // its own, unlike the diary it sits alongside (privacy audit).
            Log.Error($"Toggle completion '{plan.Id}' day {item.AssignedDay}", ex);
            SaveErrorBar.IsOpen = true;
        }
    }
}

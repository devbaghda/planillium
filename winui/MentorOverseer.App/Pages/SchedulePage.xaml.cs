using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;
using MentorOverseer.App.Views;

namespace MentorOverseer.App.Pages;

/// <summary>
/// Day-by-day plan timeline — the WinUI counterpart of the Python app's
/// schedule dialog. Same operations, same DB semantics: "Move to today"
/// (swap + shift the days between), "Day off" (shift everything from that
/// day forward), both via ScoreService so the shared contract stays in one place.
/// </summary>
public sealed partial class SchedulePage : Page
{
    public SchedulePage()
    {
        InitializeComponent();
    }

    // NavigationCacheMode="Enabled" (see XAML) reuses this instance across
    // menu switches instead of reconstructing the page + reopening the DB
    // every time; OnNavigatedTo fires every visit (cached or not), unlike
    // Loaded which would only fire once for a reused instance.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render();
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private void Render()
    {
        Sections.Children.Clear();

        List<Plan> plans;
        try
        {
            plans = PlanStore.LoadActivePlans();
        }
        catch (Exception ex)
        {
            Log.Error("SchedulePage.Render (plans)", ex);
            Sections.Children.Add(new TextBlock { Text = "Couldn't load plans: " + ex.Message });
            return;
        }

        if (plans.Count == 0)
        {
            Subtitle.Text = "No active plans.";
            return;
        }
        Subtitle.Text = "Move tasks, take days off — the plan flexes, the goal doesn't.";

        try
        {
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            var completions = db.LoadCompletions();

            foreach (var plan in plans)
                RenderPlan(plan, db, score, completions);
        }
        catch (Exception ex)
        {
            Log.Error("SchedulePage.Render", ex);
            Sections.Children.Add(new TextBlock { Text = "Couldn't load the schedule: " + ex.Message });
        }
    }

    private void RenderPlan(Plan plan, Database db, ScoreService score,
        Dictionary<(string, int, string), bool> completions)
    {
        var planDay = plan.PlanDay;
        var tasks = PlanStore.TasksFor(plan, db, completions);
        var notes = db.LoadTaskNotes(plan.Id);
        var daysOff = score.DaysOff(plan.Id);
        var byDay = tasks.GroupBy(t => t.AssignedDay)
                         .ToDictionary(g => g.Key, g => g.ToList());
        var lastDay = Math.Max(plan.TotalDaysComputed,
                               tasks.Count > 0 ? tasks.Max(t => t.AssignedDay) : 1);

        Sections.Children.Add(new TextBlock
        {
            Text = $"{plan.Name} — day {planDay} of {lastDay}",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            Margin = new Thickness(0, 8, 0, 4),
        });

        // Each plan gets its own bounded scroll box instead of every plan's
        // day cards pouring into one shared list — with 2 active plans,
        // reaching the second plan used to mean scrolling past the whole
        // first one first.
        var dayList = new StackPanel { Spacing = 12, Padding = new Thickness(0, 0, 8, 0) };
        FrameworkElement? todayCard = null;

        // A long plan (e.g. 160 days) was rendering a full DayCard — Grid,
        // Border, multiple TextBlocks — for every single future day even
        // when empty, 150+ mostly-blank cards on every visit to this page.
        // Cap the empty-future window to 3 weeks out; anything with real
        // content (tasks, or marked off) still always renders regardless
        // of distance, so nothing actually useful is hidden.
        var lookahead = planDay + 21;
        for (var day = 1; day <= lastDay; day++)
        {
            var isToday = day == planDay;
            var isOff = daysOff.Contains(day);
            var dayTasks = byDay.TryGetValue(day, out var list) ? list : new List<AssignedTask>();
            // The past is history: skip empty days that are already gone.
            if (day < planDay && dayTasks.Count == 0 && !isOff) continue;
            if (day > lookahead && dayTasks.Count == 0 && !isOff) continue;

            var card = DayCard(plan, day, isToday, isOff, dayTasks, notes);
            dayList.Children.Add(card);
            if (isToday) todayCard = card;
        }

        var scroller = new ScrollViewer
        {
            Height = 440,
            Content = dayList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Sections.Children.Add(new Border
        {
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Child = scroller,
        });

        // Land this plan's own viewport on its own today, not Day 1.
        todayCard?.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.12 });
    }

    private FrameworkElement DayCard(Plan plan, int day, bool isToday, bool isOff,
        List<AssignedTask> dayTasks, Dictionary<string, string> notes)
    {
        var planDay = plan.PlanDay;
        var date = plan.DateForPlanDay(day);
        var overdueCount = dayTasks.Count(t => t.Overdue);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 6 };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(new TextBlock
        {
            Text = $"Day {day}",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        header.Children.Add(new TextBlock
        {
            // English day names regardless of OS locale — same rule as everywhere.
            Text = date.ToString("ddd dd.MM", CultureInfo.InvariantCulture),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = Res("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (isToday) header.Children.Add(Chip("Today", "AccentFillColorDefaultBrush", onAccent: true));
        if (isOff) header.Children.Add(Chip("Day off", "SubtleFillColorSecondaryBrush"));
        if (overdueCount > 0)
            header.Children.Add(Chip($"{overdueCount} overdue", "SystemFillColorCriticalBackgroundBrush"));
        left.Children.Add(header);

        if (dayTasks.Count == 0 && !isOff)
            left.Children.Add(new TextBlock
            {
                Text = "No tasks",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = Res("TextFillColorTertiaryBrush"),
            });

        foreach (var t in dayTasks.OrderBy(t => t.Completed))
            left.Children.Add(TaskRow(plan, t, planDay, notes.GetValueOrDefault(t.Task.Text)));

        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        // Day-off toggle: only meaningful from today onward.
        if (day >= planDay)
        {
            var toggle = new HyperlinkButton
            {
                Content = isOff ? "Undo day off" : "Day off",
                FontSize = 12,
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Top,
            };
            var d = day;
            toggle.Click += (_, _) =>
            {
                try
                {
                    var plans = PlanStore.LoadActivePlans();
                    var p = plans.FirstOrDefault(x => x.Id == plan.Id) ?? plan;
                    using var db = new Database();
                    using var score = new ScoreService(plans, db);
                    if (isOff) score.UnmarkDayOff(p, d);
                    else score.MarkDayOff(p, d);
                }
                catch (Exception ex) { Log.Error("SchedulePage.DayOff", ex); }
                Render();
            };
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(toggle);
        }

        return new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = isToday ? Res("AccentFillColorDefaultBrush")
                                  : Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid,
        };
    }

    private FrameworkElement TaskRow(Plan plan, AssignedTask t, int planDay, string? note)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // A real checkbox, not a status glyph \u2014 marking done by mistake must
        // be reversible right here, not only on the Today page.
        var check = new CheckBox
        {
            IsChecked = t.Completed,
            MinWidth = 0,
            Margin = new Thickness(0, -4, 2, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(check, t.Task.Text);
        check.Checked += (_, _) => ToggleDone(plan, t, true);
        check.Unchecked += (_, _) => ToggleDone(plan, t, false);
        Grid.SetColumn(check, 0);
        row.Children.Add(check);

        var textPanel = new StackPanel();
        var title = new TextBlock
        {
            Text = t.Task.Text,
            TextWrapping = TextWrapping.Wrap,
            // Overdue reads red here now that the warning glyph gave way to
            // the checkbox.
            Foreground = t.Completed ? Res("TextFillColorTertiaryBrush")
                       : t.Overdue ? Res("SystemFillColorCriticalBrush")
                       : Res("TextFillColorPrimaryBrush"),
        };
        textPanel.Children.Add(title);
        var meta = new List<string>();
        if (t.Task.DurationMin is int dur) meta.Add($"{dur} min");
        if (t.AssignedDay != t.OriginalDay) meta.Add($"moved from day {t.OriginalDay}");
        if (meta.Count > 0)
            textPanel.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", meta),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = Res("TextFillColorTertiaryBrush"),
            });
        textPanel.Children.Add(TaskNoteView.Build(note, text =>
        {
            try
            {
                using var db = new Database();
                db.SetTaskNote(plan.Id, t.Task.Text, text);
            }
            catch (Exception ex) { Log.Error("SchedulePage.SetTaskNote", ex); }
        }));
        Grid.SetColumn(textPanel, 1);
        row.Children.Add(textPanel);

        // Move/Reschedule and Details are independent — a task can be both
        // overdue and have a detail worth reading — so they share one
        // action column instead of competing for it.
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        if (!t.Completed && t.AssignedDay > planDay)
        {
            var move = new HyperlinkButton
            {
                Content = "Move to today",
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            move.Click += (_, _) =>
            {
                try
                {
                    var plans = PlanStore.LoadActivePlans();
                    var p = plans.FirstOrDefault(x => x.Id == plan.Id) ?? plan;
                    using var db = new Database();
                    using var score = new ScoreService(plans, db);
                    score.MoveTaskToToday(p, t.Task.Text);
                }
                catch (Exception ex) { Log.Error("SchedulePage.MoveToToday", ex); }
                Render();
            };
            actions.Children.Add(move);
        }
        else if (!t.Completed && t.Overdue)
        {
            // Overdue tasks skip "Move to today" (that's what Replan-all is
            // for) but still need a way out of the past — pick a new date,
            // the accrued penalty for the late days stands.
            var reschedule = new HyperlinkButton
            {
                Content = "Reschedule…",
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            reschedule.Click += async (_, _) =>
            {
                if (await Dialogs.RescheduleTaskDialog.ShowAsync(XamlRoot, plan, t)) Render();
            };
            actions.Children.Add(reschedule);
        }

        if (t.Task.Detail is { Length: > 0 })
        {
            var details = new HyperlinkButton
            {
                Content = "Details",
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            details.Click += async (_, _) => await Dialogs.TaskDetailDialog.ShowAsync(XamlRoot, t.Task);
            actions.Children.Add(details);
        }

        if (actions.Children.Count > 0)
        {
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);
        }

        return row;
    }

    private void ToggleDone(Plan plan, AssignedTask t, bool done)
    {
        try
        {
            using var db = new Database();
            db.SaveCompletion(plan.Id, t.AssignedDay, t.Task.Text, done);
            t.Completed = done;
            (App.MainWindow as MainWindow)?.RefreshScore();
        }
        catch (Exception ex)
        {
            Log.Error($"SchedulePage.ToggleDone '{t.Task.Text}'", ex);
            SaveErrorBar.IsOpen = true;
        }
        Render();
    }

    private static Border Chip(string text, string brushKey, bool onAccent = false) => new()
    {
        Background = (Brush)Application.Current.Resources[brushKey],
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 1, 8, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources[
                onAccent ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"],
        },
    };
}

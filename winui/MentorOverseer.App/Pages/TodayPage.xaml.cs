using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

using Microsoft.UI.Xaml.Automation;
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

        var ok = await ReplanOverdueDialog.ShowAsync(XamlRoot, plans, overdue);
        if (ok == true)
        {
            (App.MainWindow as MainWindow)?.RefreshScore();
            Render();
        }
        else if (ok == false)
        {
            Render();
            SaveErrorBar.IsOpen = true;
        }
    }

    // TickTick section lives in Views/TickTickSection (network I/O out of
    // this page's rendering code).

    private void Render()
    {
        Sections.Children.Clear();
        // Every render clears any stale error from a previous failed save —
        // otherwise the banner outlives the failure it reported, even once
        // later actions succeed fine (round-5 audit finding #7).
        SaveErrorBar.IsOpen = false;
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
                Sections.Children.Add(Views.EmptyPlansState.Build(this,
                    "Generate one with Claude, paste your own, or load a plan file.", Render));
                return;
            }

            foreach (var plan in plans)
            {
                var tasks = PlanStore.TasksFor(plan, db, completions);
                var notes = db.LoadTaskNotes(plan.Id);
                var planDay = plan.PlanDay;
                // Every branching decision (not started yet? day off? what's overdue/due
                // today? eligible for "get a head start"?) lives in this one pure call —
                // the loop below just plays the result back as UI, so a purely visual
                // change here can no longer accidentally shift any of those decisions
                // (audit finding #5, same reasoning GetAheadEligibility was already
                // extracted for).
                var view = BuildPlanView(plan, tasks, planDay);

                Sections.Children.Add(SectionHeader(view.Header));

                if (view.NotStartedMessage is { } notStarted)
                {
                    Sections.Children.Add(Muted(notStarted));
                    continue;
                }

                // Day off shows its message first, then any overdue section below it —
                // a normal day shows overdue first with no day-off message at all, so
                // this stays two branches (matching the original order exactly) rather
                // than one shared block reordering either case.
                if (view.DayOff)
                {
                    Sections.Children.Add(Muted("🌴 Day off — no tasks scheduled today."));
                    totalOverdue += view.Overdue.Count;
                    if (view.Overdue.Count > 0)
                    {
                        Sections.Children.Add(GroupLabel($"OVERDUE · {view.Overdue.Count}", danger: true));
                        Sections.Children.Add(TaskCard(view.Overdue, plan, notes));
                    }
                    continue;
                }

                totalOverdue += view.Overdue.Count;
                if (view.Overdue.Count > 0)
                {
                    Sections.Children.Add(GroupLabel($"OVERDUE · {view.Overdue.Count}", danger: true));
                    Sections.Children.Add(TaskCard(view.Overdue, plan, notes));
                }

                var done = view.Today.Count(t => t.Completed);
                if (view.Today.Count > 0)
                {
                    Sections.Children.Add(GroupLabel($"TODAY · {done} OF {view.Today.Count} DONE"));
                    Sections.Children.Add(TaskCard(view.Today, plan, notes));
                }
                else if (view.NoTasksMessage is { } noTasks)
                {
                    Sections.Children.Add(Muted(noTasks));
                }

                if (view.Eligible is { } eligible)
                {
                    Sections.Children.Add(GroupLabel(eligible.TodayClear
                        ? "GET A HEAD START ON TOMORROW?"
                        : "ALSO ON DECK FOR TOMORROW"));
                    Sections.Children.Add(GetAheadCard(eligible.TomorrowTasks, plan));
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
            // The MENTOR_ROOT hint used to be stacked onto the on-screen message too,
            // reading as two different pieces of advice glued together (2026-07-18 audit
            // finding R10-10) — it's genuinely useful, just belongs in the log next to the
            // exception it explains, not as a second sentence in the user-facing text.
            Log.Error("TodayPage.Render (if the app isn't next to the data folder, set MENTOR_ROOT)", ex);
            Sections.Children.Add(Muted(Log.Friendly("Couldn't load your plan data", ex)));
        }
    }

    /// <summary>Everything Render()'s per-plan loop needs to know, decided once, up
    /// front, as plain data — see the call site's comment (audit finding #5).</summary>
    private sealed record PlanTodayView(
        string Header, string? NotStartedMessage, bool DayOff,
        List<AssignedTask> Overdue, List<AssignedTask> Today, string? NoTasksMessage,
        (List<AssignedTask> TomorrowTasks, bool TodayClear)? Eligible);

    private static PlanTodayView BuildPlanView(Plan plan, List<AssignedTask> tasks, int planDay)
    {
        var header = $"{plan.Name}  ·  Day {planDay} of {plan.TotalDaysComputed}";
        if (planDay <= 0)
            return new PlanTodayView(header,
                $"Starts {plan.StartDateParsed:dd.MM.yyyy} — {1 - planDay} day(s) to go.",
                false, new List<AssignedTask>(), new List<AssignedTask>(), null, null);

        var overdue = tasks.Where(t => t.Overdue).OrderBy(t => t.AssignedDay).ToList();

        // Today itself is an excluded weekday — nothing is due, and the day counter
        // hasn't advanced, so without this check "today's tasks" would just re-show
        // yesterday's (already completed) ones instead of a clean day-off state.
        if (plan.IsExcluded(DateOnly.FromDateTime(DateTime.Today)))
            return new PlanTodayView(header, null, true, overdue, new List<AssignedTask>(), null, null);

        var today = tasks.Where(t => t.AssignedDay == planDay).ToList();
        var done = today.Count(t => t.Completed);
        // today.Count == 0 here means no task was ever scheduled for this plan day (a
        // gap day, not a completed one) — "great work!" would be congratulating for
        // nothing, so this message only applies when there's also no overdue work.
        var noTasksMessage = today.Count == 0 && overdue.Count == 0
            ? "No tasks scheduled for today." : null;
        var eligible = GetAheadEligibility(plan, tasks, overdue.Count, today.Count, done);

        return new PlanTodayView(header, null, false, overdue, today, noTasksMessage, eligible);
    }

    /// <summary>The "offer tomorrow's tasks early" business rule, pulled out of Render()
    /// so a purely visual change to that method can no longer accidentally shift when
    /// this fires (round-5 audit finding #18) — this rule alone has already been tuned
    /// more than once (2026-07-09 audit finding #13). Returns null when not eligible.</summary>
    private static (List<AssignedTask> TomorrowTasks, bool TodayClear)? GetAheadEligibility(
        Plan plan, List<AssignedTask> tasks, int overdueCount, int todayCount, int done)
    {
        // No overdue work, and at least one task today is already done (or there was
        // never anything scheduled) — offer to pull tomorrow's tasks forward instead of
        // making the user dig for "Move to today" on the Schedule page. Used to require
        // *every* task today to be done first; now any amount of progress is enough to ask.
        if (overdueCount != 0 || (todayCount != 0 && done == 0)) return null;

        var tomorrowDay = plan.PlanDay + 1;
        if (plan.IsExcluded(plan.DateForPlanDay(tomorrowDay))) return null;

        var tomorrowTasks = tasks.Where(t => t.AssignedDay == tomorrowDay && !t.Completed).ToList();
        if (tomorrowTasks.Count == 0) return null;

        // today.Count == 0 or done == today.Count means today is genuinely clear —
        // confident copy. Otherwise this is firing on partial progress (as little as
        // one task done), so the header shouldn't read as if today were finished
        // (2026-07-09 audit finding #13).
        var todayClear = todayCount == 0 || done == todayCount;
        return (tomorrowTasks, todayClear);
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

    private Border TaskCard(List<AssignedTask> tasks, Plan plan, Dictionary<string, string> notes) =>
        Card(tasks, t => TaskRow(t, plan, notes.GetValueOrDefault(t.Task.Text)));

    private Border GetAheadCard(List<AssignedTask> tomorrowTasks, Plan plan) =>
        Card(tomorrowTasks, t => GetAheadRow(t, plan));

    /// <summary>Shared card-building logic TaskCard and GetAheadCard used to each
    /// reimplement by hand — any future visual tweak (border color, divider spacing)
    /// now only has one place to update instead of two that could quietly drift apart
    /// (round-5 audit finding #30).</summary>
    private static Border Card<T>(List<T> items, Func<T, UIElement> rowBuilder)
    {
        var list = new StackPanel();
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
                list.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                });
            list.Children.Add(rowBuilder(items[i]));
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

    /// <summary>Shared by GetAheadRow and TaskRow — both used to build this
    /// identical block by hand (2026-07-14 round-6 audit finding #10), and
    /// had already drifted (GetAheadRow always showed it, TaskRow only for
    /// an incomplete task) by the time that was caught.</summary>
    private static TextBlock? DurationLabel(int? mins) =>
        mins is int m ? new TextBlock
        {
            Text = $"{m}m",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        } : null;

    private static TextBlock? MentorNoteLine(string? mentorNote) =>
        mentorNote is { Length: > 0 } n ? new TextBlock
        {
            Text = "💡 " + n,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        } : null;

    private void AddDetailsLink(Grid grid, AssignedTask item, int column)
    {
        if (item.Task.Detail is not { Length: > 0 }) return;
        var details = TaskDetailsLink.Build(XamlRoot, item.Task,
            new Thickness(6, 0, 0, 0), VerticalAlignment.Center);
        Grid.SetColumn(details, column);
        grid.Children.Add(details);
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
        if (MentorNoteLine(item.Task.MentorNote) is { } mentorLine) textCol.Children.Add(mentorLine);
        Grid.SetColumn(textCol, 0);
        grid.Children.Add(textCol);

        if (DurationLabel(item.Task.DurationMin) is { } dur)
        {
            Grid.SetColumn(dur, 1);
            grid.Children.Add(dur);
        }

        var start = new Button { Content = "Start now", VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(start, $"Start now: {item.Task.Text}");
        start.Click += (_, _) => PlanScoreAction.Run(plan,
            (score, p) => score.MoveTaskToToday(p, item.Task.Text), "TodayPage.GetAhead",
            ok => { Render(); (App.MainWindow as MainWindow)?.RefreshScore(); if (!ok) SaveErrorBar.IsOpen = true; });
        Grid.SetColumn(start, 2);
        grid.Children.Add(start);

        AddDetailsLink(grid, item, column: 3);
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
        AutomationProperties.SetName(check, item.Task.Text);
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
                Text = item.OverdueCaption(plan.PlanDay),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
        if (MentorNoteLine(item.Task.MentorNote) is { } mentorLine) textCol.Children.Add(mentorLine);
        textCol.Children.Add(Views.TaskNoteView.Build(note, plan.Id, item.Task.Text, "TodayPage.SetTaskNote",
            onError: () => SaveErrorBar.IsOpen = true));
        Grid.SetColumn(textCol, 1);
        grid.Children.Add(textCol);

        if (!item.Completed && DurationLabel(item.Task.DurationMin) is { } dur)
        {
            Grid.SetColumn(dur, 2);
            grid.Children.Add(dur);
        }

        // The "explanation of tasks" — detail is the concrete how-to, which
        // (unlike the mentor note) never had anywhere to display at all.
        AddDetailsLink(grid, item, column: 3);
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
            // stay showing the new, unsaved state forever, since without
            // this Render() call nothing here ever refreshes it back to
            // what's actually in the database (round-5 audit finding #4).
            // Plan ID + day, not the task text — the log file has no
            // retention/redaction of its own, unlike the diary it sits
            // alongside (privacy audit).
            Log.Error($"Toggle completion '{plan.Id}' day {item.AssignedDay}", ex);
            Render();
            SaveErrorBar.IsOpen = true;
        }
    }
}
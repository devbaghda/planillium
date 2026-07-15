using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;
using MentorOverseer.App.Views;

using Microsoft.UI.Xaml.Automation;
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
        // Every render clears any stale error from a previous failed save —
        // otherwise the banner outlives the failure it reported, even once
        // later actions succeed fine (round-5 audit finding #7).
        SaveErrorBar.IsOpen = false;

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
            // Shares Today's exact empty-state panel now, rather than a hand-built
            // near-copy that had already drifted in wording (round-5 audit finding #19;
            // originally added to match Today per 2026-07-09 audit finding #33).
            Subtitle.Text = "No active plans.";
            Sections.Children.Add(Views.EmptyPlansState.Build(this,
                "Add a plan on the Plans page, or right here, to see it on your schedule.", Render));
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

    /// <summary>Cheap per-day data — building this for every day in a long plan is
    /// trivial; building the actual DayCard UI tree for every day is not (that was
    /// the whole cost problem). ItemsRepeater only calls the factory below for
    /// days that are actually near the viewport.</summary>
    private sealed record ScheduleDayItem(Plan Plan, int Day, bool IsToday, bool IsOff,
        List<AssignedTask> DayTasks, Dictionary<string, string> Notes);

    private sealed class DayCardElementFactory : IElementFactory
    {
        private readonly Func<ScheduleDayItem, UIElement> _build;
        public DayCardElementFactory(Func<ScheduleDayItem, UIElement> build) => _build = build;
        public UIElement GetElement(ElementFactoryGetArgs args) => _build((ScheduleDayItem)args.Data);
        // No pooling: each DayCard closes over its own plan/day/task state rather
        // than binding to a reusable template, so a recycled container would need
        // its handlers rewired anyway. Discarding and rebuilding on re-entry into
        // view still avoids the original problem (building every day up front).
        public void RecycleElement(ElementFactoryRecycleArgs args) { }
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

        // Data only here — no UI construction — so a 160-day plan costs a
        // list of records, not 160 Grids/Borders/TextBlocks built up front.
        var items = new List<ScheduleDayItem>();
        var todayIndex = -1;

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

            if (isToday) todayIndex = items.Count;
            items.Add(new ScheduleDayItem(plan, day, isToday, isOff, dayTasks, notes));
        }

        // Each plan gets its own bounded scroll box instead of every plan's
        // day cards pouring into one shared list — with 2 active plans,
        // reaching the second plan used to mean scrolling past the whole
        // first one first. The ScrollViewer is also what makes the
        // ItemsRepeater below virtualize: it only realizes DayCards for
        // days actually within (or near) this 440px viewport.
        var repeater = new ItemsRepeater
        {
            ItemsSource = items,
            Layout = new StackLayout { Spacing = 12 },
            ItemTemplate = new DayCardElementFactory(item =>
                DayCard(item.Plan, item.Day, item.IsToday, item.IsOff, item.DayTasks, item.Notes)),
        };
        var scroller = new ScrollViewer
        {
            Height = 440,
            Padding = new Thickness(0, 0, 8, 0),
            Content = repeater,
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

        // Land this plan's own viewport on its own today, not Day 1. Deferred
        // to Loaded (rather than called inline like the old eager StackPanel
        // version could) because the repeater can't realize/measure an item
        // by index until it's actually been through a layout pass.
        if (todayIndex >= 0)
        {
            var idx = todayIndex;
            repeater.Loaded += (_, _) =>
            {
                if (repeater.GetOrCreateElement(idx) is FrameworkElement el)
                    el.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.12 });
            };
        }
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
            Text = date.ToDisplayDate(),
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
            AutomationProperties.SetName(toggle,
                $"{(isOff ? "Undo day off" : "Day off")}: {date.ToString("dddd dd.MM", CultureInfo.InvariantCulture)}");
            var d = day;
            toggle.Click += (_, _) => PlanScoreAction.Run(plan,
                (score, p) => { if (isOff) score.UnmarkDayOff(p, d); else score.MarkDayOff(p, d); },
                "SchedulePage.DayOff",
                ok => { Render(); (App.MainWindow as MainWindow)?.RefreshScore(); if (!ok) SaveErrorBar.IsOpen = true; });
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
        AutomationProperties.SetName(check, t.Task.Text);
        check.Checked += (_, _) => ToggleDone(plan, t, true);
        check.Unchecked += (_, _) => ToggleDone(plan, t, false);
        Grid.SetColumn(check, 0);
        row.Children.Add(check);

        var textPanel = new StackPanel();
        var title = new TextBlock
        {
            Text = t.Task.Text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TaskRowStyle.TitleForeground(t),
        };
        textPanel.Children.Add(title);
        var meta = new List<string>();
        if (t.Task.DurationMin is int dur) meta.Add($"{dur} min");
        if (t.AssignedDay != t.OriginalDay) meta.Add($"moved from day {t.OriginalDay}");
        // Same "N day(s) late" caption Today shows — overdue used to be
        // color-only here, which loses the signal for anyone who can't
        // rely on color (2026-07-09 audit finding #17). Now the identical
        // shared method, not a second hand-typed copy (round-5 audit
        // finding #31).
        if (t.Overdue)
            meta.Add(t.OverdueCaption(planDay));
        if (meta.Count > 0)
            textPanel.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", meta),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = Res("TextFillColorTertiaryBrush"),
            });
        textPanel.Children.Add(TaskNoteView.Build(note, plan.Id, t.Task.Text, "SchedulePage.SetTaskNote",
            onError: () => SaveErrorBar.IsOpen = true));
        Grid.SetColumn(textPanel, 1);
        row.Children.Add(textPanel);

        // Move/Reschedule and Details are independent — a task can be both
        // overdue and have a detail worth reading — so they share one
        // action column instead of competing for it.
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        if (!t.Completed)
        {
            if (t.AssignedDay > planDay)
            {
                var move = new HyperlinkButton
                {
                    Content = "Move to today",
                    FontSize = 12,
                    Padding = new Thickness(6, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                AutomationProperties.SetName(move, $"Move to today: {t.Task.Text}");
                move.Click += (_, _) => PlanScoreAction.Run(plan,
                    (score, p) => score.MoveTaskToToday(p, t.Task.Text), "SchedulePage.MoveToToday",
                    ok => { Render(); (App.MainWindow as MainWindow)?.RefreshScore(); if (!ok) SaveErrorBar.IsOpen = true; });
                actions.Children.Add(move);
            }

            // Reschedule to a specific day is available for every open task,
            // not just overdue ones — a future task's own day can turn out
            // to be inconvenient too, and this is the only action that lets
            // you pick exactly which day, whereas "Move to today" only ever
            // targets today. The accrued penalty for any already-late days
            // stands; this only stops it from accruing further.
            var reschedule = new HyperlinkButton
            {
                Content = "Reschedule…",
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            AutomationProperties.SetName(reschedule, $"Reschedule: {t.Task.Text}");
            reschedule.Click += async (_, _) =>
            {
                var ok = await Dialogs.RescheduleTaskDialog.ShowAsync(XamlRoot, plan, t);
                if (ok == true)
                {
                    Render();
                    (App.MainWindow as MainWindow)?.RefreshScore();
                }
                else if (ok == false)
                {
                    // Render() resets SaveErrorBar — set it after, not before, or the reset wipes it.
                    Render();
                    SaveErrorBar.IsOpen = true;
                }
            };
            actions.Children.Add(reschedule);
        }

        if (t.Task.Detail is { Length: > 0 })
        {
            var details = TaskDetailsLink.Build(XamlRoot, t.Task,
                new Thickness(6, 0, 6, 0), VerticalAlignment.Top);
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
        var ok = true;
        try
        {
            using var db = new Database();
            db.SaveCompletion(plan.Id, t.AssignedDay, t.Task.Text, done);
            t.Completed = done;
            (App.MainWindow as MainWindow)?.RefreshScore();
        }
        catch (Exception ex)
        {
            // Plan ID + day, not the task text — the log file has no
            // retention/redaction of its own, unlike the diary it sits
            // alongside (privacy audit finding).
            Log.Error($"SchedulePage.ToggleDone '{plan.Id}' day {t.AssignedDay}", ex);
            ok = false;
        }
        // Render() resets SaveErrorBar — set it after, not before, or the reset wipes it.
        Render();
        if (!ok) SaveErrorBar.IsOpen = true;
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
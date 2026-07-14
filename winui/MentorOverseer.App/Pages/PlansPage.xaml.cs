using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class PlansPage : Page
{
    public PlansPage()
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

    private void Render()
    {
        ActiveList.Children.Clear();
        ArchivedList.Children.Clear();

        List<Plan> plans;
        Dictionary<(string, int, string), bool> completions;
        try
        {
            plans = PlanStore.LoadActivePlans(out var failedFiles);
            if (failedFiles.Count > 0)
            {
                LoadErrorBar.Title = failedFiles.Count == 1
                    ? $"Couldn't load '{failedFiles[0]}'"
                    : $"Couldn't load {failedFiles.Count} plan files";
                LoadErrorBar.Message = $"{string.Join(", ", failedFiles)} — the file's JSON " +
                    "may be malformed. Fix it manually or re-generate the plan; it won't " +
                    "appear here until then. See the log for details.";
                LoadErrorBar.IsOpen = true;
            }
            else
            {
                LoadErrorBar.IsOpen = false;
            }
            using var db = new Database();
            completions = db.LoadCompletions();

            foreach (var plan in plans)
            {
                var tasks = PlanStore.TasksFor(plan, db, completions);
                var done = tasks.Count(t => t.Completed);
                var complete = tasks.Count > 0 && done == tasks.Count;

                // The plan's originally-due date never moves on its own —
                // it's derived from each task's un-overridden Day (or the
                // plan's own total_days field), neither of which changes
                // when a task is rescheduled. The currently-due date is the
                // same calculation but from AssignedDay, which does move —
                // later from a reschedule/day-off pushing things back,
                // earlier from pulling a task forward and compacting the
                // gap it leaves. The gap between the two is exactly "how
                // many days later/earlier the plan will now finish."
                var originalEndDate = plan.DateForPlanDay(plan.TotalDaysComputed);
                var currentEndDate = plan.DateForPlanDay(
                    tasks.Count > 0 ? tasks.Max(t => t.AssignedDay) : plan.TotalDaysComputed);
                var driftDays = currentEndDate.DayNumber - originalEndDate.DayNumber;

                ActiveList.Children.Add(PlanCard(plan, done, tasks.Count, complete, originalEndDate, driftDays));
            }
            if (plans.Count == 0)
                ActiveList.Children.Add(new TextBlock
                {
                    Text = "No active plans.",
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                });
        }
        catch (Exception ex)
        {
            Log.Error("PlansPage.Render", ex);
            ActiveList.Children.Add(new TextBlock { Text = "Couldn't load plans: " + ex.Message });
            return;
        }

        var archiveDir = Path.Combine(AppPaths.Root, "plans", "archive");
        var archived = Directory.Exists(archiveDir)
            ? Directory.GetFiles(archiveDir, "*.json").OrderBy(f => f).ToList()
            : new List<string>();
        foreach (var file in archived)
            ArchivedList.Children.Add(ArchivedRow(file, plans.Count));
        if (archived.Count == 0)
            ArchivedList.Children.Add(new TextBlock
            {
                Text = "No archived plans.",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
    }

    private Border PlanCard(Plan plan, int done, int total, bool complete,
        DateOnly originalEndDate, int driftDays)
    {
        var grid = new Grid { Padding = new Thickness(18, 14, 18, 14), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(new TextBlock
        {
            Text = plan.Name,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        var metaLine = $"Day {plan.PlanDay} of {plan.TotalDaysComputed} · {done}/{total} tasks done";
        if (plan.ExcludedWeekdays.Count > 0)
        {
            var names = plan.ExcludedWeekdays
                .Select(d => ((DayOfWeek)d).ToString()[..3]);
            metaLine += $" · excludes {string.Join(", ", names)}";
        }
        left.Children.Add(new TextBlock
        {
            Text = metaLine,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 13,
        });

        // Originally-due date is fixed the moment the plan is created — it
        // comes from each task's un-overridden Day (or the plan's own
        // total_days), neither of which a reschedule ever touches. The
        // "(+Nd)"/"(-Nd)" is how far the plan's actual finish has since
        // drifted from that: later from reschedules/day-offs pushing tasks
        // back, earlier from pulling a task forward and compacting the gap.
        var dueLine = $"Originally due {originalEndDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
        if (driftDays != 0)
            dueLine += driftDays > 0 ? $" — now {driftDays}d later" : $" — now {-driftDays}d earlier";
        left.Children.Add(new TextBlock
        {
            Text = dueLine,
            Foreground = (Brush)Application.Current.Resources[
                driftDays > 0 ? "SystemFillColorCriticalBrush"
                : driftDays < 0 ? "SystemFillColorSuccessBrush"
                : "TextFillColorTertiaryBrush"],
            FontSize = 12,
        });
        var bar = new ProgressBar
        {
            Value = total > 0 ? done * 100.0 / total : 0,
            Margin = new Thickness(0, 6, 0, 0),
        };
        left.Children.Add(bar);
        grid.Children.Add(left);

        if (plan.Briefing != null)
        {
            var briefing = new Button { Content = "📋 Briefing", VerticalAlignment = VerticalAlignment.Center };
            briefing.Click += async (_, _) => await BriefingDialog.ShowAsync(XamlRoot, plan);
            Grid.SetColumn(briefing, 1);
            grid.Children.Add(briefing);
        }

        // "task" everywhere else in the app (Today/Schedule/Reports); this
        // used to say "step" here and in the dialog title, while the
        // dialog's own field was already headered "Task" three lines below
        // it (2026-07-09 audit finding #32).
        var addStep = new Button { Content = "+ Add task", VerticalAlignment = VerticalAlignment.Center };
        addStep.Click += async (_, _) =>
        {
            if (await AddTaskDialog.ShowAsync(XamlRoot, plan)) Render();
        };
        Grid.SetColumn(addStep, 2);
        grid.Children.Add(addStep);

        var excludeDays = new Button { Content = "Excluded days…", VerticalAlignment = VerticalAlignment.Center };
        excludeDays.Click += async (_, _) =>
        {
            if (await ExcludedWeekdaysDialog.ShowAsync(XamlRoot, plan)) Render();
        };
        Grid.SetColumn(excludeDays, 3);
        grid.Children.Add(excludeDays);

        var archive = new Button
        {
            Content = complete ? "Archive ✓" : "Archive",
            IsEnabled = complete,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(archive,
            complete ? "All tasks done — free the slot" : "Enabled once every task is complete");
        archive.Click += async (_, _) => await ArchiveAsync(plan);
        Grid.SetColumn(archive, 4);
        grid.Children.Add(archive);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
    }

    private async Task ArchiveAsync(Plan plan)
    {
        var confirm = new ContentDialog
        {
            Title = $"Archive '{plan.Name}'?",
            Content = "It disappears from Today and frees a plan slot. You can restore it below any time.",
            PrimaryButtonText = "Archive",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        var src = Path.Combine(AppPaths.ActivePlansDir, $"{plan.Id}.json");
        var dstDir = Path.Combine(AppPaths.Root, "plans", "archive");
        Directory.CreateDirectory(dstDir);
        if (File.Exists(src))
            File.Move(src, Path.Combine(dstDir, $"{plan.Id}.json"), overwrite: false);
        Render();
    }

    private Grid ArchivedRow(string file, int activeCount)
    {
        string name;
        try
        {
            name = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file))
                .RootElement.GetProperty("name").GetString() ?? Path.GetFileName(file);
        }
        catch (Exception ex)
        {
            // Runs once per archived plan file on page render, not a hot
            // loop, so logging every occurrence is fine here — unlike the
            // tracker's per-minute poll catches, this doesn't need
            // once-per-run suppression (2026-07-09 audit finding #26).
            Log.Warn("PlansPage.ArchivedRow", $"couldn't read name from {Path.GetFileName(file)}: {ex.Message}");
            name = Path.GetFileName(file);
        }

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });

        var restore = new Button { Content = "Restore", IsEnabled = activeCount < 2 };
        restore.Click += (_, _) =>
        {
            var dst = Path.Combine(AppPaths.ActivePlansDir, Path.GetFileName(file));
            if (!File.Exists(dst)) File.Move(file, dst);
            Render();
        };
        Grid.SetColumn(restore, 1);
        grid.Children.Add(restore);
        return grid;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (await AddPlanDialog.ShowAsync(this))
            Render();
    }
}

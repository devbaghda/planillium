using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Dialogs;
using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class PlansPage : Page
{
    public PlansPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    private void Render()
    {
        ActiveList.Children.Clear();
        ArchivedList.Children.Clear();

        List<Plan> plans;
        Dictionary<(string, int, string), bool> completions;
        try
        {
            plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            completions = db.LoadCompletions();

            foreach (var plan in plans)
            {
                var tasks = PlanStore.TasksFor(plan, db, completions);
                var done = tasks.Count(t => t.Completed);
                var complete = tasks.Count > 0 && done == tasks.Count;
                ActiveList.Children.Add(PlanCard(plan, done, tasks.Count, complete));
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

    private Border PlanCard(Plan plan, int done, int total, bool complete)
    {
        var grid = new Grid { Padding = new Thickness(18, 14, 18, 14), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(new TextBlock
        {
            Text = plan.Name,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        left.Children.Add(new TextBlock
        {
            Text = $"Day {plan.PlanDay} of {plan.TotalDaysComputed} · {done}/{total} tasks done",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 13,
        });
        var bar = new ProgressBar
        {
            Value = total > 0 ? done * 100.0 / total : 0,
            Margin = new Thickness(0, 6, 0, 0),
        };
        left.Children.Add(bar);
        grid.Children.Add(left);

        var archive = new Button
        {
            Content = complete ? "Archive ✓" : "Archive",
            IsEnabled = complete,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(archive,
            complete ? "All tasks done — free the slot" : "Enabled once every task is complete");
        archive.Click += async (_, _) => await ArchiveAsync(plan);
        Grid.SetColumn(archive, 1);
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
        catch { name = Path.GetFileName(file); }

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

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Views;

/// <summary>
/// "My Tasks · TickTick" section — extracted from TodayPage's code-behind so
/// the page stops mixing rendering with network I/O (audit finding #17).
/// </summary>
public static class TickTickSection
{
    public static async Task LoadAsync(StackPanel host)
    {
        host.Children.Clear();
        host.Children.Add(Header());

        if (!TickTickService.IsAuthorized)
        {
            host.Children.Add(Muted(
                "Not connected. Connect once in the Python app — this app reuses the same token."));
            return;
        }

        var loading = Muted("Loading…");
        host.Children.Add(loading);
        try
        {
            var tasks = await TickTickService.TasksDueTodayAsync();
            host.Children.Remove(loading);
            if (tasks.Count == 0)
            {
                host.Children.Add(Muted("No personal TickTick tasks due today."));
                return;
            }

            var list = new StackPanel();
            foreach (var t in tasks)
            {
                if (list.Children.Count > 0)
                    list.Children.Add(new Border
                    {
                        Height = 1,
                        Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                    });
                list.Children.Add(TaskRow(t, host));
            }
            host.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = list,
            });
        }
        catch (Exception ex)
        {
            Log.Error("TickTickSection.Load", ex);
            host.Children.Remove(loading);
            host.Children.Add(Muted(
                "TickTick sync failed — the token may have expired. Reconnect once in the " +
                "Python app and this section comes back. (" + ex.Message + ")"));
        }
    }

    private static Grid TaskRow(TickTickService.TtTask t, StackPanel host)
    {
        var grid = new Grid { Padding = new Thickness(16, 10, 16, 10), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox { MinWidth = 0 };
        // Screen readers otherwise announce a bare "checkbox" with no label.
        AutomationProperties.SetName(check, $"Complete: {t.Title}");
        check.Checked += async (_, _) =>
        {
            try
            {
                await TickTickService.CompleteTaskAsync(t.ProjectId, t.Id);
                await LoadAsync(host);
            }
            catch (Exception ex)
            {
                Log.Error("TickTick complete", ex);
                check.IsChecked = false;
            }
        };
        grid.Children.Add(check);

        var name = new TextBlock
        {
            Text = t.Title, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var proj = new Border
        {
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 3, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            Child = new TextBlock
            {
                Text = t.ProjectName, FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            },
        };
        Grid.SetColumn(proj, 2);
        grid.Children.Add(proj);
        return grid;
    }

    private static TextBlock Header() => new()
    {
        Text = "My Tasks  ·  TickTick",
        Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        Margin = new Thickness(0, 18, 0, 2),
    };

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        Margin = new Thickness(2, 8, 0, 8),
    };
}

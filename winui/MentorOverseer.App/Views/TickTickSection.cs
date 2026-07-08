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
            host.Children.Add(Muted("Not connected."));
            var connect = new HyperlinkButton { Content = "Connect TickTick…", FontSize = 13 };
            connect.Click += async (_, _) =>
            {
                if (await Dialogs.TickTickConnectDialog.ShowAsync(host.XamlRoot))
                    await LoadAsync(host);
            };
            host.Children.Add(connect);
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
                "TickTick sync failed. (" + ex.Message + ")"));
            var reconnect = new HyperlinkButton { Content = "Reconnect TickTick…", FontSize = 13 };
            reconnect.Click += async (_, _) =>
            {
                if (await Dialogs.TickTickConnectDialog.ShowAsync(host.XamlRoot))
                    await LoadAsync(host);
            };
            host.Children.Add(reconnect);
        }
    }

    private static Grid TaskRow(TickTickService.TtTask t, StackPanel host)
    {
        var grid = new Grid { Padding = new Thickness(16, 10, 16, 10), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

        // Priority badge (TickTick: 0=None, 1=Low, 3=Medium, 5=High) — only
        // shown when the task actually has one set.
        if (PriorityLabel(t.Priority) is { } pl)
        {
            var priority = new TextBlock
            {
                Text = pl.Text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                CharacterSpacing = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources[pl.BrushKey],
            };
            Grid.SetColumn(priority, 2);
            grid.Children.Add(priority);
        }

        // Flat bordered tag, not a filled oval — matches the app's plainer
        // chip style elsewhere instead of standing out as a colored pill.
        var proj = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = t.ProjectName, FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            },
        };
        Grid.SetColumn(proj, 3);
        grid.Children.Add(proj);
        return grid;
    }

    private static (string Text, string BrushKey)? PriorityLabel(int priority) => priority switch
    {
        5 => ("HIGH", "SystemFillColorCriticalBrush"),
        3 => ("MED", "SystemFillColorCautionBrush"),
        1 => ("LOW", "TextFillColorTertiaryBrush"),
        _ => null,
    };

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

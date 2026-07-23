using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// "What did I miss" recap — shown right after the main window is brought to the foreground if
/// anything fired while it wasn't being looked at. Closes the 2026-07-22 gap where the tray
/// icon's red dot had no way to actually see what it was about: clicking the icon just opened
/// the window with nothing pointing back at the toast that had just fired. Only shows when
/// <see cref="NotificationCenter.TakePending"/> actually returns something — most activations
/// (there's nothing pending) show nothing at all.
/// </summary>
public static class PendingNotificationsDialog
{
    public static async Task ShowAsync(MainWindow window, List<PendingNotification> items)
    {
        if (items.Count == 0) return;

        var panel = new StackPanel { Spacing = 12 };
        // Oldest first — reads like a short timeline of what happened while you were away,
        // rather than a most-recent-first log.
        foreach (var item in items)
        {
            var row = new StackPanel { Spacing = 2 };
            var when = DateTime.TryParse(item.AtIso, out var at)
                ? $"{at.ToDisplayDate()} {at.ToIsoTimeOfDay()}"
                : item.AtIso;
            row.Children.Add(new TextBlock
            {
                Text = $"{when} — {item.Title}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            row.Children.Add(new TextBlock
            {
                Text = item.Message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            panel.Children.Add(row);
        }

        var dialog = DialogControls.Build(window.Content.XamlRoot,
            items.Count == 1 ? "While you were away" : $"While you were away ({items.Count})",
            new ScrollViewer { Content = panel, MaxHeight = 360 }, closeButtonText: "Close");

        await DialogGate.ShowAsync(dialog);
    }
}

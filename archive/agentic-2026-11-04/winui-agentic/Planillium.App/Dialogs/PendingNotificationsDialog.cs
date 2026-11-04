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
    // Which recorded actions this recap can actually act on, and what to call the button —
    // anything else (or a notification with no action at all, e.g. the focus-nudge alert)
    // just shows as plain text, same as before. Keeps an unrecognized future action string
    // from rendering a button that dispatches to nothing.
    private static readonly Dictionary<string, string> ActionLabels = new()
    {
        [ToastArgs.Kickoff] = "Open plan",
        [ToastArgs.IdleReturn] = "Log it",
        [ToastArgs.Review] = "Review day",
    };

    public static async Task ShowAsync(MainWindow window, List<PendingNotification> items)
    {
        if (items.Count == 0) return;

        var panel = new StackPanel { Spacing = 12 };
        ContentDialog dialog = null!;
        string? runAction = null;
        IDictionary<string, string>? runArgs = null;

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

            // The message text above ("click to log where you were" etc.) was written for the
            // live toast, where clicking really did open the real dialog — reached this way
            // (app reopened some other way, toast never clicked) it used to be nothing but
            // inert copy with no click behind it. Give actionable items back their action
            // instead of just repeating a promise this dialog couldn't keep (2026-07-28).
            if (item.Args.TryGetValue(ToastArgs.Action, out var action) &&
                ActionLabels.TryGetValue(action, out var label))
            {
                var actBtn = new Button { Content = label, Margin = new Thickness(0, 4, 0, 0) };
                actBtn.Click += (_, _) =>
                {
                    runAction = action;
                    runArgs = item.Args;
                    dialog.Hide();
                };
                row.Children.Add(actBtn);
            }

            panel.Children.Add(row);
        }

        dialog = DialogControls.Build(window.Content.XamlRoot,
            items.Count == 1 ? "While you were away" : $"While you were away ({items.Count})",
            new ScrollViewer { Content = panel, MaxHeight = 360 }, closeButtonText: "Close");

        await DialogGate.ShowAsync(dialog);

        if (runAction is { } a && runArgs is { } args)
        {
            try { await window.HandleNotificationActivation(a, args); }
            catch (Exception ex) { Log.Error("PendingNotificationsDialog action dispatch", ex); }
        }
    }
}

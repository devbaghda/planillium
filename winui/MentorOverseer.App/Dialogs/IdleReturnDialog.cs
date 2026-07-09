using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// "Welcome back" — the softened replacement for the Python app's blocking
/// idle interrogation: one-tap chips (fixed set + your most frequent past
/// answers) for the common single-activity case, or "Split into activities"
/// for the "it was actually several things" case — the WinUI port of the
/// Python dialog's multi-row editor, minus the modal blocking. Logs through
/// ActivityTracker.LogIdleAnswer (once per segment when split), so the
/// idle-answer library still reclassifies matched text per segment.
/// </summary>
public static class IdleReturnDialog
{
    private static readonly string[] FixedChips =
        { "Lunch", "Break", "Errand", "Work off-screen" };

    private const int ChipsPerRow = 4;

    public static async Task ShowAsync(MainWindow window, int idleMinutes, DateTime idleStart)
    {
        if (window.Tracker is not { } tracker) return;

        List<string> frequent;
        try
        {
            using var db = new Database();
            frequent = db.MostFrequentIdleAnswers();
        }
        catch (Exception ex)
        {
            Log.Error("IdleReturnDialog.MostFrequentIdleAnswers", ex);
            frequent = new List<string>();
        }
        var chips = FixedChips
            .Concat(frequent.Where(f => !FixedChips.Contains(f, StringComparer.OrdinalIgnoreCase)))
            .Take(8)
            .ToArray();

        var root = new StackPanel { Spacing = 12, MinWidth = 460 };
        root.Children.Add(new TextBlock
        {
            Text = $"You were away {idleMinutes} min. What was it, roughly?",
            TextWrapping = TextWrapping.Wrap,
        });

        // ── single-answer mode (default): chips + free text ──────────────
        var singleRoot = new StackPanel { Spacing = 12 };
        var input = new TextBox
        {
            PlaceholderText = "…or type it (matches your idle-answer library)",
        };

        string? chosen = null;
        ContentDialog dialog = null!;

        var chipRows = new StackPanel { Spacing = 6 };
        for (var i = 0; i < chips.Length; i += ChipsPerRow)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var chip in chips.Skip(i).Take(ChipsPerRow))
            {
                var b = new Button { Content = chip };
                b.Click += (_, _) => { chosen = chip; dialog.Hide(); };
                row.Children.Add(b);
            }
            chipRows.Children.Add(row);
        }
        singleRoot.Children.Add(chipRows);
        singleRoot.Children.Add(input);

        var splitLink = new HyperlinkButton
        {
            Content = "It was actually several things — split it",
            Padding = new Thickness(0),
        };
        singleRoot.Children.Add(splitLink);
        root.Children.Add(singleRoot);

        // ── split mode: hidden until "split it" is clicked ───────────────
        var splitRoot = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
        root.Children.Add(splitRoot);

        var rowsPanel = new StackPanel { Spacing = 6 };
        splitRoot.Children.Add(rowsPanel);

        var addRowBtn = new Button { Content = "+ Add activity", Padding = new Thickness(0) };
        var remainingText = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(addRowBtn);
        toolbar.Children.Add(remainingText);
        splitRoot.Children.Add(toolbar);

        dialog = new ContentDialog
        {
            Title = "Welcome back",
            Content = root,
            PrimaryButtonText = "Log it",
            CloseButtonText = "Skip",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var rows = new List<(NumberBox Dur, TextBox Desc, Button Remove)>();

        void UpdateSplitState()
        {
            var used = rows.Sum(r => double.IsNaN(r.Dur.Value) ? 0 : (int)r.Dur.Value);
            var remaining = idleMinutes - used;
            var allDurOk = rows.All(r => !double.IsNaN(r.Dur.Value) && r.Dur.Value > 0);
            var allDescOk = rows.All(r => r.Desc.Text.Trim().Length > 0);

            remainingText.Text = remaining < 0
                ? $"{-remaining} min over — reduce a duration"
                : remaining > 0
                    ? $"{remaining} min unaccounted — OK to save"
                    : "All time accounted";

            foreach (var r in rows) r.Remove.IsEnabled = rows.Count > 1;

            dialog.IsPrimaryButtonEnabled = remaining >= 0 && allDurOk && allDescOk;
        }

        void AddRow(int? prefillMin)
        {
            var durBox = DialogControls.MinutesBox(prefillMin);
            var descBox = new TextBox
            {
                PlaceholderText = "e.g. lunch, walked the dog…",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var removeBtn = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(removeBtn, "Remove this activity");

            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(durBox, 0); Grid.SetColumn(descBox, 1); Grid.SetColumn(removeBtn, 2);
            row.Children.Add(durBox);
            row.Children.Add(descBox);
            row.Children.Add(removeBtn);
            rowsPanel.Children.Add(row);

            var entry = (durBox, descBox, removeBtn);
            rows.Add(entry);

            durBox.ValueChanged += (_, _) => UpdateSplitState();
            descBox.TextChanged += (_, _) => UpdateSplitState();
            removeBtn.Click += (_, _) =>
            {
                rowsPanel.Children.Remove(row);
                rows.Remove(entry);
                UpdateSplitState();
            };
            UpdateSplitState();
        }

        var splitMode = false;
        splitLink.Click += (_, _) =>
        {
            splitMode = true;
            singleRoot.Visibility = Visibility.Collapsed;
            splitRoot.Visibility = Visibility.Visible;
            if (rows.Count == 0) AddRow(idleMinutes);
            UpdateSplitState();
        };
        addRowBtn.Click += (_, _) => AddRow(null);

        var result = await DialogGate.ShowAsync(dialog);

        if (chosen is null && result != ContentDialogResult.Primary)
        {
            tracker.LogIdleAnswer(idleStart, idleMinutes, "dismissed");
            return;
        }

        if (splitMode && chosen is null)
        {
            var t = idleStart;
            foreach (var (dur, desc, _) in rows)
            {
                var mins = (int)dur.Value;
                tracker.LogIdleAnswer(t, mins, desc.Text.Trim());
                t = t.AddMinutes(mins);
            }
            return;
        }

        var text = chosen ?? input.Text.Trim();
        tracker.LogIdleAnswer(idleStart, idleMinutes, text.Length > 0 ? text : "dismissed");
    }
}

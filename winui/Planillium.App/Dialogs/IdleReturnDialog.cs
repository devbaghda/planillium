using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

using Microsoft.UI.Xaml.Automation;
namespace Planillium.App.Dialogs;

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

    /// <summary>
    /// Poll-loop entry point: shows the interactive dialog directly when the
    /// window is actually on screen, otherwise raises a "welcome back" toast
    /// so the prompt still reaches the user on PC activation after an
    /// absence even while the app sits in the tray — this fires from
    /// ActivityTracker's background poll, so most of the time nobody is
    /// looking at the (hidden) main window. If the toast is never clicked,
    /// nothing is lost: the evening review's gap sweep (ActivityTracker.
    /// PendingDayGap) picks up the same unaccounted stretch later.
    /// </summary>
    public static Task Trigger(MainWindow window, int idleMinutes, DateTime idleStart)
    {
        // No once-per-day throttle here, unlike Kickoff/Review — a real
        // idle-return can legitimately happen several times in one day
        // (lunch, an errand, an afternoon break), and each is a distinct
        // event worth its own prompt. The Tag below still stops them from
        // stacking up in Action Center: a second toast before the first is
        // acted on replaces it rather than piling beside it, but it never
        // suppresses a genuinely new return.
        return PromptRouter.ShowOrToast(window, () => ShowAsync(window, idleMinutes, idleStart),
            () => false, () => { },
            "Welcome back.", "Quick check-in — click to log where you were.",
            ToastArgs.IdleReturn,
            (ToastArgs.Action, ToastArgs.IdleReturn),
            (ToastArgs.Mins, idleMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            (ToastArgs.Start, idleStart.ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <param name="leadIn">Optional context line shown above the usual
    /// question — used when this dialog is standing in for something other
    /// than a plain return-from-idle (e.g. the evening review's gap sweep),
    /// so it doesn't read as an unrelated, unexplained interruption.</param>
    public static async Task ShowAsync(MainWindow window, int idleMinutes, DateTime idleStart,
        string? leadIn = null)
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
        if (leadIn is { Length: > 0 })
            root.Children.Add(new TextBlock
            {
                Text = leadIn,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        var idleEnd = idleStart.AddMinutes(idleMinutes);
        root.Children.Add(new TextBlock
        {
            Text = $"You were away {idleMinutes} min ({idleStart.ToIsoTimeOfDay()}–{idleEnd.ToIsoTimeOfDay()}). What was it, roughly?",
            TextWrapping = TextWrapping.Wrap,
        });

        // ── single-answer mode (default): chips + free text ──────────────
        var singleRoot = new StackPanel { Spacing = 12 };
        var input = new TextBox
        {
            PlaceholderText = "…or type it (matches your idle-answer library)",
        };
        AutomationProperties.SetName(input, "What you were doing");

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
            AutomationProperties.SetName(descBox, "Activity description");
            var removeBtn = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4) };
            AutomationProperties.SetName(removeBtn, "Remove this activity");

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

        try
        {
            if (chosen is null && result != ContentDialogResult.Primary)
            {
                tracker.LogIdleAnswer(idleStart, idleMinutes, "unaccounted time");
                return;
            }

            if (splitMode && chosen is null)
            {
                tracker.LogIdleAnswers(BuildSegments(idleStart, rows));
                return;
            }

            var text = chosen ?? input.Text.Trim();
            tracker.LogIdleAnswer(idleStart, idleMinutes, text.Length > 0 ? text : "unaccounted time");
        }
        catch (Exception ex)
        {
            // No page is guaranteed to be on screen here (this runs from a
            // background poll's idle-return event) — toast is the same
            // fallback the timed prompts already use to reach the user
            // outside of an open dialog (2026-07-14 round-6 audit finding #5).
            Log.Error("IdleReturnDialog.LogIdleAnswer(s)", ex);
            Services.ToastNotifier.Show("Couldn't save that",
                "The idle-time answer didn't save — the database was likely briefly busy.", tag: null);
        }
    }

    /// <summary>Turns the split-mode rows into back-to-back diary segments starting at
    /// idleStart — the one piece of ShowAsync that's pure data transformation rather
    /// than UI wiring or closure-shared dialog state, pulled out on its own so it can be
    /// read (and eventually tested) independently of the dialog around it (audit finding
    /// #5). The rest of ShowAsync stays one method deliberately — its closures (rows,
    /// dialog, chosen, splitMode) share too much mutable state specific to this one
    /// widget's two UI modes to split further without adding more complexity than it
    /// removes, the same reasoning ReportsPage.Diary.BuildDiarySection's own doc comment
    /// already gives for staying unsplit.</summary>
    private static List<(DateTime Start, int Minutes, string Description)> BuildSegments(
        DateTime idleStart, List<(NumberBox Dur, TextBox Desc, Button Remove)> rows)
    {
        var t = idleStart;
        var segments = new List<(DateTime, int, string)>();
        foreach (var (dur, desc, _) in rows)
        {
            var mins = (int)dur.Value;
            segments.Add((t, mins, desc.Text.Trim()));
            t = t.AddMinutes(mins);
        }
        return segments;
    }
}
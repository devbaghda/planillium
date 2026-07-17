using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

using Microsoft.UI.Xaml.Automation;
namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Splits one already-recorded diary entry (idle, dismissed, or any other
/// category) into several — the same row-editor idea as IdleReturnDialog's
/// split mode, but for a fixed, already-closed time block: durations must
/// sum to EXACTLY the original entry's length (not "≤", since shrinking a
/// recorded interval would silently lose time), and each row picks its own
/// category, since a recorded block isn't auto-classified from text.
/// </summary>
public static class SplitDiaryEntryDialog
{
    private static readonly (string Label, string Value)[] Categories =
    {
        ("On-plan", DiaryCategory.OnPlan), ("Off-plan", DiaryCategory.OffPlan), ("Paid", DiaryCategory.Paid),
        ("Neutral", DiaryCategory.Neutral), ("Idle", DiaryCategory.Idle),
    };

    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, long id, DateOnly date,
        string start, string end, int durationMin, string category, string window, string? description)
    {
        if (!TimeOnly.TryParseExact(start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            return false;

        var root = new StackPanel { Spacing = 10, MinWidth = 460 };
        root.Children.Add(new TextBlock
        {
            Text = $"Split this {durationMin}-minute entry ({start} → {end}) into several activities.",
            TextWrapping = TextWrapping.Wrap,
        });

        var rowsPanel = new StackPanel { Spacing = 6 };
        root.Children.Add(rowsPanel);

        var addRowBtn = new Button { Content = "+ Add activity", Padding = new Thickness(0) };
        var remainingText = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(addRowBtn);
        toolbar.Children.Add(remainingText);
        root.Children.Add(toolbar);

        var dialog = new ContentDialog
        {
            Title = "Split diary entry",
            Content = root,
            PrimaryButtonText = "Split",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        var rows = new List<(NumberBox Dur, ComboBox Cat, TextBox Desc, Button Remove)>();

        void UpdateState()
        {
            var used = rows.Sum(r => double.IsNaN(r.Dur.Value) ? 0 : (int)r.Dur.Value);
            var remaining = durationMin - used;
            var allDurOk = rows.All(r => !double.IsNaN(r.Dur.Value) && r.Dur.Value > 0);

            remainingText.Text = remaining == 0 ? "All time accounted"
                : remaining > 0 ? $"{remaining} min left to assign"
                : $"{-remaining} min over — reduce a duration";

            foreach (var r in rows) r.Remove.IsEnabled = rows.Count > 1;
            dialog.IsPrimaryButtonEnabled = remaining == 0 && allDurOk && rows.Count > 1;
        }

        void AddRow(int? prefillMin, string prefillCat, string? prefillDesc)
        {
            var durBox = DialogControls.MinutesBox(prefillMin);
            var catBox = new ComboBox { Width = 110 };
            foreach (var (label, value) in Categories)
                catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            catBox.SelectedIndex = Array.FindIndex(Categories, c => c.Value == prefillCat) is >= 0 and var ci ? ci : 0;
            AutomationProperties.SetName(catBox, "Category");
            var descBox = new TextBox
            {
                PlaceholderText = "description",
                Text = prefillDesc ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(descBox, "Activity description");
            var removeBtn = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4) };
            AutomationProperties.SetName(removeBtn, "Remove this activity");

            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(durBox, 0); Grid.SetColumn(catBox, 1);
            Grid.SetColumn(descBox, 2); Grid.SetColumn(removeBtn, 3);
            row.Children.Add(durBox);
            row.Children.Add(catBox);
            row.Children.Add(descBox);
            row.Children.Add(removeBtn);
            rowsPanel.Children.Add(row);

            var entry = (durBox, catBox, descBox, removeBtn);
            rows.Add(entry);

            durBox.ValueChanged += (_, _) => UpdateState();
            catBox.SelectionChanged += (_, _) => UpdateState();
            removeBtn.Click += (_, _) =>
            {
                rowsPanel.Children.Remove(row);
                rows.Remove(entry);
                UpdateState();
            };
            UpdateState();
        }

        // Two rows to start — that's the whole point of "split". First row
        // keeps the original category/description; the remaining time is
        // left for the user to fill in on the second (mirrors the idle
        // dialog's "first row = full total" convention, minus the case
        // where one row alone would be a no-op split).
        AddRow(durationMin, category, description);
        AddRow(null, category, null);

        var result = await DialogGate.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary) return false;

        try
        {
            using var db = new Database();
            // Delete + reinsert must be one transaction, not sequential calls — a failure
            // partway through the loop (e.g. a lock race with the tracker's poll thread)
            // used to leave the original entry already deleted with only some of its
            // replacements written, permanently losing the rest (round-5 audit finding #1).
            db.RunInTransaction(() =>
            {
                db.DeleteDiaryEntry(id);
                var t = startTime;
                var dateStr = date.ToIsoDate();
                foreach (var (dur, cat, desc, _) in rows)
                {
                    var mins = (int)dur.Value;
                    var segEnd = t.AddMinutes(mins);
                    var catValue = ((ComboBoxItem)cat.SelectedItem).Tag as string ?? category;
                    db.InsertDiaryEntry(dateStr, t.ToIsoTimeOfDay(),
                        segEnd.ToIsoTimeOfDay(), mins, catValue, window,
                        desc.Text.Trim() is { Length: > 0 } d ? d : null);
                    t = segEnd;
                }
            });
            // Splitting can change this day's category minutes (each row picks its own
            // category) — recompute so an already-scored day doesn't keep showing a stale
            // figure (2026-07-17 request). Best-effort: doesn't turn an otherwise-
            // successful split into a reported failure.
            try
            {
                using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
                score.RecalculateDayScore(date);
            }
            catch (Exception ex) { Log.Error("SplitDiaryEntryDialog.RecalculateScore", ex); }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("SplitDiaryEntryDialog", ex);
            return false;
        }
    }
}
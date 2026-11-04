using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// Edit (or delete) one time-diary row — the WinUI port of main.py's
/// _edit_diary_entry / _delete_diary_entry. Start/end are typed HH:MM,
/// duration auto-recalculates from them (matching the Python dialog),
/// category is one of the five tracker buckets.
/// </summary>
public static class EditDiaryEntryDialog
{
    /// <returns>null if the user cancelled (either dialog), true once
    /// saved/deleted, false if the save/delete itself failed (2026-07-14
    /// round-6 audit finding #6: callers used to treat false the same as a
    /// cancel).</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot, long id, DateOnly date,
        string start, string end, int durationMin, string category, string? description, string? tag)
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 320 };

        var startBox = new TextBox { Header = "Start (HH:MM)", Text = start };
        var endBox = new TextBox { Header = "End (HH:MM)", Text = end };
        var times = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        times.Children.Add(startBox);
        times.Children.Add(endBox);
        panel.Children.Add(times);

        var durBox = new NumberBox
        {
            Header = "Duration (min)",
            Value = durationMin,
            Minimum = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        panel.Children.Add(durBox);

        var catBox = new ComboBox { Header = "Category", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (label, value) in DiaryCategory.EditableOptions)
            catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        catBox.SelectedIndex = Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == category) is >= 0 and var i ? i : 0;
        panel.Children.Add(catBox);

        // A second, independent axis (2026-08-06) — unlike Category, "(none)" is a real,
        // common choice here (most entries have no tag), so it's first in the list rather
        // than defaulted away like Category's mandatory five.
        var tagBox = new ComboBox { Header = "Tag (optional)", HorizontalAlignment = HorizontalAlignment.Stretch };
        tagBox.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });
        foreach (var (label, value) in DiaryTag.Options)
            tagBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        tagBox.SelectedIndex = tag is null ? 0
            : Array.FindIndex(DiaryTag.Options, t => t.Value == tag) is >= 0 and var ti ? ti + 1 : 0;
        panel.Children.Add(tagBox);

        // AutoSuggestBox, not a plain TextBox — surfaces your own most commonly-used
        // descriptions (across every category, not just idle-answer text) as soon as you
        // focus an empty field, instead of you having to remember and retype them by hand
        // every time (2026-07-28 request). Falls back to a plain empty suggestion list if
        // the DB read fails, same as IdleReturnDialog's own MostFrequentIdleAnswers guard —
        // a missing convenience, not a broken dialog.
        List<string> frequent;
        try { using var db = new Database(); frequent = db.MostFrequentDescriptions(); }
        catch (Exception ex) { Log.Error("EditDiaryEntryDialog.MostFrequentDescriptions", ex); frequent = new(); }

        var descBox = new AutoSuggestBox
        {
            Header = "Description",
            Text = description ?? "",
        };
        DialogControls.WireFrequentSuggestions(descBox, frequent);
        panel.Children.Add(descBox);

        // Real one-tap chips alongside the dropdown, mirroring SplitDiaryEntryDialog's own fix for
        // the same documented gap (MostFrequentDescriptions' doc comment promises "quick-pick chips
        // on Edit/Split diary entry" — this dialog only ever got the dropdown half of that; see
        // SplitDiaryEntryDialog.cs for the fuller history). Single field here, so no need for
        // Split's "which row was last focused" tracking — a chip always fills this one box.
        if (frequent.Count > 0)
        {
            const int chipsPerRow = 4;
            var chipSection = new StackPanel { Spacing = 6 };
            for (var chipStart = 0; chipStart < frequent.Count; chipStart += chipsPerRow)
            {
                var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                foreach (var chip in frequent.Skip(chipStart).Take(chipsPerRow))
                {
                    var b = new Button { Content = chip, FontSize = 12, Padding = new Thickness(8, 3, 8, 3) };
                    b.Click += (_, _) => descBox.Text = chip;
                    chipRow.Children.Add(b);
                }
                chipSection.Children.Add(chipRow);
            }
            panel.Children.Add(chipSection);
        }

        var error = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(error);

        // Same auto-recalc as the Python dialog: valid HH:MM on both ends
        // overwrites the duration field.
        void Recalc()
        {
            if (DateExtensions.TryParseTimeOfDay(startBox.Text.Trim(), out var s) &&
                DateExtensions.TryParseTimeOfDay(endBox.Text.Trim(), out var e))
            {
                var diff = (e.ToTimeSpan() - s.ToTimeSpan()).TotalMinutes;
                if (diff > 0) durBox.Value = diff;
            }
        }
        startBox.TextChanged += (_, _) => Recalc();
        endBox.TextChanged += (_, _) => Recalc();

        var dialog = DialogControls.Build(xamlRoot, "Edit diary entry", panel,
            primaryButtonText: "Save", secondaryButtonText: "Delete", closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Primary,
            secondaryButtonStyle: (Style)Application.Current.Resources["DangerButtonStyle"]);

        dialog.PrimaryButtonClick += (sender, args) =>
        {
            if (!DateExtensions.TryParseTimeOfDay(startBox.Text.Trim(), out _) ||
                !DateExtensions.TryParseTimeOfDay(endBox.Text.Trim(), out _))
            {
                error.Text = "Start and end must be HH:MM (e.g. 08:00).";
                args.Cancel = true;
                return;
            }
            if (double.IsNaN(durBox.Value) || durBox.Value <= 0)
            {
                error.Text = "Duration must be greater than 0.";
                args.Cancel = true;
            }
        };

        var result = await DialogGate.ShowAsync(dialog);
        try
        {
            using var db = new Database();
            if (result == ContentDialogResult.Primary)
            {
                var cat = ((ComboBoxItem)catBox.SelectedItem).Tag as string ?? category;
                var chosenTag = ((ComboBoxItem)tagBox.SelectedItem).Tag as string;
                db.UpdateDiaryEntry(id, startBox.Text.Trim(), endBox.Text.Trim(),
                    (int)durBox.Value, cat, descBox.Text.Trim() is { Length: > 0 } d ? d : null, chosenTag);
                ScoreService.TryRecalculateDayScores(db, [date], "EditDiaryEntryDialog.RecalculateScore");
                return true;
            }
            if (result == ContentDialogResult.Secondary)
            {
                // Names exactly what's being removed and its side effect — matches the
                // level of detail every other permanent-delete confirmation in the app
                // already gives (2026-07-18 audit finding R8-08: this one used to be a
                // bare "Delete this diary entry?" with no Content at all).
                var label = Array.Find(DiaryCategory.EditableOptions, c => c.Value == category).Label ?? category;
                var confirm = DialogControls.Build(xamlRoot, "Delete this diary entry?",
                    $"The {label} entry from {start}–{end} on {date.ToDisplayDate()} will be " +
                    "permanently removed, and that day's score will be recalculated. This can't be undone.",
                    primaryButtonText: "Delete", closeButtonText: "Cancel");
                if (await DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return null;
                db.DeleteDiaryEntry(id);
                ScoreService.TryRecalculateDayScores(db, [date], "EditDiaryEntryDialog.RecalculateScore");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error("EditDiaryEntryDialog", ex);
            return false;
        }
        return null;
    }
}

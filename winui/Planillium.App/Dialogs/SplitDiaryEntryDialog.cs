using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

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
    /// <returns>null if the user cancelled, true once split, false if the split itself
    /// failed — matches EditDiaryEntryDialog's contract (2026-07-14 round-6 audit finding
    /// #6, applied here 2026-07-18 audit finding R8-06: this dialog was the one sibling
    /// still returning a plain bool, so a save failure was indistinguishable from
    /// Cancel and the caller could never show an error for it).</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot, long id, DateOnly date,
        string start, string end, int durationMin, string category, string window, string? description,
        string? tag)
    {
        if (!DateExtensions.TryParseTimeOfDay(start, out var startTime))
            return false;

        var root = new StackPanel { Spacing = 10, MinWidth = 460 };
        root.Children.Add(new TextBlock
        {
            Text = $"Split this {durationMin}-minute entry ({start} → {end}) into several activities.",
            TextWrapping = TextWrapping.Wrap,
        });

        // Horizontally scrollable, not just widened (2026-08-06 follow-up to adding the Tag
        // combo): ContentDialog's own template caps its width around the platform's
        // ContentDialogMaxWidth theme resource regardless of this content's MinWidth — a row
        // with duration+category+tag+description+remove genuinely doesn't fit inside that cap.
        // Past it, a Stretch-arranged Grid zero-arranges its trailing Auto columns instead of
        // visibly clipping (confirmed live: the Remove button's BoundingRectangle read Empty,
        // same ambiguous-looking symptom this project already root-caused once for the diary
        // row list — see ReportsPage.Diary.cs's DiaryList/BuildRow comments on HorizontalAlignment
        // .Left + a MinWidth for the Star column, and its diaryScroller sibling for the
        // horizontal-scroll half of that same fix). This is that fix applied here.
        var rowsPanel = new StackPanel { Spacing = 6 };
        var rowsScroller = new ScrollViewer
        {
            // Visible, not Auto — this project already learned that lesson once
            // (ReportsPage.Diary.cs's diaryScroller, 2026-07-28: "can't split the unaccounted
            // time" — Auto's hover-only indicator went unnoticed and read as broken/missing
            // content rather than scrollable). Applying it here before anyone has to hit the
            // same thing a second time in a different dialog.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rowsPanel,
        };
        root.Children.Add(rowsScroller);

        var addRowBtn = new Button { Content = "+ Add activity", Padding = new Thickness(0) };
        var remainingText = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(addRowBtn);
        toolbar.Children.Add(remainingText);
        root.Children.Add(toolbar);

        var dialog = DialogControls.Build(xamlRoot, "Split diary entry", root,
            primaryButtonText: "Split", closeButtonText: "Cancel", defaultButton: ContentDialogButton.Primary);

        // Surfaces your own most commonly-used descriptions as soon as you focus an empty
        // row's description field, instead of retyping the same handful by hand every time
        // a block gets split (2026-07-28 request) — same data EditDiaryEntryDialog now uses.
        // Best-effort: a DB read failure just means no suggestions, not a broken dialog.
        List<string> frequent;
        try { using var db = new Database(); frequent = db.MostFrequentDescriptions(); }
        catch (Exception ex) { Log.Error("SplitDiaryEntryDialog.MostFrequentDescriptions", ex); frequent = new(); }

        var rows = new List<(NumberBox Dur, ComboBox Cat, ComboBox Tag, AutoSuggestBox Desc, Button Remove)>();

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

        void AddRow(int? prefillMin, string prefillCat, string? prefillDesc, string? prefillTag)
        {
            var durBox = DialogControls.MinutesBox(prefillMin);
            var catBox = new ComboBox { Width = 110 };
            foreach (var (label, value) in DiaryCategory.EditableOptions)
                catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            catBox.SelectedIndex = Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == prefillCat) is >= 0 and var ci ? ci : 0;
            AutomationProperties.SetName(catBox, "Category");
            // Independent axis (2026-08-06) — inherits the original entry's tag on every new
            // row by default, same as category does, since that's the least surprising
            // starting point; each row can still change it.
            var tagBox = new ComboBox { Width = 130 };
            tagBox.Items.Add(new ComboBoxItem { Content = "(no tag)", Tag = null });
            foreach (var (label, value) in DiaryTag.Options)
                tagBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            tagBox.SelectedIndex = prefillTag is null ? 0
                : Array.FindIndex(DiaryTag.Options, t => t.Value == prefillTag) is >= 0 and var ti ? ti + 1 : 0;
            AutomationProperties.SetName(tagBox, "Tag (optional)");
            // Fixed width, not Star (2026-08-06 follow-up) — inside rowsScroller's horizontal
            // ScrollViewer this Grid is offered effectively unconstrained width, and a Star
            // column has nothing finite to divide against there, so it measures to ~0 instead
            // of the description actually being usable (same trap ReportsPage.Diary.cs's own
            // row comments already documented: "a Grid measured with infinite width can
            // collapse its Star column").
            var descBox = new AutoSuggestBox
            {
                PlaceholderText = "description",
                Text = prefillDesc ?? "",
                Width = 220,
            };
            DialogControls.WireFrequentSuggestions(descBox, frequent);
            AutomationProperties.SetName(descBox, "Activity description");
            var removeBtn = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4) };
            AutomationProperties.SetName(removeBtn, "Remove this activity");

            // Left, not the default Stretch — same reasoning as DiaryList/BuildRow's row Grid:
            // sizes itself to its true natural (Measure-time) width and is never compressed by
            // Arrange, which is what lets rowsScroller actually reach the Remove button instead
            // of zero-arranging it away.
            var row = new Grid { ColumnSpacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(durBox, 0); Grid.SetColumn(catBox, 1); Grid.SetColumn(tagBox, 2);
            Grid.SetColumn(descBox, 3); Grid.SetColumn(removeBtn, 4);
            row.Children.Add(durBox);
            row.Children.Add(catBox);
            row.Children.Add(tagBox);
            row.Children.Add(descBox);
            row.Children.Add(removeBtn);
            rowsPanel.Children.Add(row);

            var entry = (durBox, catBox, tagBox, descBox, removeBtn);
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
        AddRow(durationMin, category, description, tag);
        AddRow(null, category, null, tag);

        // Was never wired at all — the button existed and looked clickable, but had no
        // Click handler, so nothing happened no matter how many times it was pressed
        // (2026-07-29 user report: "not possible to add more than 2 lines"). Same
        // defaults as the second seed row above: empty duration for the user to fill in,
        // the original entry's category/tag, no prefilled description.
        addRowBtn.Click += (_, _) => AddRow(null, category, null, tag);

        var result = await DialogGate.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary) return null;

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
                foreach (var (dur, cat, rowTag, desc, _) in rows)
                {
                    var mins = (int)dur.Value;
                    var segEnd = t.AddMinutes(mins);
                    var catValue = ((ComboBoxItem)cat.SelectedItem).Tag as string ?? category;
                    var tagValue = ((ComboBoxItem)rowTag.SelectedItem).Tag as string;
                    db.InsertDiaryEntry(dateStr, t.ToIsoTimeOfDay(),
                        segEnd.ToIsoTimeOfDay(), mins, catValue, window,
                        desc.Text.Trim() is { Length: > 0 } d ? d : null, tagValue);
                    t = segEnd;
                }
            });
            // Splitting can change this day's category minutes (each row picks its own
            // category) — recompute so an already-scored day doesn't keep showing a stale
            // figure (2026-07-17 request). Best-effort: doesn't turn an otherwise-
            // successful split into a reported failure.
            ScoreService.TryRecalculateDayScores(db, [date], "SplitDiaryEntryDialog.RecalculateScore");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("SplitDiaryEntryDialog", ex);
            return false;
        }
    }
}
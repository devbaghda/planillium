using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Edit (or delete) one time-diary row — the WinUI port of main.py's
/// _edit_diary_entry / _delete_diary_entry. Start/end are typed HH:MM,
/// duration auto-recalculates from them (matching the Python dialog),
/// category is one of the five tracker buckets.
/// </summary>
public static class EditDiaryEntryDialog
{
    private static readonly (string Label, string Value)[] Categories =
    {
        ("On-plan", DiaryCategory.OnPlan), ("Off-plan", DiaryCategory.OffPlan), ("Paid", DiaryCategory.Paid),
        ("Neutral", DiaryCategory.Neutral), ("Idle", DiaryCategory.Idle),
    };

    /// <returns>null if the user cancelled (either dialog), true once
    /// saved/deleted, false if the save/delete itself failed (2026-07-14
    /// round-6 audit finding #6: callers used to treat false the same as a
    /// cancel).</returns>
    public static async Task<bool?> ShowAsync(XamlRoot xamlRoot, long id, DateOnly date,
        string start, string end, int durationMin, string category, string? description)
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
            Header = "Duration (min)", Value = durationMin, Minimum = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        panel.Children.Add(durBox);

        var catBox = new ComboBox { Header = "Category", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (label, value) in Categories)
            catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        catBox.SelectedIndex = Array.FindIndex(Categories, c => c.Value == category) is >= 0 and var i ? i : 0;
        panel.Children.Add(catBox);

        var descBox = new TextBox { Header = "Description", Text = description ?? "" };
        panel.Children.Add(descBox);

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
            if (TimeOnly.TryParseExact(startBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var s) &&
                TimeOnly.TryParseExact(endBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var e))
            {
                var diff = (e.ToTimeSpan() - s.ToTimeSpan()).TotalMinutes;
                if (diff > 0) durBox.Value = diff;
            }
        }
        startBox.TextChanged += (_, _) => Recalc();
        endBox.TextChanged += (_, _) => Recalc();

        var dialog = new ContentDialog
        {
            Title = "Edit diary entry",
            Content = panel,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        dialog.PrimaryButtonClick += (sender, args) =>
        {
            if (!TimeOnly.TryParseExact(startBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _) ||
                !TimeOnly.TryParseExact(endBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
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
                db.UpdateDiaryEntry(id, startBox.Text.Trim(), endBox.Text.Trim(),
                    (int)durBox.Value, cat, descBox.Text.Trim() is { Length: > 0 } d ? d : null);
                RecalculateScore(db, date);
                return true;
            }
            if (result == ContentDialogResult.Secondary)
            {
                var confirm = new ContentDialog
                {
                    Title = "Delete this diary entry?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xamlRoot,
                };
                if (await DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return null;
                db.DeleteDiaryEntry(id);
                RecalculateScore(db, date);
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

    /// <summary>Editing or deleting an entry changes that day's on/off-plan minutes, but
    /// a day already scored (score_ledger's daily_score is once-per-date, not
    /// auto-refreshing) would otherwise keep showing the stale figure forever — recompute
    /// it right away instead of waiting for a change that never comes (2026-07-17
    /// request, prompted by editing a diary entry's category). Best-effort: a failure
    /// here shouldn't turn an otherwise-successful save/delete into a reported failure,
    /// so it's logged rather than thrown.</summary>
    private static void RecalculateScore(Database db, DateOnly date)
    {
        try
        {
            using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
            score.RecalculateDayScore(date);
        }
        catch (Exception ex)
        {
            Log.Error("EditDiaryEntryDialog.RecalculateScore", ex);
        }
    }
}

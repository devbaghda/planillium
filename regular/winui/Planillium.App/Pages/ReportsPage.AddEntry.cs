using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Pages;

/// <summary>G9c: add a Diary row by hand (any time of day, including after hours). Goes through
/// <see cref="DataChangeDoor.AddEntry"/>: validation, any overlap refused, backup, change history, Undo.</summary>
public sealed partial class ReportsPage
{
    private async Task AddDiaryEntryAsync()
    {
        if (_diaryBulkBusy) return;
        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        var dateBox = new TextBox { Header = "Date (YYYY-MM-DD)", Text = _diaryDate.ToIsoDate() };
        AutomationProperties.SetAutomationId(dateBox, "AddEntryDate");
        var startBox = new TextBox { Header = "Start (HH:MM)", PlaceholderText = "21:00" };
        AutomationProperties.SetAutomationId(startBox, "AddEntryStart");
        var endBox = new TextBox { Header = "End (HH:MM)", PlaceholderText = "22:30" };
        AutomationProperties.SetAutomationId(endBox, "AddEntryEnd");
        var times = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        times.Children.Add(startBox);
        times.Children.Add(endBox);
        panel.Children.Add(dateBox);
        panel.Children.Add(times);

        var catBox = new ComboBox { Header = "Category", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (label, value) in DiaryCategory.EditableOptions)
            catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        catBox.SelectedIndex = Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == DiaryCategory.OnPlan) is >= 0 and var ci ? ci : 0;
        AutomationProperties.SetAutomationId(catBox, "AddEntryCategory");
        panel.Children.Add(catBox);

        var tagBox = new ComboBox { Header = "Tag (optional)", HorizontalAlignment = HorizontalAlignment.Stretch };
        tagBox.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });
        foreach (var (label, value) in DiaryTag.Options)
            tagBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        tagBox.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(tagBox, "AddEntryTag");
        panel.Children.Add(tagBox);

        var noteBox = new TextBox { Header = "Note (optional)" };
        AutomationProperties.SetAutomationId(noteBox, "AddEntryNote");
        panel.Children.Add(noteBox);

        var error = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(error, "AddEntryError");
        panel.Children.Add(error);

        var dialog = Dialogs.DialogControls.Build(XamlRoot, "Add diary entry", panel,
            primaryButtonText: "Add", closeButtonText: "Cancel", defaultButton: ContentDialogButton.Primary);

        DateOnly date = default;
        TimeOnly start = default, end = default;
        string? category = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!dateBox.Text.Trim().TryParseIsoDate(out date))
            {
                error.Text = "The date must look like 2026-11-04.";
                args.Cancel = true;
                return;
            }
            if (!DateExtensions.TryParseTimeOfDay(startBox.Text.Trim(), out start) ||
                !DateExtensions.TryParseTimeOfDay(endBox.Text.Trim(), out end))
            {
                error.Text = "Start and end must be HH:MM (e.g. 21:00).";
                args.Cancel = true;
                return;
            }
            category = (catBox.SelectedItem as ComboBoxItem)?.Tag as string;
            var bad = DataChangeDoor.ValidateEntry(date, start, end, category, DateTime.Now);
            if (bad == null)
            {
                try
                {
                    using var db = new Database();
                    var clash = DataChangeDoor.FindOverlap(db, date.ToIsoDate(), start.ToIsoTimeOfDay(), end.ToIsoTimeOfDay());
                    if (clash != null) bad = $"That overlaps an existing entry: {clash}. Nothing was saved.";
                }
                catch (Exception ex) { Log.Error("ReportsPage.AddEntry.Overlap", ex); }
            }
            if (bad != null)
            {
                error.Text = bad;
                args.Cancel = true;
            }
        };

        if (await Dialogs.DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return;

        var tag = (tagBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var note = noteBox.Text;
        _diaryBulkBusy = true;
        try
        {
            var result = await Task.Run(() => DataChangeDoor.AddEntry(date, start, end, category!, tag, note));
            (App.MainWindow as MainWindow)?.RefreshScore();
            Render();
            ShowDiaryNotice(new(InfoBarSeverity.Success,
                $"Added an entry: {date.ToIsoDate()} {start.ToIsoTimeOfDay()}–{end.ToIsoTimeOfDay()}",
                "A backup was saved first.", result.ChangeId));
        }
        catch (InvalidOperationException ex)
        {
            ShowDiaryNotice(new(InfoBarSeverity.Warning, "Couldn't add that entry", ex.Message, null));
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.AddEntry", ex);
            ShowDiaryNotice(new(InfoBarSeverity.Error, "Couldn't add that entry",
                Log.Friendly("Nothing was changed", ex), null));
        }
        finally { _diaryBulkBusy = false; }
    }
}

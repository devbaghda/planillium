using System.Globalization;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// G3 (2026-10-28): Settings ▸ Change history — the kept bulk changes (DataChangeDoor), each with
// its own Undo.
public sealed partial class SettingsPage
{
    private bool _historyBusy;

    /// <summary>Re-reads the history and rebuilds the list. Called whenever the section is shown
    /// and after each Undo, so it never shows a stale "Undo" for a change already undone elsewhere
    /// (e.g. from the Diary's message bar).</summary>
    private void LoadChangeHistory()
    {
        HistoryList.Children.Clear();
        List<DataChangeDoor.Change> changes;
        try
        {
            changes = DataChangeDoor.History();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ChangeHistory.Load", ex);
            HistoryList.Children.Add(new TextBlock
            {
                Text = Log.Friendly("Couldn't read the change history", ex),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        if (changes.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = "No bulk changes in the last 30 days.",
                Foreground = Ui.Brush("TextFillColorSecondaryBrush"),
            });
            return;
        }
        foreach (var c in changes) HistoryList.Children.Add(HistoryRow(c));
    }

    private Border HistoryRow(DataChangeDoor.Change c)
    {
        var rows = $"{c.RowCount.ToString("N0", CultureInfo.CurrentCulture)} {(c.RowCount == 1 ? "row" : "rows")}";
        var what = c.Field switch
        {
            DataChangeDoor.Field.DiaryCategory => "diary category",
            DataChangeDoor.Field.LearnedRules => "learned rule",
            DataChangeDoor.Field.DiaryAdd => "added entry",
            DataChangeDoor.Field.DiaryRules => "rules applied to history",
            _ => "diary tag",
        };
        var title = new TextBlock
        {
            Text = c.RowCount == 0 ? c.Label : $"{c.Label} · {rows}",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var detail = new TextBlock
        {
            Text = $"{c.At.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)} · {what}" +
                   (c.UndoneAt is DateTime u
                       ? $" · undone {u.ToString("d MMM, HH:mm", CultureInfo.InvariantCulture)}: restored " +
                         $"{c.Restored.ToString("N0", CultureInfo.CurrentCulture)}" +
                         (c.Skipped > 0 ? $", skipped {c.Skipped.ToString("N0", CultureInfo.CurrentCulture)} changed since" : "")
                       : ""),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.Brush("TextFillColorSecondaryBrush"),
        };
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(title);
        text.Children.Add(detail);

        var undo = new Button
        {
            Content = c.UndoneAt is null ? "Undo" : "Undone",
            IsEnabled = c.UndoneAt is null,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Ui.Name(undo, $"Undo: {c.Label}, {rows}, {c.At.ToString("d MMM HH:mm", CultureInfo.InvariantCulture)}");
        undo.Click += async (_, _) => await UndoChangeAsync(c, undo);

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(undo, 1);
        grid.Children.Add(text);
        grid.Children.Add(undo);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = Ui.Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Ui.Brush("CardStrokeColorDefaultBrush"),
        };
    }

    private async Task UndoChangeAsync(DataChangeDoor.Change c, Button trigger)
    {
        if (_historyBusy) return;
        var rows = $"{c.RowCount.ToString("N0", CultureInfo.CurrentCulture)} {(c.RowCount == 1 ? "row" : "rows")}";
        var keywords = c.KeywordsTaught == 0 ? "" :
            $" The {c.KeywordsTaught.ToString("N0", CultureInfo.CurrentCulture)} " +
            $"{(c.KeywordsTaught == 1 ? "activity keyword" : "activity keywords")} it taught " +
            "are forgotten too, unless you changed them since.";
        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Undo this change?",
            $"\"{c.Label}\" ({rows}, {c.At.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)}) — " +
            "each row goes back to the value it had before. Rows changed since are skipped and keep " +
            "their newer values." + keywords + " A backup is saved first.",
            primaryButtonText: "Undo", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        _historyBusy = true;
        trigger.IsEnabled = false;
        SaveStatus.Text = "Undoing…";
        try
        {
            var result = await Task.Run(() => DataChangeDoor.Undo(c.Id));
            var status = DataChangeDoor.DescribeUndo(result);
            if (result.KeywordsForgotten.Count > 0)
            {
                RefreshSummaries();
                MainWindow.Instance?.RestartTracker();
            }
            if (result.RulesRemoved + result.RulesRestored > 0) MainWindow.Instance?.ReloadTrackerRules();
            MainWindow.Instance?.RefreshScore();
            SaveStatus.Text = status;
        }
        catch (InvalidOperationException ex)
        {
            SaveStatus.Text = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ChangeHistory.Undo", ex);
            SaveStatus.Text = Log.Friendly("Couldn't undo that change", ex);
        }
        finally
        {
            _historyBusy = false;
            LoadChangeHistory();
        }
    }
}

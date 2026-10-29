using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// G3 (2026-10-28): the Diary's bulk Mark — confirm with filled / overwritten / unchanged counts,
// then the write through DataChangeDoor (backup, old values kept), then a "Marked N rows as X"
// message with Undo. The single-row Edit dialog is untouched. See ReportsPage.xaml.cs for the
// file split.
public sealed partial class ReportsPage
{
    // The ticked diary rows. Static like the filters, and no longer cleared by Mark, so ticks stay
    // until the user changes them — leaving and reopening the Diary keeps them (G3). The list
    // still drops any tick whose row stops matching the filters (RenderDiaryResults).
    private static readonly HashSet<long> _diarySelectedIds = new();

    // True while a bulk mark or undo is running — the mark buttons stay disabled meanwhile, so a
    // second click can't start a second write over the first.
    private bool _diaryBulkBusy;

    // The current build's RenderDiaryResults, for an Undo that finishes after the page re-rendered.
    private Action? _diaryRerenderAction;

    private sealed record DiaryNotice(InfoBarSeverity Severity, string Title, string Message, long? UndoChangeId);
    private DiaryNotice? _diaryNotice;
    private InfoBar? _diaryNoticeBar;

    private static string FormatCount(int n) => n.ToString("N0", CultureInfo.CurrentCulture);

    private static string RowsWord(int n) => n == 1 ? "row" : "rows";

    private static string MarkValueLabel(DataChangeDoor.Field field, string? value) =>
        field == DataChangeDoor.Field.DiaryCategory
            ? (Array.Find(DiaryCategory.EditableOptions, o => o.Value == value).Label ?? value ?? "").ToLowerInvariant()
            : DiaryTag.LabelOf(value) ?? value ?? "no tag";

    /// <summary>The message bar above the mark toolbar. Rebuilt with the section on every Render;
    /// its content comes from <see cref="_diaryNotice"/>, so a message (and its Undo) survives a
    /// re-render until the user closes it or a newer change replaces it.</summary>
    private InfoBar BuildDiaryChangeNotice()
    {
        var bar = new InfoBar { IsClosable = true, IsOpen = false, Margin = new Thickness(0, 0, 0, 8) };
        AutomationProperties.SetName(bar, "Bulk change message");
        bar.CloseButtonClick += (_, _) => _diaryNotice = null;
        _diaryNoticeBar = bar;
        ApplyDiaryNotice();
        return bar;
    }

    private void ShowDiaryNotice(DiaryNotice notice)
    {
        _diaryNotice = notice;
        ApplyDiaryNotice();
    }

    private void ApplyDiaryNotice()
    {
        if (_diaryNoticeBar is not { } bar) return;
        if (_diaryNotice is not { } n) { bar.IsOpen = false; return; }
        bar.Severity = n.Severity;
        bar.Title = n.Title;
        bar.Message = n.Message;
        if (n.UndoChangeId is long id)
        {
            var undo = new Button { Content = "Undo" };
            AutomationProperties.SetName(undo, "Undo this change");
            undo.Click += async (_, _) =>
            {
                undo.IsEnabled = false;
                await UndoDiaryChangeAsync(id);
            };
            bar.ActionButton = undo;
        }
        else bar.ActionButton = null;
        bar.IsOpen = true;
    }

    /// <summary>
    /// One bulk mark: preview → confirm (filled / overwritten / unchanged, "Mark N rows" /
    /// Cancel) → DataChangeDoor.Apply (backup, write, old values kept) → message with Undo.
    /// Cancel writes nothing. <paramref name="rows"/> is every row the mark targets — the ticked
    /// ones, or all matches of the filters when none are ticked. <paramref name="teach"/>: whether
    /// a category mark also teaches each row's app/page keyword — only when the user ticked the
    /// rows; marking all matches of a filter teaches nothing (G3 round 2).
    /// </summary>
    private async Task BulkMarkDiaryAsync(DataChangeDoor.Field field, string? value,
        List<ReportData.DiaryEntry> rows, bool teach, Action updateToolbar, Action refresh, Func<HashSet<long>> shownIds)
    {
        if (rows.Count == 0 || _diaryBulkBusy) return;
        var ids = rows.Select(r => r.Id).ToList();
        var valueLabel = MarkValueLabel(field, value);
        var isCategory = field == DataChangeDoor.Field.DiaryCategory;
        var clearing = !isCategory && value is null;

        _diaryBulkBusy = true;
        updateToolbar();
        try
        {
            DataChangeDoor.Preview preview;
            try
            {
                preview = await Task.Run(() => DataChangeDoor.PreviewChange(ids, field, value));
            }
            catch (Exception ex)
            {
                Log.Error("ReportsPage.BulkMark.Preview", ex);
                ShowDiaryNotice(new(InfoBarSeverity.Error, "Couldn't read those rows",
                    Log.Friendly("Nothing was changed", ex), null));
                return;
            }

            if (preview.Changed == 0)
            {
                var none = Dialogs.DialogControls.Build(XamlRoot, "Nothing to change",
                    clearing
                        ? $"None of these {FormatCount(rows.Count)} {RowsWord(rows.Count)} has a tag."
                        : $"All {FormatCount(rows.Count)} {RowsWord(rows.Count)} are already {valueLabel}.",
                    closeButtonText: "OK");
                await Dialogs.DialogGate.ShowAsync(none);
                return;
            }

            var title = clearing
                ? $"Clear the tag on {FormatCount(preview.Changed)} {RowsWord(preview.Changed)}?"
                : $"Mark {FormatCount(preview.Changed)} {RowsWord(preview.Changed)} as {valueLabel}?";
            var confirm = Dialogs.DialogControls.Build(XamlRoot, title,
                BuildMarkPreviewContent(preview, isCategory, clearing, valueLabel),
                primaryButtonText: $"Mark {FormatCount(preview.Changed)} {RowsWord(preview.Changed)}",
                closeButtonText: "Cancel");
            if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

            var changeLabel = clearing ? "Cleared the tag" : $"Marked as {valueLabel}";
            DataChangeDoor.ApplyResult result;
            try
            {
                result = await Task.Run(() => DataChangeDoor.Apply(changeLabel, ids, field, value));
            }
            catch (Exception ex)
            {
                Log.Error("ReportsPage.BulkMark.Apply", ex);
                ShowDiaryNotice(new(InfoBarSeverity.Error, "Couldn't save that change",
                    Log.Friendly("Nothing was changed", ex), null));
                return;
            }

            // A category mark of TICKED rows also teaches the tracker each row's app/page keyword
            // (Settings ▸ Activity keywords), recorded with the change so Undo forgets exactly
            // the ones it newly taught (G3 round 2). Marking all filter matches teaches nothing;
            // tags never teach.
            var learned = new List<string>();
            if (isCategory)
            {
                if (teach)
                {
                    var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var row in rows)
                    {
                        var keyword = AppNames.Sub(row.Window) ?? AppNames.Group(row.Window);
                        if (keyword is { Length: > 0 } && keyword != "—") keywords.Add(keyword);
                    }
                    try
                    {
                        learned = await Task.Run(() => DataChangeDoor.TeachKeywords(result.ChangeId, value!, keywords));
                    }
                    catch (Exception ex) { Log.Error("ReportsPage.BulkMark.Learn", ex); }
                    if (learned.Count > 0) (App.MainWindow as MainWindow)?.RestartTracker();
                }
                (App.MainWindow as MainWindow)?.RefreshScore();
            }

            refresh();
            var shown = shownIds();
            var leftView = ids.Count(id => !shown.Contains(id));

            var parts = new List<string> { "A backup was saved first." };
            if (leftView > 0)
                parts.Add($"{FormatCount(leftView)} {RowsWord(leftView)} no longer match your filters, so they left the list.");
            if (learned.Count > 0)
                parts.Add($"Keywords learned for {valueLabel}: {string.Join(", ", learned.Take(8))}" +
                          (learned.Count > 8 ? $" and {learned.Count - 8} more" : "") +
                          " (Undo forgets these too).");
            ShowDiaryNotice(new(InfoBarSeverity.Success,
                clearing
                    ? $"Cleared the tag on {FormatCount(result.Changed)} {RowsWord(result.Changed)}"
                    : $"Marked {FormatCount(result.Changed)} {RowsWord(result.Changed)} as {valueLabel}",
                string.Join(" ", parts), result.ChangeId));
        }
        finally
        {
            _diaryBulkBusy = false;
            updateToolbar();
        }
    }

    /// <summary>The confirm dialog's body: the three counts, the total that will change, and
    /// where Undo lives.</summary>
    private static StackPanel BuildMarkPreviewContent(DataChangeDoor.Preview p, bool isCategory, bool clearing,
        string valueLabel)
    {
        var lines = clearing
            ? new List<(string, int)>
            {
                ("Have a tag — will be cleared", p.Overwritten),
                ("Have no tag — unchanged", p.Unchanged),
            }
            : new List<(string, int)>
            {
                (isCategory ? "No category — will be filled" : "No tag — will be filled", p.Filled),
                (isCategory ? "Another category — will be overwritten" : "Another tag — will be overwritten", p.Overwritten),
                ($"Already {valueLabel} — unchanged", p.Unchanged),
            };
        lines.Add(("Total changed", p.Changed));

        var grid = new Grid { ColumnSpacing = 24, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i < lines.Count; i++)
        {
            var (label, count) = lines[i];
            var total = i == lines.Count - 1;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new TextBlock { Text = label, FontWeight = total ? FontWeights.SemiBold : FontWeights.Normal };
            var value = new TextBlock
            {
                Text = FormatCount(count),
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = total ? FontWeights.SemiBold : FontWeights.Normal,
            };
            AutomationProperties.SetName(value, $"{label}: {count}");
            Grid.SetRow(name, i); Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(name);
            grid.Children.Add(value);
        }

        var panel = new StackPanel { Spacing = 12, MinWidth = 340 };
        panel.Children.Add(grid);
        if (p.Missing > 0)
            panel.Children.Add(new TextBlock
            {
                Text = $"{FormatCount(p.Missing)} {RowsWord(p.Missing)} no longer exist and will be skipped.",
                TextWrapping = TextWrapping.Wrap,
            });
        panel.Children.Add(new TextBlock
        {
            Text = "A backup of your data is saved first. You can undo this from the message that " +
                   "follows, or later from Settings ▸ Change history.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        return panel;
    }

    /// <summary>Undo from the message bar — same DataChangeDoor.Undo the Settings history uses.</summary>
    private async Task UndoDiaryChangeAsync(long changeId)
    {
        if (_diaryBulkBusy) return;
        _diaryBulkBusy = true;
        try
        {
            var result = await Task.Run(() => DataChangeDoor.Undo(changeId));
            if (result.KeywordsForgotten.Count > 0) (App.MainWindow as MainWindow)?.RestartTracker();
            (App.MainWindow as MainWindow)?.RefreshScore();
            ShowDiaryNotice(new(result.Skipped > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success,
                "Change undone", DataChangeDoor.DescribeUndo(result), null));
        }
        catch (InvalidOperationException ex)
        {
            ShowDiaryNotice(new(InfoBarSeverity.Warning, "Couldn't undo", ex.Message, null));
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.BulkMark.Undo", ex);
            ShowDiaryNotice(new(InfoBarSeverity.Error, "Couldn't undo",
                Log.Friendly("Nothing was changed", ex), null));
        }
        finally
        {
            _diaryBulkBusy = false;
            _diaryRerenderAction?.Invoke();
        }
    }
}

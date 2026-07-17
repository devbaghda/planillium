using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Views;

/// <summary>
/// Inline, click-to-edit personal note per task — the user's own scratchpad,
/// separate from the plan JSON's detail/mentor_note (Claude-authored, read-
/// only). Shared between Today and Schedule so both pages get the identical
/// widget and storage (Database.LoadTaskNotes/SetTaskNote, keyed on
/// plan_id + task_text) instead of two divergent copies.
/// </summary>
public static class TaskNoteView
{
    /// <summary>Convenience overload that also owns the save-to-database
    /// closure (open a Database, call SetTaskNote, log on failure) — this
    /// exact block used to be copy-pasted, unchanged apart from the log
    /// tag, into both TodayPage and SchedulePage (2026-07-09 audit finding
    /// #6). Prefer this over the delegate overload unless a caller needs
    /// genuinely different save behavior. <paramref name="onError"/> lets
    /// the caller light up its own SaveErrorBar the same way a failed task
    /// completion already does.</summary>
    public static FrameworkElement Build(string? initialNote, string planId, string taskText, string logTag,
        Action? onError = null) =>
        Build(initialNote, text =>
        {
            try
            {
                using var db = new Database();
                db.SetTaskNote(planId, taskText, text);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(logTag, ex);
                onError?.Invoke();
                return false;
            }
        }, taskText);

    /// <param name="onSave">Returns whether the save actually succeeded — the
    /// displayed note only updates to the new text once this returns true
    /// (2026-07-14 round-6 audit finding #2: this used to update the
    /// on-screen text/visibility *before* the write was even attempted, so a
    /// failed save still read as "saved" until the note silently reverted
    /// the next time this task re-rendered). On failure, edit mode stays
    /// open with what was typed so nothing already-entered is lost.</param>
    /// <param name="taskLabel">Task text this note belongs to, for the edit box's
    /// accessible name — a screen reader used to announce every task's note field
    /// identically, with no way to tell which task it belonged to (audit finding #9).</param>
    public static FrameworkElement Build(string? initialNote, Func<string, bool> onSave, string? taskLabel = null)
    {
        var root = new StackPanel { Spacing = 4 };

        var display = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var noteText = new TextBlock
        {
            Text = initialNote ?? "",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Visibility = string.IsNullOrEmpty(initialNote) ? Visibility.Collapsed : Visibility.Visible,
        };
        var editLink = new HyperlinkButton
        {
            Content = string.IsNullOrEmpty(initialNote) ? "+ Add note" : "Edit note",
            FontSize = 12,
            Padding = new Thickness(0),
        };
        display.Children.Add(noteText);
        display.Children.Add(editLink);
        root.Children.Add(display);

        var editBox = new TextBox
        {
            Text = initialNote ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60,
            MaxWidth = 420,
            FontSize = 12,
            PlaceholderText = "Your own note for this task…",
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetName(editBox,
            taskLabel is { Length: > 0 } ? $"Note for: {taskLabel}" : "Task note");
        var editRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Visibility = Visibility.Collapsed,
        };
        var save = new Button
        {
            Content = "Save", FontSize = 12, Padding = new Thickness(10, 4, 10, 4),
        };
        var cancel = new HyperlinkButton { Content = "Cancel", FontSize = 12 };
        editRow.Children.Add(save);
        editRow.Children.Add(cancel);
        root.Children.Add(editBox);
        root.Children.Add(editRow);

        void EnterEdit()
        {
            editBox.Text = noteText.Text;
            display.Visibility = Visibility.Collapsed;
            editBox.Visibility = Visibility.Visible;
            editRow.Visibility = Visibility.Visible;
            editBox.Focus(FocusState.Programmatic);
        }

        void ExitEdit()
        {
            editBox.Visibility = Visibility.Collapsed;
            editRow.Visibility = Visibility.Collapsed;
            display.Visibility = Visibility.Visible;
        }

        editLink.Click += (_, _) => EnterEdit();
        cancel.Click += (_, _) => ExitEdit();
        save.Click += (_, _) =>
        {
            var text = editBox.Text.Trim();
            if (!onSave(text)) return;
            noteText.Text = text;
            noteText.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            editLink.Content = text.Length == 0 ? "+ Add note" : "Edit note";
            ExitEdit();
        };

        return root;
    }
}

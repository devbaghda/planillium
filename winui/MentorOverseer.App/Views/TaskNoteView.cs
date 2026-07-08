using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
    public static FrameworkElement Build(string? initialNote, Action<string> onSave)
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
            noteText.Text = text;
            noteText.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            editLink.Content = text.Length == 0 ? "+ Add note" : "Edit note";
            onSave(text);
            ExitEdit();
        };

        return root;
    }
}

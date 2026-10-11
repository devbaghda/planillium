using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Planillium.App.Pages;

/// <summary>A drop-down of check boxes for the diary filters (2026-10-01 request: filters
/// should be multi-selectable). Holds no state of its own: it reads and writes the page's
/// persisted <see cref="Selected"/> set, where an empty set means "no filter". The button
/// text always says what is active, so a collapsed filter never hides its own state.</summary>
internal sealed class MultiPicker
{
    private readonly string _title;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly Dictionary<string, string> _labels = new();
    private bool _syncing;

    public DropDownButton Button { get; }
    public HashSet<string> Selected { get; }
    /// <summary>Raised after the user ticks or unticks a box (never during SetOptions).</summary>
    public Action? Changed { get; set; }

    public MultiPicker(string title, HashSet<string> selected, double minWidth, string automationName)
    {
        _title = title;
        Selected = selected;
        Button = new DropDownButton
        {
            Content = title,
            MinWidth = minWidth,
            Flyout = new Flyout
            {
                Content = new ScrollViewer { MaxHeight = 320, Content = _list },
            },
        };
        AutomationProperties.SetName(Button, automationName);
    }

    /// <summary>Rebuilds the check boxes from <paramref name="options"/> (value, label) and
    /// re-ticks whatever is in <see cref="Selected"/>.</summary>
    public void SetOptions(IEnumerable<(string Value, string Label)> options)
    {
        _syncing = true;
        _list.Children.Clear();
        _labels.Clear();
        foreach (var (value, label) in options)
        {
            _labels[value] = label;
            var box = new CheckBox { Content = label, IsChecked = Selected.Contains(value), Tag = value };
            box.Checked += (_, _) => Toggle(value, true);
            box.Unchecked += (_, _) => Toggle(value, false);
            _list.Children.Add(box);
        }
        _syncing = false;
        UpdateTitle();
    }

    private void Toggle(string value, bool on)
    {
        if (_syncing) return;
        if (on) Selected.Add(value); else Selected.Remove(value);
        UpdateTitle();
        Changed?.Invoke();
    }

    private void UpdateTitle()
    {
        Button.Content = Selected.Count switch
        {
            0 => _title,
            1 => $"{_title}: {(_labels.TryGetValue(Selected.First(), out var l) ? l : Selected.First())}",
            _ => $"{_title}: {Selected.Count} selected",
        };
    }
}

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
    // G5 (2026-10-27): the options are kept as plain data and the check boxes are only built
    // while the drop-down is open, then dropped when it closes. The Page list in "All time"
    // holds one entry per distinct page in the whole diary (thousands), and every diary
    // refresh rebuilt all of them as hidden check boxes — the largest share of the memory
    // Reports held and kept churning (measured, see regular/CHANGELOG.md G5).
    private List<(string Value, string Label)> _options = new();
    private bool _open;
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
        Button.Flyout.Opening += (_, _) => { _open = true; BuildBoxes(); };
        Button.Flyout.Closed += (_, _) => { _open = false; _list.Children.Clear(); };
    }

    /// <summary>Rebuilds the check boxes from <paramref name="options"/> (value, label) and
    /// re-ticks whatever is in <see cref="Selected"/>.</summary>
    public void SetOptions(IEnumerable<(string Value, string Label)> options)
    {
        _options = options.ToList();
        _labels.Clear();
        foreach (var (value, label) in _options) _labels[value] = label;
        // Open drop-down (a tick re-ran the diary): rebuild in place, as before.
        if (_open) BuildBoxes();
        UpdateTitle();
    }

    private void BuildBoxes()
    {
        _syncing = true;
        _list.Children.Clear();
        foreach (var (value, label) in _options)
        {
            var box = new CheckBox { Content = label, IsChecked = Selected.Contains(value), Tag = value };
            box.Checked += (_, _) => Toggle(value, true);
            box.Unchecked += (_, _) => Toggle(value, false);
            _list.Children.Add(box);
        }
        _syncing = false;
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

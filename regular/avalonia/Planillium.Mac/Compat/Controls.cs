using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Planillium.App.Compat;

public sealed class NavigationEventArgs : EventArgs { }

public enum NumberBoxSpinButtonPlacementMode { Hidden, Compact, Inline }
public sealed class NumberBoxValueChangedEventArgs : EventArgs
{
    public double OldValue { get; init; }
    public double NewValue { get; init; }
}

/// <summary>Stand-in for WinUI's NumberBox: header + NumericUpDown. Value is NaN while empty.</summary>
public sealed class NumberBox : StackPanel
{
    private readonly TextBlock _header = new() { IsVisible = false, FontSize = 12, Margin = new Thickness(0, 0, 0, 2) };
    private readonly NumericUpDown _n = new()
    {
        FormatString = "0.##", ClipValueToMinMax = true, Increment = 1, HorizontalAlignment = HorizontalAlignment.Stretch,
        NumberFormat = System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
        ParsingNumberStyle = System.Globalization.NumberStyles.Float,
    };
    private double _last = double.NaN;
    public event Action<NumberBox, NumberBoxValueChangedEventArgs>? ValueChanged;

    public NumberBox()
    {
        Children.Add(_header);
        Children.Add(_n);
        _n.ValueChanged += (_, e) =>
        {
            var nv = Value;
            if (nv.Equals(_last)) return;
            var old = _last; _last = nv;
            ValueChanged?.Invoke(this, new NumberBoxValueChangedEventArgs { OldValue = old, NewValue = nv });
        };
    }

    public object? Header { get => _header.Text; set { _header.Text = value?.ToString(); _header.IsVisible = !string.IsNullOrEmpty(_header.Text); } }
    public string? PlaceholderText { get => _n.Watermark; set => _n.Watermark = value; }
    public double Value
    {
        get => _n.Value is { } d ? (double)d : double.NaN;
        set { _last = value; _n.Value = double.IsNaN(value) ? null : (decimal)value; }
    }
    public double Minimum { get => (double)_n.Minimum; set => _n.Minimum = (decimal)value; }
    public double Maximum { get => (double)_n.Maximum; set => _n.Maximum = (decimal)value; }
    public double SmallChange { get => (double)_n.Increment; set => _n.Increment = (decimal)value; }
    public NumberBoxSpinButtonPlacementMode SpinButtonPlacementMode
    {
        set => _n.ShowButtonSpinner = value != NumberBoxSpinButtonPlacementMode.Hidden;
    }
    public NumericUpDown Inner => _n;
}

/// <summary>Spinner. WinUI's ProgressRing.IsActive maps to an indeterminate bar.</summary>
public sealed class ProgressRing : ProgressBar
{
    protected override Type StyleKeyOverride => typeof(ProgressBar);
    public ProgressRing() { IsIndeterminate = true; }
    public bool IsActive { get => IsIndeterminate; set { IsIndeterminate = value; IsVisible = value; } }
}

/// <summary>WinUI's Style objects are not used on Mac; only ever passed as null/ignored.</summary>
public sealed class Style { }

public class HTextBox : Avalonia.Controls.TextBox
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.TextBox);
    private object? _header; private string? _placeholder;
    /// <summary>WinUI Header → floating label (shown in place of the hint while empty).</summary>
    public object? Header { get => _header; set { _header = value; Sync(); } }
    public string? PlaceholderText { get => _placeholder; set { _placeholder = value; Sync(); } }
    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Fluent paints the floating label in accent blue; Windows shows the header as plain secondary text.
        if (e.NameScope.Find<Avalonia.Controls.TextBlock>("PART_FloatingWatermark") is { } t)
            t.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, t.GetResourceObservable("TextFillColorSecondaryBrush"));
    }
    private void Sync()
    {
        var h = _header?.ToString();
        if (!string.IsNullOrEmpty(h)) { Watermark = _placeholder is { Length: > 0 } p ? $"{h} — {p}" : h; UseFloatingWatermark = true; }
        else Watermark = _placeholder;
    }
}

public sealed class PasswordBox : HTextBox
{
    public PasswordBox() { PasswordChar = '•'; RevealPassword = false; }
    public string Password { get => Text ?? ""; set => Text = value; }
}

public class HComboBox : Avalonia.Controls.ComboBox
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.ComboBox);
    private object? _header;
    public object? Header
    {
        get => _header;
        set { _header = value; if (PlaceholderText is null) PlaceholderText = value?.ToString(); Ui.SetTip(this, value?.ToString()); }
    }
}

public sealed class DateChangedArgs(DateTimeOffset? newDate) { public DateTimeOffset? NewDate { get; } = newDate; }

public sealed class HDatePicker : Avalonia.Controls.CalendarDatePicker
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.CalendarDatePicker);
    public HDatePicker() { SelectedDateFormat = CalendarDatePickerFormat.Custom; CustomDateFormatString = "dd.MM.yyyy"; }
    public object? Header { get => null; set { if (Watermark is null) Watermark = value?.ToString(); Ui.SetTip(this, value?.ToString()); } }
    public string? PlaceholderText { get => Watermark; set => Watermark = value; }
    public DateTimeOffset? Date
    {
        get => SelectedDate is { } d ? new DateTimeOffset(d) : null;
        set => SelectedDate = value?.DateTime;
    }
    public DateTimeOffset MinDate { set => DisplayDateStart = value.DateTime; }
    public DateTimeOffset MaxDate { set => DisplayDateEnd = value.DateTime; }
    public event Action<HDatePicker, DateChangedArgs>? DateChanged;
    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateProperty)
            DateChanged?.Invoke(this, new DateChangedArgs(Date));
    }
    public string? DateFormat { set { } }
}

public class HTextBlock : Avalonia.Controls.TextBlock
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.TextBlock);
    public bool IsTextSelectionEnabled { get; set; }
    public int CharacterSpacing { set => LetterSpacing = value / 100.0; }
}

/// <summary>Segoe MDL2 glyphs do not exist on Mac; the few used are mapped to plain Unicode.</summary>
public sealed class FontIcon : HTextBlock
{
    private static string Map(string g) => g.Length == 0 ? "" : (int)g[0] switch
    {
        0xE76B => "‹", 0xE76C => "›", 0xE70D => "⌄", 0xE70E => "⌃", 0xE70F => "✎", 0xE783 => "⚠", _ => "•",
    };
    public string Glyph { get => Text ?? ""; set => Text = Map(value); }
}

public sealed class HAutoSuggestBox : Avalonia.Controls.AutoCompleteBox
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.AutoCompleteBox);
    public object? Header { get => null; set { if (Watermark is null) Watermark = value?.ToString(); Ui.SetTip(this, value?.ToString()); } }
    public string? PlaceholderText { get => Watermark; set => Watermark = value; }
}

/// <summary>WinUI RadioButtons: optional header, then RadioButton items (row if MaxColumns > 1, else column).</summary>
public sealed class RadioButtons : StackPanel
{
    private static int _n;
    private readonly string _group = "rb" + (++_n);
    private readonly Avalonia.Controls.TextBlock _header = new() { FontSize = 12, IsVisible = false, Margin = new Thickness(0, 0, 0, 4) };
    private readonly StackPanel _items = new() { Spacing = 4 };
    private int _pending = -1;
    private RadioButton? _justChecked;   // the radio whose check raised the event; the old one is unchecked only afterwards
    private int _maxColumns;
    public object? Header { get => _header.Text; set { _header.Text = value?.ToString(); _header.IsVisible = !string.IsNullOrEmpty(_header.Text); } }
    public int MaxColumns { get => _maxColumns; set { _maxColumns = value; _items.Orientation = value > 1 ? Avalonia.Layout.Orientation.Horizontal : Avalonia.Layout.Orientation.Vertical; if (value > 1) _items.Spacing = 8; } }
    public ItemsShim Items { get; }
    public event Action<RadioButtons, object?>? SelectionChanged;
    public RadioButtons() { Children.Add(_header); Children.Add(_items); Items = new ItemsShim(this); }
    public int SelectedIndex
    {
        get { if (_justChecked is { IsChecked: true }) return _items.Children.IndexOf(_justChecked); for (int i = 0; i < _items.Children.Count; i++) if (_items.Children[i] is RadioButton { IsChecked: true }) return i; return -1; }
        set { _pending = value; for (int i = 0; i < _items.Children.Count; i++) ((RadioButton)_items.Children[i]).IsChecked = i == value; }
    }
    public object? SelectedItem { get { if (_justChecked is { IsChecked: true }) return _justChecked; var i = SelectedIndex; return i < 0 ? null : _items.Children[i]; } }
    public sealed class ItemsShim
    {
        private readonly RadioButtons _o;
        public ItemsShim(RadioButtons o) => _o = o;
        public void Add(object item)
        {
            var rb = item as RadioButton ?? new RadioButton { Content = item };
            rb.GroupName = _o._group;
            rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true) { _o._justChecked = rb; try { _o.SelectionChanged?.Invoke(_o, rb); } finally { _o._justChecked = null; } } };
            _o._items.Children.Add(rb);
            if (_o._pending == _o._items.Children.Count - 1) rb.IsChecked = true;
        }
    }
}

/// <summary>WinUI ToggleSwitch: header above, IsOn, Toggled event.</summary>
public sealed class ToggleSwitch : StackPanel
{
    private readonly Avalonia.Controls.TextBlock _header = new() { FontSize = 12, IsVisible = false, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Avalonia.Controls.ToggleSwitch _t = new();
    public ToggleSwitch() { Children.Add(_header); Children.Add(_t); _t.IsCheckedChanged += (_, e) => Toggled?.Invoke(this, e); }
    public object? Header { get => _header.Text; set { _header.Text = value?.ToString(); _header.IsVisible = !string.IsNullOrEmpty(_header.Text); } }
    public bool IsOn { get => _t.IsChecked == true; set => _t.IsChecked = value; }
    public object? OnContent { get => _t.OnContent; set => _t.OnContent = value; }
    public object? OffContent { get => _t.OffContent; set => _t.OffContent = value; }
    public event EventHandler<RoutedEventArgs>? Toggled;
}

public sealed class SliderChangedArgs(double v) { public double NewValue { get; } = v; }

/// <summary>WinUI Slider: header above, StepFrequency, ValueChanged.</summary>
public sealed class Slider : StackPanel
{
    private readonly Avalonia.Controls.TextBlock _header = new() { FontSize = 12, IsVisible = false, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Avalonia.Controls.Slider _s = new() { IsSnapToTickEnabled = true };
    public Slider() { Children.Add(_header); Children.Add(_s); _s.ValueChanged += (_, e) => ValueChanged?.Invoke(this, new SliderChangedArgs(e.NewValue)); }
    public object? Header { get => _header.Text; set { _header.Text = value?.ToString(); _header.IsVisible = !string.IsNullOrEmpty(_header.Text); } }
    public double Minimum { get => _s.Minimum; set => _s.Minimum = value; }
    public double Maximum { get => _s.Maximum; set => _s.Maximum = value; }
    public double Value { get => _s.Value; set => _s.Value = value; }
    public double StepFrequency { set => _s.TickFrequency = value; }
    public double TickFrequency { set { } }
    public string? TickPlacement { set { } }
    public event Action<Slider, SliderChangedArgs>? ValueChanged;
}

/// <summary>WinUI ScrollViewer API on top of Avalonia's (ScrollableHeight, VerticalOffset, ChangeView).</summary>
public sealed class HScrollViewer : Avalonia.Controls.ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.ScrollViewer);
    public double ScrollableHeight => Math.Max(0, Extent.Height - Viewport.Height);
    public double ScrollableWidth => Math.Max(0, Extent.Width - Viewport.Width);
    public double VerticalOffset => Offset.Y;
    public double HorizontalOffset => Offset.X;
    public void ChangeView(double? x, double? y, float? zoom, bool disableAnimation = true)
        => Offset = new Avalonia.Vector(x ?? Offset.X, y ?? Offset.Y);
}

/// <summary>WinUI MenuFlyoutItem: a MenuItem with a Text property.</summary>
public sealed class MenuFlyoutItem : Avalonia.Controls.MenuItem
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.MenuItem);
    public string Text { get => Header?.ToString() ?? ""; set => Header = value; }
}

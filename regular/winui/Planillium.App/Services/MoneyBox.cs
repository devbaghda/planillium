using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Planillium.App.Services;

/// <summary>Money input for Settings → Income (B1, 2026-10-23): a TextBox that groups thousands
/// with a thin space as the user types and has no spin arrows (the NumberBox it replaces had both
/// the arrows and a locale-dependent separator). Rules live in <see cref="MoneyFormat"/>.
/// <see cref="Value"/> is NaN while the box is empty; <see cref="ValueChanged"/> fires for any
/// change of the number, including a programmatic one — same contract the NumberBox had.
/// B1b (2026-10-25): <see cref="ValueChanged"/> is for live readouts only; anything that SAVES
/// listens to <see cref="Committed"/>, which fires once when the box is left or Enter is pressed and
/// the number differs from the last committed one. A typed minus sign raises
/// <see cref="NegativeTyped"/> (true) and is dropped from the text; the next minus-free change or
/// leaving the box raises it again with false.</summary>
public sealed class MoneyBox : TextBox
{
    private bool _internal;
    private double _value = double.NaN;

    public event EventHandler? ValueChanged;
    public event EventHandler? Committed;
    public event EventHandler<bool>? NegativeTyped;
    private string? _lastFixed;
    private double _committed = double.NaN;

    public MoneyBox()
    {
        InputScope = new Microsoft.UI.Xaml.Input.InputScope
        {
            Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number) },
        };
        TextChanged += OnTextChanged;
    }

    public double Value
    {
        get => _value;
        set
        {
            _lastFixed = null;
            _internal = true;
            try { Text = MoneyFormat.FromNumber(value, CultureInfo.CurrentCulture); }
            finally { _internal = false; }
            Update();
            _committed = _value;
            NegativeTyped?.Invoke(this, false);
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internal) return;
        // Our own correction below raises a second TextChanged (delivered after _internal is cleared);
        // it must not hide the hint the first one just showed.
        if (Text == _lastFixed) return;
        _lastFixed = null;
        NegativeTyped?.Invoke(this, Text.Contains('-') || Text.Contains('−'));
        var fixedText = MoneyFormat.Normalize(Text, SelectionStart, CultureInfo.CurrentCulture, out var caret);
        if (fixedText != Text)
        {
            _internal = true;
            _lastFixed = fixedText;
            try { Text = fixedText; SelectionStart = caret; }
            finally { _internal = false; }
        }
        Update();
    }

    private void Update()
    {
        var v = MoneyFormat.Parse(Text);
        if (v.Equals(_value)) return;
        _value = v;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        NegativeTyped?.Invoke(this, false);
        Commit();
    }

    protected override void OnKeyDown(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Windows.System.VirtualKey.Enter) Commit();
    }

    /// <summary>Settles the text (decimal dot → culture separator) and, if the number differs from
    /// the last committed one, raises <see cref="Committed"/> once.</summary>
    private void Commit()
    {
        var settled = MoneyFormat.Settle(Text, CultureInfo.CurrentCulture);
        if (settled != Text)
        {
            _internal = true;
            try { Text = settled; }
            finally { _internal = false; }
        }
        if (_value.Equals(_committed)) return;
        _committed = _value;
        Committed?.Invoke(this, EventArgs.Empty);
    }
}

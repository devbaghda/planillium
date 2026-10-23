using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Planillium.App.Services;

/// <summary>Money input for Settings → Income (B1, 2026-10-23): a TextBox that groups thousands
/// with a thin space as the user types and has no spin arrows (the NumberBox it replaces had both
/// the arrows and a locale-dependent separator). Rules live in <see cref="MoneyFormat"/>.
/// <see cref="Value"/> is NaN while the box is empty; <see cref="ValueChanged"/> fires for any
/// change of the number, including a programmatic one — same contract the NumberBox had.</summary>
public sealed class MoneyBox : TextBox
{
    private bool _internal;
    private double _value = double.NaN;

    public event EventHandler? ValueChanged;

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
            _internal = true;
            try { Text = MoneyFormat.FromNumber(value, CultureInfo.CurrentCulture); }
            finally { _internal = false; }
            Update();
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internal) return;
        var fixedText = MoneyFormat.Normalize(Text, SelectionStart, CultureInfo.CurrentCulture, out var caret);
        if (fixedText != Text)
        {
            _internal = true;
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
        var settled = MoneyFormat.Settle(Text, CultureInfo.CurrentCulture);
        if (settled == Text) return;
        _internal = true;
        try { Text = settled; }
        finally { _internal = false; }
    }
}

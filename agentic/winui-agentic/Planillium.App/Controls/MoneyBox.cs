using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Planillium.App.Services;

namespace Planillium.App.Controls;

/// <summary>
/// A text box for money amounts: no spin buttons, digits grouped with a thin space as the user types
/// (<see cref="MoneyFormat"/>). <see cref="Value"/> is a plain number, NaN when the box is empty.
/// </summary>
public sealed class MoneyBox : TextBox
{
    private readonly char _decimalSeparator = MoneyFormat.DecimalSeparator;
    private bool _normalising;

    /// <summary>Raised after a text change that leaves a valid number in the box. Not raised when the box is empty.</summary>
    public event EventHandler? ValueChanged;

    public MoneyBox()
    {
        InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
        TextChanged += OnTextChanged;
    }

    /// <summary>The amount as a plain number; NaN when the box is empty or has no digits.</summary>
    public double Value
    {
        get => MoneyFormat.Parse(Text);
        set => Text = MoneyFormat.FromNumber(value, _decimalSeparator);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        var settled = MoneyFormat.Settle(Text, _decimalSeparator);
        if (settled != Text) Text = settled;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_normalising) return;
        var raw = Text;
        var (text, caret) = MoneyFormat.Normalize(raw, SelectionStart, _decimalSeparator);
        if (text != raw)
        {
            _normalising = true;
            try
            {
                Text = text;
                Select(caret, 0);
            }
            finally
            {
                _normalising = false;
            }
        }
        if (!double.IsNaN(Value)) ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}

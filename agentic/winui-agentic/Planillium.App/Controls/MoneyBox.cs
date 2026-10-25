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

    /// <summary>Raised when the user leaves the box or presses Enter and the number differs from the one
    /// the box had when it got focus (or was last committed). Never raised for programmatic <see cref="Value"/> sets.</summary>
    public event EventHandler? ValueCommitted;

    /// <summary>Raised when <see cref="NegativeAttempt"/> changes.</summary>
    public event EventHandler? NegativeAttemptChanged;

    private double _baseline = double.NaN;
    private bool _hasBaseline;
    private bool _negativeAttempt;

    /// <summary>True while the last keystroke or paste contained a minus sign (which the box drops).</summary>
    public bool NegativeAttempt
    {
        get => _negativeAttempt;
        private set
        {
            if (_negativeAttempt == value) return;
            _negativeAttempt = value;
            NegativeAttemptChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raises <see cref="ValueCommitted"/> if the box holds a valid number that differs from the baseline.</summary>
    public void Commit()
    {
        if (!_hasBaseline) return; // never focused since the last commit/leave: nothing the user typed
        var v = Value;
        if (double.IsNaN(v) || v == _baseline) return;
        _baseline = v;
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        _baseline = Value;
        _hasBaseline = true;
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        var settled = MoneyFormat.Settle(Text, _decimalSeparator);
        if (settled != Text) Text = settled;
        NegativeAttempt = false;
        Commit();
        _hasBaseline = false;
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var settled = MoneyFormat.Settle(Text, _decimalSeparator);
            if (settled != Text) Text = settled;
            Commit();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_normalising) return;
        var raw = Text;
        NegativeAttempt = MoneyFormat.ContainsMinus(raw);
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

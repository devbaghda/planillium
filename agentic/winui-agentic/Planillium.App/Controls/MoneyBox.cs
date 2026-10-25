using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Planillium.App.Services;

namespace Planillium.App.Controls;

/// <summary>
/// A text box for money amounts: no spin buttons, digits grouped with a thin space as the user types
/// (<see cref="MoneyFormat"/>). A typed or pasted minus sign is kept as one leading "-" and makes the box
/// invalid (<see cref="IsNegative"/>): <see cref="Value"/> is then NaN, so nothing that reads it saves a
/// negative amount. <see cref="Value"/> is also NaN when the box is empty.
/// </summary>
public sealed class MoneyBox : TextBox
{
    private readonly char _decimalSeparator = MoneyFormat.DecimalSeparator;
    private bool _normalising;

    /// <summary>Raised after a text change that leaves a valid number in the box. Not raised when the box is
    /// empty or negative.</summary>
    public event EventHandler? ValueChanged;

    public MoneyBox()
    {
        InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
        TextChanged += OnTextChanged;
    }

    /// <summary>The amount as a plain number; NaN when the box is empty, has no digits, or is negative.</summary>
    public double Value
    {
        // Read from Text, not from IsNegative: a programmatic Text set is visible here at once, while the
        // TextChanged that updates IsNegative arrives later.
        get => MoneyFormat.ContainsMinus(Text) ? double.NaN : MoneyFormat.Parse(Text);
        set => Text = MoneyFormat.FromNumber(value, _decimalSeparator);
    }

    /// <summary>Raised when the user leaves the box or presses Enter and the number differs from the one
    /// the box had when it got focus (or was last committed). Never raised for programmatic <see cref="Value"/> sets,
    /// nor while the box is empty or negative.</summary>
    public event EventHandler? ValueCommitted;

    /// <summary>Raised by every Enter, after <see cref="Commit"/> (so a valid number is saved first). The page decides
    /// what follows: it moves focus out of the box only when <see cref="Value"/> is a number; with an empty or negative
    /// box the cursor stays so the user can fix it. Raised for invalid values too so the new-tag line can show its
    /// "needs a number" message.</summary>
    public event EventHandler? EnterPressed;

    /// <summary>Raised when <see cref="IsNegative"/> changes.</summary>
    public event EventHandler? IsNegativeChanged;

    private double _baseline = double.NaN;
    private bool _hasBaseline;
    private bool _isNegative;

    /// <summary>True while the box's text holds a minus sign. Derived from the text itself (set on every text
    /// change, including programmatic ones), so it stays true for as long as the minus is in the box, also after
    /// the box is left, and turns false as soon as the minus is deleted or the text is replaced.</summary>
    public bool IsNegative
    {
        get => _isNegative;
        private set
        {
            if (_isNegative == value) return;
            _isNegative = value;
            IsNegativeChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>Leaving saves a valid number. An invalid one (negative) is left in the box as typed, with its
    /// message; nothing is saved and the saved value stays as it was (user decision 2026-10-25).</summary>
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Settle();
        Commit();
        _hasBaseline = false;
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            Settle();
            Commit();
            e.Handled = true;
            // After Commit, so the save happens before the page moves focus away.
            EnterPressed?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.OnKeyDown(e);
    }

    private void Settle()
    {
        var settled = MoneyFormat.Settle(Text, _decimalSeparator);
        if (settled != Text) Text = settled;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_normalising) return;
        var raw = Text;
        var (text, caret) = MoneyFormat.NormalizeKeepingSign(raw, SelectionStart, _decimalSeparator);
        // Computed from the text about to be shown. WinUI delivers the TextChanged for the write-back below
        // after _normalising is already false; that echo carries the same text, so it yields the same answer
        // and cannot hide the message the keystroke just showed (the B1b D1 defect).
        IsNegative = MoneyFormat.ContainsMinus(text);
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

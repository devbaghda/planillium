using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Planillium.App.Services;

/// <summary>Money input for Settings → Income (B1, 2026-10-23): a TextBox that groups thousands
/// with a thin space as the user types and has no spin arrows (the NumberBox it replaces had both
/// the arrows and a locale-dependent separator). Digit rules live in <see cref="MoneyFormat"/>,
/// which formats the unsigned part only; the sign is this class's business.
/// <see cref="Value"/> is NaN while the box is empty; <see cref="ValueChanged"/> fires for any
/// change of the number, including a programmatic one.
/// B1b (2026-10-25): <see cref="ValueChanged"/> is for live readouts only; anything that SAVES
/// listens to <see cref="Committed"/>, which fires once when the box is left or Enter is pressed and
/// the number differs from the last committed one.
/// B1c (2026-10-25, reverses B1b's "minus is dropped"): a typed minus is KEPT in front of the
/// digits ("-500"). While the text carries a minus, <see cref="IsNegative"/> is true, the box is
/// invalid and <see cref="Committed"/> never fires — nothing is saved, the text stays as typed even
/// when the box is left. <see cref="NegativeChanged"/> reports every change of that state so a
/// message can stay visible exactly as long as the text is negative. Enter raises
/// <see cref="Submitted"/> after committing, so the page can move the cursor out of the box.
/// B1d (2026-10-25): a box with <see cref="KeepsStoredValue"/> left empty (letters are stripped as
/// typed, so "abc" ends up empty too) goes back to its stored amount instead of sitting blank.
/// B1h (2026-10-27): Esc in a <see cref="KeepsStoredValue"/> box discards the typing, shows the stored
/// amount and raises <see cref="Cancelled"/> so the page moves the cursor out. Other boxes (the
/// new-tag line) keep their own Esc handling.</summary>
public sealed class MoneyBox : TextBox
{
    private bool _internal;
    private double _value = double.NaN;

    public event EventHandler? ValueChanged;
    public event EventHandler? Committed;
    public event EventHandler<bool>? NegativeChanged;
    public event EventHandler? Submitted;
    private string? _lastFixed;
    private double _committed = double.NaN;

    /// <summary>True while the text carries a minus sign (invalid; never saved).</summary>
    public bool IsNegative { get; private set; }

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
        }
    }

    private static bool IsMinus(char c) => c == '-' || c == '−';

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internal) return;
        // Our own correction below raises a second TextChanged (delivered after _internal is cleared);
        // re-reading it would be harmless, skipping it just avoids the extra work.
        if (Text == _lastFixed) return;
        _lastFixed = null;
        var raw = Text;
        var neg = raw.Any(IsMinus);
        var caret = Math.Min(SelectionStart, raw.Length);
        var unsigned = raw;
        if (neg)
        {
            caret -= raw.Take(caret).Count(IsMinus);
            unsigned = new string(raw.Where(c => !IsMinus(c)).ToArray());
        }
        var fixedText = MoneyFormat.Normalize(unsigned, caret, CultureInfo.CurrentCulture, out var newCaret);
        if (neg) { fixedText = "-" + fixedText; newCaret++; }
        if (fixedText != raw)
        {
            _internal = true;
            _lastFixed = fixedText;
            try { Text = fixedText; SelectionStart = Math.Min(newCaret, fixedText.Length); }
            finally { _internal = false; }
        }
        Update();
    }

    /// <summary>Re-reads the number and the sign from the text; raises the two change events.
    /// B1d: both fields are set BEFORE either event fires. Firing ValueChanged while IsNegative still
    /// held the old sign made the hourly readout read "-500 123 → 500 123" as still negative and stick
    /// on "= —/h".</summary>
    private void Update()
    {
        var neg = Text.Any(IsMinus);
        var v = MoneyFormat.Parse(Text);          // digits only; the sign is applied here
        if (neg && !double.IsNaN(v)) v = -v;
        var valueChanged = !v.Equals(_value);
        var signChanged = neg != IsNegative;
        _value = v;
        IsNegative = neg;
        if (valueChanged) ValueChanged?.Invoke(this, EventArgs.Empty);
        if (signChanged) NegativeChanged?.Invoke(this, neg);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Commit();
    }

    protected override void OnKeyDown(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Windows.System.VirtualKey.Escape && KeepsStoredValue)
        {
            // B1h: Esc in a box showing a stored amount throws away whatever was typed (digits, an
            // emptied box, letters/spaces, a minus) and shows the stored amount again. Nothing is
            // saved. The page moves the cursor out of the box on Cancelled.
            e.Handled = true;
            Value = _committed;
            Cancelled?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        Commit();
        Submitted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Settles the text (decimal dot → culture separator) and, if the number is valid and
    /// differs from the last committed one, raises <see cref="Committed"/> once. A negative text is
    /// settled but never committed.</summary>
    private void Commit()
    {
        var neg = Text.Any(IsMinus);
        var unsigned = neg ? new string(Text.Where(c => !IsMinus(c)).ToArray()) : Text;
        var settled = MoneyFormat.Settle(unsigned, CultureInfo.CurrentCulture);
        if (neg) settled = "-" + settled;
        if (settled != Text)
        {
            _internal = true;
            try { Text = settled; }
            finally { _internal = false; }
        }
        if (IsNegative) return;
        // B1d: a box holding a stored amount never leaves an empty (or unaccepted) text behind it —
        // the box would say one thing and config.json another. It goes back to the stored amount and
        // says so through Reverted; nothing is saved.
        if (KeepsStoredValue && (double.IsNaN(_value) || (Accepts is not null && !Accepts(_value))))
        {
            if (double.IsNaN(_committed)) return;
            Value = _committed;
            Reverted?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (_value.Equals(_committed)) return;
        _committed = _value;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>B1d: set on boxes that show a stored amount (salary, payable tags). Leaving such a
    /// box empty, or with a value <see cref="Accepts"/> refuses, puts the stored amount back.</summary>
    public bool KeepsStoredValue { get; set; }

    /// <summary>B1d: optional extra rule for a non-negative value (e.g. "above 0" for payable tags).</summary>
    public Func<double, bool>? Accepts { get; set; }

    /// <summary>B1d: raised after an empty or refused value was replaced by the stored amount.</summary>
    public event EventHandler? Reverted;

    /// <summary>B1h: raised after Esc put the stored amount back (only on <see cref="KeepsStoredValue"/>
    /// boxes); the page then moves the cursor out of the box.</summary>
    public event EventHandler? Cancelled;
}

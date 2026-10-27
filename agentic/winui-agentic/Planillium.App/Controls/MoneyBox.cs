using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Planillium.App.Services;

namespace Planillium.App.Controls;

/// <summary>
/// A text box for money amounts: no spin buttons, digits grouped with a thin space as the user types
/// (<see cref="MoneyFormat"/>). A typed or pasted minus sign is kept as one leading "-" and makes the box
/// invalid (<see cref="IsNegative"/>); with <see cref="AllowZero"/> off, a zero amount is invalid too. While invalid
/// (<see cref="ProblemText"/> not null) <see cref="Value"/> is NaN, so nothing that reads it saves the amount.
/// <see cref="Value"/> is also NaN when the box is empty (letters are dropped as typed, so "abc" leaves it empty).
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

    /// <summary>The amount as a plain number; NaN when the box is empty, has no digits, or is invalid
    /// (negative, or zero where <see cref="AllowZero"/> is off).</summary>
    public double Value
    {
        // Read from Text, not from the cached problem state: a programmatic Text set is visible here at once,
        // while the TextChanged that updates the state arrives later.
        get => ProblemFor(Text, AllowZero) != null ? double.NaN : MoneyFormat.Parse(Text);
        set
        {
            Text = MoneyFormat.FromNumber(value, _decimalSeparator);
            if (!double.IsNaN(value)) _saved = value;
        }
    }

    /// <summary>False for amounts that must be above zero (a payable tag's rate, the new-tag amount): a 0 is then
    /// invalid, shown with a message like a minus, and never saved. True (default) for the previous salary.</summary>
    public bool AllowZero { get; set; } = true;

    /// <summary>True for boxes that show a saved amount (salary, payable-tag rates): leaving the box with no number
    /// in it (emptied, or only letters typed) or with a too-large amount puts the saved amount back, so the box never
    /// shows blank or a refused 12-digit amount while the stored value is something else (B1d, B1g). Esc in such a box
    /// discards whatever was typed, puts the saved amount back and raises <see cref="EscapePressed"/> (B1g). Off for the
    /// new-tag amount, where empty is the starting state and Esc belongs to the add line.</summary>
    public bool RevertEmptyOnLeave { get; set; }

    /// <summary>Raised after leaving the box (or Enter) put the saved amount back (see <see cref="RevertEmptyOnLeave"/>).
    /// The argument is the refused amount's problem (<see cref="TooLargeText"/>), or null when the box was empty.</summary>
    public event EventHandler<string?>? Reverted;

    /// <summary>Raised by Esc in a box with <see cref="RevertEmptyOnLeave"/> on, after the saved amount was put back.
    /// The argument is true when typed text was discarded (the box showed something other than the saved amount). The
    /// page moves focus out of the box.</summary>
    public event EventHandler<bool>? EscapePressed;

    public const string NegativeText = "Amounts can't be negative.";
    public const string ZeroText = "Amount must be more than 0.";
    public const string TooLargeText = "Amount is too large (9 digits at most).";

    /// <summary>The message for an invalid box text, or null: <see cref="NegativeText"/> for a minus anywhere,
    /// <see cref="TooLargeText"/> for more than <see cref="MoneyFormat.MaxIntegerDigits"/> integer digits (B1g),
    /// <see cref="ZeroText"/> for a zero amount when <paramref name="allowZero"/> is false. Empty is not a problem
    /// here (each caller decides what empty means).</summary>
    public static string? ProblemFor(string? text, bool allowZero)
    {
        text ??= "";
        if (MoneyFormat.ContainsMinus(text)) return NegativeText;
        if (MoneyFormat.IntegerDigitCount(text) > MoneyFormat.MaxIntegerDigits) return TooLargeText;
        if (!allowZero && MoneyFormat.Parse(text) == 0) return ZeroText;
        return null;
    }

    /// <summary>The problem of the text in the box right now (<see cref="ProblemFor"/>). Unlike <see cref="ProblemText"/>
    /// it does not wait for TextChanged, so it is already null right after a revert.</summary>
    public string? CurrentProblem => ProblemFor(Text, AllowZero);

    /// <summary>The current problem message (see <see cref="ProblemFor"/>), or null while the box is valid or empty.
    /// Derived from the text on every change, also programmatic ones, and kept after the box is left.</summary>
    public string? ProblemText
    {
        get => _problem;
        private set
        {
            if (_problem == value) return;
            var wasNegative = IsNegative;
            _problem = value;
            ProblemChanged?.Invoke(this, EventArgs.Empty);
            if (wasNegative != IsNegative) IsNegativeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when <see cref="ProblemText"/> changes.</summary>
    public event EventHandler? ProblemChanged;

    private string? _problem;

    /// <summary>The last amount known to be stored: set by a programmatic <see cref="Value"/> and by every
    /// successful <see cref="Commit"/>. What <see cref="RevertEmptyOnLeave"/> puts back.</summary>
    private double _saved = double.NaN;

    /// <summary>Raised when the user leaves the box or presses Enter and the number differs from the one
    /// the box had when it got focus (or was last committed). Never raised for programmatic <see cref="Value"/> sets,
    /// nor while the box is empty or negative.</summary>
    public event EventHandler? ValueCommitted;

    /// <summary>Raised by every Enter, after <see cref="Commit"/> (so a valid number is saved first) and, with
    /// <see cref="RevertEmptyOnLeave"/> on, after an empty box got its saved amount back. The page decides what follows:
    /// it moves focus out of the box when <see cref="Value"/> is a number; with a negative or zero box the cursor stays
    /// so the user can fix it. Raised for invalid values too so the new-tag line can show its "needs a number" message.</summary>
    public event EventHandler? EnterPressed;

    /// <summary>Raised when <see cref="IsNegative"/> changes.</summary>
    public event EventHandler? IsNegativeChanged;

    private double _baseline = double.NaN;
    private bool _hasBaseline;

    /// <summary>True while the box's text holds a minus sign. Derived from the text itself (set on every text
    /// change, including programmatic ones), so it stays true for as long as the minus is in the box, also after
    /// the box is left, and turns false as soon as the minus is deleted or the text is replaced.</summary>
    public bool IsNegative => _problem == NegativeText;

    /// <summary>Raises <see cref="ValueCommitted"/> if the box holds a valid number that differs from the baseline.</summary>
    public void Commit()
    {
        if (!_hasBaseline) return; // never focused since the last commit/leave: nothing the user typed
        var v = Value;
        if (double.IsNaN(v) || v == _baseline) return;
        _baseline = v;
        _saved = v;
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        _baseline = Value;
        _hasBaseline = true;
    }

    /// <summary>Leaving saves a valid number. An invalid one (negative, or a zero where it is not allowed) is left in
    /// the box as typed, with its message; nothing is saved and the saved value stays as it was (user decision
    /// 2026-10-25). A box left with no number in it, or with a too-large amount, shows the saved amount again when
    /// <see cref="RevertEmptyOnLeave"/> is on (B1d, B1g: box and stored value agree).</summary>
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Settle();
        Commit();
        _hasBaseline = false;
        RevertIfNoAmount();
    }

    /// <summary>Puts the saved amount back when <see cref="RevertEmptyOnLeave"/> is on and the box holds no number
    /// (emptied, letters or spaces only) or a too-large amount (B1g: the 12 digits are not kept on screen; nothing was
    /// saved). A minus or a zero is left as typed (it has its own message). Run on every way out of the box: leaving
    /// it, and Enter (B1d fix: Enter used to leave the box blank and focused).</summary>
    private void RevertIfNoAmount()
    {
        if (!RevertEmptyOnLeave || double.IsNaN(_saved)) return;
        var problem = CurrentProblem;
        var tooLarge = problem == TooLargeText;
        if (!tooLarge && (problem != null || !double.IsNaN(MoneyFormat.Parse(Text)))) return;
        PutSavedBack();
        Reverted?.Invoke(this, tooLarge ? problem : null);
    }

    /// <summary>Shows the saved amount again. The box then holds the stored amount, so the coming LostFocus must not
    /// count it as a new value to save (nor a box that held a minus when it got focus).</summary>
    private void PutSavedBack()
    {
        Value = _saved;
        if (_hasBaseline) _baseline = _saved;
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && RevertEmptyOnLeave)
        {
            // B1g (user rule 2026-10-27): Esc discards what was typed -- a new number, an emptied box, letters, a minus,
            // a too-large amount -- shows the saved amount and the page moves the cursor out. Nothing is saved.
            e.Handled = true;
            var discarded = Text != MoneyFormat.FromNumber(_saved, _decimalSeparator);
            PutSavedBack();
            EscapePressed?.Invoke(this, discarded);
            return;
        }
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            Settle();
            Commit();
            // Enter is a way out like leaving: an empty or too-large box shows the saved amount again before the page
            // moves focus away (box and stored value agree).
            RevertIfNoAmount();
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
        // Up to BoxIntegerDigits are kept, so a too-large amount stays visible with its message instead of being cut
        // to 9 digits without a word (B1g).
        var (text, caret) = MoneyFormat.NormalizeKeepingSign(raw, SelectionStart, _decimalSeparator,
            MoneyFormat.BoxIntegerDigits);
        // Computed from the text about to be shown. WinUI delivers the TextChanged for the write-back below
        // after _normalising is already false; that echo carries the same text, so it yields the same answer
        // and cannot hide the message the keystroke just showed (the B1b D1 defect).
        ProblemText = ProblemFor(text, AllowZero);
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

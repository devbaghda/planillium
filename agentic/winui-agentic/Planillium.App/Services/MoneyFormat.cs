using System.Globalization;
using System.Text;

namespace Planillium.App.Services;

/// <summary>
/// Pure parse/format rules for the money boxes in Settings -> Income. Display only: integer digits
/// are grouped with a thin space (U+2009) every three digits, the saved value is always a plain number.
/// No WinUI dependencies, so it can be unit-tested on its own.
/// </summary>
public static class MoneyFormat
{
    /// <summary>The grouping character used in the display (U+2009, never a normal space).</summary>
    public const char ThinSpace = ' ';

    /// <summary>Integer digits kept in a box; further typed digits are dropped.</summary>
    public const int MaxIntegerDigits = 9;

    /// <summary>Decimal digits kept in a box; further typed digits are dropped.</summary>
    public const int MaxDecimalDigits = 2;

    /// <summary>The current culture's decimal separator (',' in Italian/Dutch, '.' in English).</summary>
    public static char DecimalSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

    /// <summary>
    /// Cleans raw box text. Keeps digits, the first decimal separator (',' or '.'), at most
    /// <see cref="MaxIntegerDigits"/> integer and <see cref="MaxDecimalDigits"/> decimal digits.
    /// Everything else (letters, '-', spaces, a second separator) is dropped. Returns the display text
    /// and the caret index in it, placed after the same digit the caret was after in <paramref name="raw"/>.
    /// </summary>
    public static (string Text, int Caret) Normalize(string raw, int caret, char decimalSeparator)
    {
        raw ??= "";
        var intRaw = new StringBuilder();
        var dec = new StringBuilder();
        var hasSep = false;
        var sepIndex = -1;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (char.IsAsciiDigit(c))
            {
                if (!hasSep) intRaw.Append(c);
                else if (dec.Length < MaxDecimalDigits) dec.Append(c);
            }
            else if ((c == ',' || c == '.') && !hasSep)
            {
                hasSep = true;
                sepIndex = i;
            }
        }

        var intDigits = intRaw.ToString().TrimStart('0');
        if (intDigits.Length > MaxIntegerDigits) intDigits = intDigits[..MaxIntegerDigits];
        if (intDigits.Length == 0 && !hasSep && intRaw.Length > 0) intDigits = "0";

        var grouped = Group(intDigits);
        var text = hasSep ? $"{grouped}{decimalSeparator}{dec}" : grouped;

        // Caret: count the digits typed before the caret, and place the caret after the same number of
        // digits in the result. A caret that was after the separator stays after it.
        var caretClamped = Math.Clamp(caret, 0, raw.Length);
        var digitsLeft = 0;
        for (var i = 0; i < caretClamped; i++)
            if (char.IsAsciiDigit(raw[i])) digitsLeft++;
        var sepLeft = sepIndex >= 0 && sepIndex < caretClamped;

        var pos = 0;
        if (digitsLeft > 0)
        {
            var seen = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsAsciiDigit(text[i])) continue;
                seen++;
                if (seen == digitsLeft) { pos = i + 1; break; }
            }
            if (seen < digitsLeft) pos = text.Length;
        }
        if (sepLeft && hasSep)
        {
            var sepOut = grouped.Length;
            if (pos <= sepOut) pos = sepOut + 1;
        }
        return (text, Math.Min(pos, text.Length));
    }

    /// <summary>Groups an integer digit string with a thin space every three digits from the right.</summary>
    public static string Group(string digits)
    {
        var sb = new StringBuilder(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(ThinSpace);
            sb.Append(digits[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Display text for a stored number: grouped integer part, decimals only if present (max 2),
    /// no trailing ",00". NaN gives an empty string. Negative values show as 0, as the old
    /// NumberBox minimum did.
    /// </summary>
    public static string FromNumber(double value, char decimalSeparator)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "";
        var rounded = Math.Round(Math.Max(0, value), MaxDecimalDigits);
        var s = rounded.ToString("0.##", CultureInfo.InvariantCulture);
        var dot = s.IndexOf('.');
        var intPart = dot < 0 ? s : s[..dot];
        var decPart = dot < 0 ? "" : s[(dot + 1)..];
        return decPart.Length > 0
            ? $"{Group(intPart)}{decimalSeparator}{decPart}"
            : Group(intPart);
    }

    /// <summary>
    /// Number for a display or typed text: digits, and the first decimal separator (',' or '.').
    /// Spaces and other characters are ignored. Returns NaN when there are no digits at all.
    /// </summary>
    public static double Parse(string text)
    {
        var intDigits = new StringBuilder();
        var decDigits = new StringBuilder();
        var hasSep = false;
        foreach (var c in text ?? "")
        {
            if (char.IsAsciiDigit(c)) (hasSep ? decDigits : intDigits).Append(c);
            else if ((c == ',' || c == '.') && !hasSep) hasSep = true;
        }
        if (intDigits.Length == 0 && decDigits.Length == 0) return double.NaN;
        var number = (intDigits.Length == 0 ? "0" : intDigits.ToString())
                     + (decDigits.Length > 0 ? "." + decDigits : "");
        return double.Parse(number, CultureInfo.InvariantCulture);
    }
}

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

    /// <summary>Integer digits an amount may have (999 999 999 at most). <see cref="Normalize"/> drops further typed
    /// digits by default; the money boxes keep up to <see cref="BoxIntegerDigits"/> so an over-long amount can be shown
    /// and refused with a message (B1g) instead of being cut silently.</summary>
    public const int MaxIntegerDigits = 9;

    /// <summary>Integer digits a money box keeps while typing (more are dropped); anything above
    /// <see cref="MaxIntegerDigits"/> is refused as too large (B1g).</summary>
    public const int BoxIntegerDigits = 15;

    /// <summary>Decimal digits kept in a box; further typed digits are dropped.</summary>
    public const int MaxDecimalDigits = 2;

    /// <summary>The current culture's decimal separator (',' in Italian/Dutch, '.' in English).</summary>
    public static char DecimalSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

    /// <summary>
    /// Cleans raw box text. Keeps digits, the first decimal separator (',' or '.'), at most
    /// <paramref name="maxIntegerDigits"/> (default <see cref="MaxIntegerDigits"/>) integer and <see cref="MaxDecimalDigits"/> decimal digits.
    /// Everything else (letters, '-', spaces, a second separator) is dropped (the money boxes use
    /// <see cref="NormalizeKeepingSign"/>, which puts a typed minus back in front). Returns the display text
    /// and the caret index in it, placed after the same digit the caret was after in <paramref name="raw"/>.
    /// </summary>
    public static (string Text, int Caret) Normalize(string raw, int caret, char decimalSeparator,
        int maxIntegerDigits = MaxIntegerDigits)
    {
        raw ??= "";
        var intRaw = new StringBuilder();
        var dec = new StringBuilder();
        var hasSep = false;
        var sepIndex = -1;
        var pendingDot = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (char.IsAsciiDigit(c))
            {
                if (!hasSep) intRaw.Append(c);
                else if (dec.Length < MaxDecimalDigits) dec.Append(c);
            }
            else if (IsThousandsDot(raw, i))
            {
                // Thousands separator: dropped, the digits around it stay one number.
            }
            else if ((c == ',' || c == '.') && !hasSep)
            {
                hasSep = true;
                sepIndex = i;
                // A dot still being typed (1 or 2 digits, or none, and nothing after them) stays a dot
                // until the number is settled, so typing "1.000" is not turned into "1,00" on the way.
                pendingDot = c == '.' && IsPendingDecimalDot(raw, i);
            }
        }

        var intDigits = intRaw.ToString().TrimStart('0');
        if (intDigits.Length > maxIntegerDigits) intDigits = intDigits[..maxIntegerDigits];
        if (intDigits.Length == 0 && !hasSep && intRaw.Length > 0) intDigits = "0";

        var grouped = Group(intDigits);
        var sep = pendingDot ? '.' : decimalSeparator;
        var text = hasSep ? $"{grouped}{sep}{dec}" : grouped;

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

    /// <summary>
    /// <see cref="Normalize"/>, but a minus sign is kept: if <paramref name="raw"/> has any minus
    /// (see <see cref="ContainsMinus"/>), anywhere and any number of times, the result starts with exactly one
    /// "-" (U+002D) followed by the normalised digits ("5-0" -> "-50", "--5" and "−5" -> "-5", "-" -> "-").
    /// The caret is shifted past the sign, never placed before it. A box showing such text holds an invalid
    /// (negative) amount; <see cref="Parse"/> still ignores the sign, so callers must check
    /// <see cref="ContainsMinus"/> before using the number.
    /// </summary>
    public static (string Text, int Caret) NormalizeKeepingSign(string raw, int caret, char decimalSeparator,
        int maxIntegerDigits = MaxIntegerDigits)
    {
        var (text, pos) = Normalize(raw, caret, decimalSeparator, maxIntegerDigits);
        if (!ContainsMinus(raw)) return (text, pos);
        return ("-" + text, pos + 1);
    }

    /// <summary>Number of integer digits in <paramref name="text"/> (before the first decimal separator, leading zeros
    /// and thousands dots not counted): above <see cref="MaxIntegerDigits"/> the amount is too large (B1g).</summary>
    public static int IntegerDigitCount(string? text)
    {
        text ??= "";
        var count = 0;
        var leading = true;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c))
            {
                if (leading && c == '0') continue;
                leading = false;
                count++;
            }
            else if (IsThousandsDot(text, i)) continue;
            else if (c == ',' || c == '.') break;
        }
        return count;
    }

    /// <summary>True if the raw text has a minus sign: '-' (U+002D), U+2212 or U+2013.</summary>
    public static bool ContainsMinus(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        return raw.IndexOfAny(['-', '−', '–']) >= 0;
    }

    /// <summary>
    /// Box text once focus leaves it: a decimal dot still pending (see <see cref="Normalize"/>) becomes the
    /// culture's decimal separator. Other text is returned unchanged.
    /// </summary>
    public static string Settle(string text, char decimalSeparator)
    {
        text ??= "";
        var dot = text.LastIndexOf('.');
        if (dot < 0 || decimalSeparator == '.' || !IsPendingDecimalDot(text, dot)) return text;
        return string.Concat(text.AsSpan(0, dot), decimalSeparator.ToString(), text.AsSpan(dot + 1));
    }

    /// <summary>
    /// A '.' followed by exactly three digits is a thousands separator ("14.000" = 14000, "14.000.000" = 14000000),
    /// whatever the culture. A comma is always the decimal separator, and a '.' followed by 1 or 2 digits stays a decimal.
    /// </summary>
    private static bool IsThousandsDot(string s, int i)
    {
        if (s[i] != '.') return false;
        var run = 0;
        for (var j = i + 1; j < s.Length && char.IsAsciiDigit(s[j]); j++) run++;
        return run == 3;
    }

    /// <summary>A '.' with at most two digits after it and nothing else: a decimal still being typed.</summary>
    private static bool IsPendingDecimalDot(string s, int i)
    {
        var rest = s.AsSpan(i + 1);
        if (rest.Length > MaxDecimalDigits) return false;
        foreach (var c in rest)
            if (!char.IsAsciiDigit(c)) return false;
        return true;
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
        text ??= "";
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c)) (hasSep ? decDigits : intDigits).Append(c);
            else if (IsThousandsDot(text, i)) continue;
            else if ((c == ',' || c == '.') && !hasSep) hasSep = true;
        }
        if (intDigits.Length == 0 && decDigits.Length == 0) return double.NaN;
        var number = (intDigits.Length == 0 ? "0" : intDigits.ToString())
                     + (decDigits.Length > 0 ? "." + decDigits : "");
        return double.Parse(number, CultureInfo.InvariantCulture);
    }
}

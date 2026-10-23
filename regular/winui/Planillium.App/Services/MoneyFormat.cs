using System.Globalization;
using System.Text;

namespace Planillium.App.Services;

/// <summary>Pure text rules for the Settings → Income money boxes (B1, 2026-10-23): a thin space
/// between thousands as the user types, no sign, at most 9 integer and 2 decimal digits. Kept free
/// of WinUI so the same code can be linked into the test project.
/// Separator rule (user decision 2026-10-23): a comma is the decimal separator; a dot followed by
/// exactly three digits is a thousands separator ("14.000" = 14 000); a dot followed by one or two
/// digits is a decimal ("14.5").</summary>
public static class MoneyFormat
{
    public const char Thin = ' ';
    private const int MaxInt = 9, MaxDec = 2;

    /// <summary>Result of reading raw text: the digits, the decimal digits (null = no decimal part)
    /// and whether the decimal separator was typed as a dot that may still become thousands.</summary>
    private readonly record struct Parsed(string Int, string? Dec, bool PendingDot);

    private static Parsed Read(string raw)
    {
        var i = new StringBuilder();
        string? dec = null;
        var pendingDot = false;
        for (var p = 0; p < raw.Length; p++)
        {
            var c = raw[p];
            if (char.IsAsciiDigit(c))
            {
                if (dec is null) { if (i.Length < MaxInt) i.Append(c); }
                else if (dec.Length < MaxDec) dec += c;
            }
            else if (c == ',' && dec is null) { dec = ""; pendingDot = false; }
            else if (c == '.' && dec is null)
            {
                var n = 0;
                while (p + 1 + n < raw.Length && char.IsAsciiDigit(raw[p + 1 + n])) n++;
                if (n == 3) continue;                    // dot + exactly 3 digits: thousands
                dec = ""; pendingDot = true;
            }
        }
        return new Parsed(i.ToString(), dec, pendingDot);
    }

    private static string GroupInt(string digits)
    {
        var s = digits.TrimStart('0');
        if (s.Length == 0) s = digits.Length > 0 ? "0" : "";
        if (s.Length <= 3) return s;
        var sb = new StringBuilder();
        for (var k = 0; k < s.Length; k++)
        {
            if (k > 0 && (s.Length - k) % 3 == 0) sb.Append(Thin);
            sb.Append(s[k]);
        }
        return sb.ToString();
    }

    /// <summary>Re-formats what the user typed. <paramref name="caret"/> is the caret index in
    /// <paramref name="raw"/>; the returned caret sits after the same number of digits.
    /// A decimal typed as a dot stays a dot until <see cref="Settle"/> (focus loss), because "1.00"
    /// may still turn into "1.000" with the next keystroke.</summary>
    public static string Normalize(string raw, int caret, CultureInfo culture, out int newCaret)
    {
        var p = Read(raw);
        var text = GroupInt(p.Int);
        if (p.Dec is not null)
        {
            if (text.Length == 0) text = "0";
            text += (p.PendingDot ? "." : culture.NumberFormat.NumberDecimalSeparator) + p.Dec;
        }
        var left = 0;
        for (var k = 0; k < Math.Min(caret, raw.Length); k++) if (char.IsAsciiDigit(raw[k])) left++;
        newCaret = text.Length;
        if (caret < raw.Length)
        {
            var seen = 0;
            newCaret = 0;
            while (newCaret < text.Length && seen < left)
            {
                if (char.IsAsciiDigit(text[newCaret])) seen++;
                newCaret++;
            }
        }
        return text;
    }

    /// <summary>Focus-loss form: a decimal dot becomes the culture's separator.</summary>
    public static string Settle(string text, CultureInfo culture) =>
        Normalize(text, text.Length, culture, out _).Replace(".", culture.NumberFormat.NumberDecimalSeparator);

    /// <summary>The number the text stands for; NaN when empty.</summary>
    public static double Parse(string text)
    {
        var p = Read(text);
        if (p.Int.Length == 0 && string.IsNullOrEmpty(p.Dec)) return double.NaN;
        var s = (p.Int.Length == 0 ? "0" : p.Int) + (string.IsNullOrEmpty(p.Dec) ? "" : "." + p.Dec);
        return double.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>Grouped display text for a stored amount (NaN → empty).</summary>
    public static string FromNumber(double v, CultureInfo culture)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "";
        var r = Math.Round(Math.Abs(v), MaxDec);
        var whole = Math.Floor(r);
        var cents = (int)Math.Round((r - whole) * 100);
        var text = GroupInt(((long)whole).ToString(CultureInfo.InvariantCulture));
        if (cents > 0)
            text += culture.NumberFormat.NumberDecimalSeparator + cents.ToString("00", CultureInfo.InvariantCulture).TrimEnd('0');
        return text;
    }
}

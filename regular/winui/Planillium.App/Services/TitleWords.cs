using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Planillium.App.Services;

/// <summary>
/// G9b: turns a window title into the set of *alike-comparable* words a learned rule can match.
/// "Quotation_Acme.xlsx", "Quotations 2026" and "quotation beta" must all give "quotation":
/// case, accents, plural, digits, dates and file extensions never count, and neither does the
/// app's own name (it is already the rule's other condition). Whole words only, so a rule word
/// "quote" never matches "quotefree". Pure functions, no I/O — unit-tested.
/// </summary>
internal static class TitleWords
{
    /// <summary>Shortest word kept. Shorter tokens ("of", "a", "v2" remnants) carry no meaning.</summary>
    internal const int MinLength = 3;

    // ".xlsx", ".docx", ".tar" ... directly after a name character. Removed before splitting so the
    // extension never becomes a "word" of its own.
    private static readonly Regex Extension = new(@"(?<=[\p{L}\p{N}_\)\]])\.[a-z][a-z0-9]{1,4}(?![\p{L}\p{N}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The normalised word set of <paramref name="title"/>, minus the words of
    /// <paramref name="app"/> (pass "" to keep them).</summary>
    internal static HashSet<string> Normalize(string? title, string? app = null)
    {
        var words = Split(title);
        if (!string.IsNullOrWhiteSpace(app)) words.ExceptWith(Split(app));
        return words;
    }

    /// <summary>Normalises one typed rule word the same way titles are, so a hand-edited word
    /// ("Quotations") compares equal to what the matcher sees. Empty when nothing usable is left.</summary>
    internal static string NormalizeWord(string? word)
    {
        var w = Split(word);
        return w.Count == 0 ? "" : w.First();
    }

    private static HashSet<string> Split(string? text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return result;

        var t = StripAccents(text.ToLowerInvariant());
        t = Extension.Replace(t, " ");

        var sb = new StringBuilder();
        void Flush()
        {
            if (sb.Length == 0) return;
            var word = Stem(sb.ToString());
            sb.Clear();
            if (word.Length >= MinLength) result.Add(word);
        }
        // Letters only: digits, underscores, dashes, dots and every other character split words,
        // which is what removes numbers and dates ("2026-11-04", "v2") without special cases.
        foreach (var ch in t)
        {
            if (char.IsLetter(ch)) sb.Append(ch);
            else Flush();
        }
        Flush();
        return result;
    }

    private static string StripAccents(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Deliberately crude English plural folding — enough for "quotations"/"quotation",
    /// "companies"/"company", "boxes"/"box". Synonyms and other languages are out of scope
    /// (acceptance: not in this feature).</summary>
    internal static string Stem(string w)
    {
        if (w.Length <= 3) return w;
        if (w.EndsWith("ies", StringComparison.Ordinal) && w.Length > 4) return w[..^3] + "y";
        if (w.EndsWith("sses", StringComparison.Ordinal) || w.EndsWith("xes", StringComparison.Ordinal)
            || w.EndsWith("zes", StringComparison.Ordinal) || w.EndsWith("ches", StringComparison.Ordinal)
            || w.EndsWith("shes", StringComparison.Ordinal)) return w[..^2];
        if (w.EndsWith("ss", StringComparison.Ordinal) || w.EndsWith("us", StringComparison.Ordinal)
            || w.EndsWith("is", StringComparison.Ordinal)) return w;
        if (w.EndsWith('s')) return w[..^1];
        return w;
    }
}

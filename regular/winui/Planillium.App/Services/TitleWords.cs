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

    /// <summary>True when a rule word matches one of the title's words: equal, or the title word starts with
    /// it - so the rule word "акт" also fires on "акты" and "актов" (user 2026-11-12: word forms count as one).</summary>
    internal static bool Covers(string ruleWord, IReadOnlySet<string> titleWords)
    {
        if (titleWords.Contains(ruleWord)) return true;
        foreach (var t in titleWords)
            if (t.StartsWith(ruleWord, StringComparison.Ordinal)) return true;
        // Russian and Armenian change letters near the end of a root (рынок -> рынка, fleeting vowels,
        // alternations), so there the rule word may be up to 2 letters longer than the root it shares with
        // the title word (min 3 shared): "акты" and "актов" both cover "акт", "рынок" covers "рынка".
        // English keeps its roots whole (plurals are folded by Stem), so it stays a pure prefix match -
        // otherwise "report" would fire on "repository".
        if (ruleWord.Length > 0 && ruleWord[0] <= 'z') return false;
        var need = Math.Max(3, ruleWord.Length - 2);
        foreach (var t in titleWords)
        {
            var n = 0;
            while (n < ruleWord.Length && n < t.Length && ruleWord[n] == t[n]) n++;
            if (n >= need) return true;
        }
        return false;
    }

    /// <summary>Normalises one typed rule word the same way titles are, so a hand-edited word
    /// ("Quotations") compares equal to what the matcher sees. Empty when nothing usable is left.</summary>
    internal static string NormalizeWord(string? word)
    {
        var w = Split(word);
        return w.Count == 0 ? "" : w.First();
    }

    // Junk words (user 2026-11-11): never proposed as rule words and refused when typed. Fillers and
    // generic brand/file-type words say nothing about what the work is. Digits and dates never survive
    // Split, so they need no entry here.
    private static readonly HashSet<string> Junk = new(StringComparer.Ordinal)
    {
        // English fillers
        "and", "the", "for", "with", "from", "into", "onto", "that", "this", "are", "was", "not", "you", "your",
        "all", "has", "have", "can", "but", "out", "off", "new", "untitled",
        // Russian fillers
        "для", "как", "что", "это", "при", "или", "над", "под", "без", "про", "его", "ней", "они", "все", "так",
        // generic app / vendor names
        "google", "chrome", "chromium", "edge", "firefox", "mozilla", "microsoft", "windows", "office",
        "word", "excel", "powerpoint", "outlook", "explorer", "browser",
        // file types
        "docx", "doc", "xlsx", "xls", "xlsm", "csv", "pdf", "pptx", "ppt", "txt", "rtf", "zip", "rar", "png", "jpg",
        "jpeg", "gif", "mp3", "mp4", "mpp", "exe", "msi", "html", "htm",
    };

    /// <summary>True when <paramref name="word"/> (already normalised) is filler, a generic app/vendor or
    /// file-type word, or part of the app's own name.</summary>
    internal static bool IsJunk(string word, string? app = null) =>
        Junk.Contains(word) || (!string.IsNullOrWhiteSpace(app) && Split(app).Contains(word));

    /// <summary>Reads typed rule words: separated by commas, semicolons or new lines (a space inside an
    /// entry is tolerated and just yields several words). Returns the accepted normalised words and the
    /// entries refused as unusable (digits, too short) or junk.</summary>
    internal static (List<string> Words, List<string> Refused) ParseTyped(string? text, string? app = null)
    {
        var words = new List<string>();
        var refused = new List<string>();
        foreach (var raw in (text ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = raw.Trim();
            if (entry.Length == 0) continue;
            var parts = Split(entry);
            if (parts.Count == 0) { refused.Add(entry); continue; }
            foreach (var w in parts)
            {
                if (IsJunk(w, app)) { refused.Add(w); continue; }
                if (!words.Contains(w)) words.Add(w);
            }
        }
        return (words, refused);
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

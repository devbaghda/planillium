namespace Planillium.App.Services;

/// <summary>Wilson 95% interval for "k of n examples agree" (DECISIONS 19c). Pure maths.</summary>
internal static class Wilson
{
    internal const double Z = 1.96;
    /// <summary>Act-alone bar (user, 2026-11-04): at least this many examples ...</summary>
    internal const int MinExamples = 10;
    /// <summary>... AND a lower bound at or above this.</summary>
    internal const double MinLowerBound = 0.85;

    internal static (double Lower, double Upper) Range(int k, int n)
    {
        if (n <= 0) return (0, 0);
        double p = (double)k / n, z2 = Z * Z;
        var denom = 1 + z2 / n;
        var centre = (p + z2 / (2 * n)) / denom;
        var half = Z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / denom;
        return (Math.Max(0, centre - half), Math.Min(1, centre + half));
    }

    /// <summary>True when a rule may act alone: n examples in its category out of total matches.</summary>
    internal static bool IsConfident(int examples, int total) =>
        examples >= MinExamples && Range(examples, total).Lower >= MinLowerBound;
}

/// <summary>One saved combination rule: an app plus one or more normalised title words. G9c:
/// <paramref name="Tag"/> (null = category-only rule) is a diary tag the rule also applies, e.g. a paid tag.</summary>
internal sealed record LearnedRule(long Id, string App, IReadOnlyList<string> Words, string Category, string? Tag = null);

/// <summary>One past Diary observation, live row or permanent-counts row: the app, the normalised
/// word set of its title, the category it ended up with, and how many identical rows it stands for.</summary>
internal sealed record HistoryRow(string App, IReadOnlySet<string> Words, string Category, int Count = 1, string? Tag = null);

/// <summary>How well a rule's words predict its category over the whole history.</summary>
internal readonly record struct RuleEvidence(int Examples, int Total, bool Stated = false)
{
    internal (double Lower, double Upper) Range => Wilson.Range(Examples, Total);
    /// <summary>An app-only rule (no words) is the user's own statement - "this app is always X" - so it
    /// acts without history to back it (user 2026-11-12).</summary>
    internal bool Confident => Stated || Wilson.IsConfident(Examples, Total);
}

internal static class RuleMatcher
{
    private static bool SameApp(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The rule that governs a title: the app matches and ANY one of the rule's words is a whole
    /// word of the title (OR, user 2026-11-11); the rule with the most of its words in the title wins,
    /// equal -> the one with a tag, then the older (lower id). Null when none match.
    /// Whether the winner may act or only suggest is the caller's call (<see cref="RuleEvidence"/>).</summary>
    internal static LearnedRule? Match(IEnumerable<LearnedRule> rules, string app, string? title)
    {
        var words = TitleWords.Normalize(title, app);
        LearnedRule? best = null;
        var bestHits = 0;
        foreach (var r in rules)
        {
            if (!SameApp(r.App, app)) continue;
            // An app-only rule (no words) matches every title of its app, below any rule that has a word hit.
            var hits = r.Words.Count == 0 ? 0 : r.Words.Count(w => TitleWords.Covers(w, words));
            if (hits == 0 && r.Words.Count > 0) continue;
            // Equal hits: a rule carrying a tag is the more specific one (G9c), else the older wins -
            // otherwise a tag rule added after a category-only rule on the same words would never apply.
            if (best == null || hits > bestHits
                || (hits == bestHits
                    && ((r.Tag != null && best.Tag == null) || ((r.Tag == null) == (best.Tag == null) && r.Id < best.Id))))
            {
                best = r;
                bestHits = hits;
            }
        }
        return best;
    }

    private static bool SameTag(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>Counts over <paramref name="history"/>: rows of the rule's app containing any of its
    /// words (Total) and, of those, rows in the rule's category (Examples).</summary>
    internal static RuleEvidence Evidence(LearnedRule rule, IEnumerable<HistoryRow> history)
    {
        int ex = 0, tot = 0;
        foreach (var h in history)
        {
            if (!SameApp(h.App, rule.App) || (rule.Words.Count > 0 && !rule.Words.Any(w => TitleWords.Covers(w, h.Words)))) continue;
            tot += h.Count;
            if (h.Category == rule.Category && (rule.Tag == null || SameTag(h.Tag, rule.Tag))) ex += h.Count;
        }
        return new RuleEvidence(ex, tot, Stated: rule.Words.Count == 0);
    }
}

/// <summary>A proposed rule shown after Bulk Mark, before the user confirms.</summary>
internal sealed record RuleProposal(string App, IReadOnlyList<string> Words, string Category, RuleEvidence Evidence, string? Tag = null);

internal static class RuleProposer
{
    /// <summary>Most words a proposed rule carries.</summary>
    internal const int MaxWords = 5;

    /// <summary>Proposal for one app group of a Bulk Mark. <paramref name="marked"/> are the title
    /// word sets just marked <paramref name="category"/>; <paramref name="history"/> is every
    /// categorised row ever (live + counts table, the marked rows included). Picks the word shared
    /// by most marked rows and purest among the app's rows (the shared-word method); adds further
    /// OR words only to cover marked rows the first missed, while the rule stays pure. Null when there is no usable word
    /// (never an empty-word rule, A6), nothing was marked, or the category is idle.</summary>
    internal static RuleProposal? Propose(string app, string category, IReadOnlyList<IReadOnlySet<string>> marked,
        IReadOnlyList<HistoryRow> history, string? tag = null)
    {
        if (marked.Count == 0 || category == DiaryCategory.Idle || string.IsNullOrWhiteSpace(category)) return null;

        // Candidate words: every usable word of the marked rows. Junk words (fillers, brand names, file
        // extensions) are never proposed (user 2026-11-11: "Chrome + google", "Word + and").
        var shares = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in marked) foreach (var w in m) if (!TitleWords.IsJunk(w, app)) shares[w] = shares.GetValueOrDefault(w) + 1;
        var candidates = shares.Keys.ToList();
        if (candidates.Count == 0) return null;

        var appRows = history.Where(h => string.Equals(h.App, app, StringComparison.OrdinalIgnoreCase)).ToList();

        (double Precision, int Support) Score(IReadOnlyList<string> words)
        {
            int tot = 0, ex = 0;
            foreach (var h in appRows)
            {
                if (!words.Any(w => TitleWords.Covers(w, h.Words))) continue;
                tot += h.Count;
                if (h.Category == category && (tag == null || string.Equals(h.Tag ?? "", tag, StringComparison.OrdinalIgnoreCase))) ex += h.Count;
            }
            return (tot == 0 ? 0 : (double)ex / tot, ex);
        }

        // First word: shared by most marked rows, then purest, then most supported, then alphabetical (stable).
        var best = candidates
            .Select(w => (Word: w, Sh: shares[w], Sc: Score(new[] { w })))
            .OrderByDescending(x => x.Sh).ThenByDescending(x => x.Sc.Precision)
            .ThenByDescending(x => x.Sc.Support).ThenBy(x => x.Word, StringComparer.Ordinal)
            .First();

        // OR rule (user 2026-11-11): further words are added only while marked rows are still
        // uncovered, one word at a time (best new coverage first), and only if the rule stays pure -
        // the union may not fall clearly below the first word's purity (nor below 85%).
        var chosen = new List<string> { best.Word };
        var chosenScore = best.Sc;
        var floor = Math.Min(0.85, best.Sc.Precision);
        while (chosen.Count < MaxWords)
        {
            var uncovered = marked.Where(m => !chosen.Any(c => TitleWords.Covers(c, m))).ToList();
            if (uncovered.Count == 0) break;
            var next = candidates.Where(w => !chosen.Contains(w) && uncovered.Any(m => TitleWords.Covers(w, m)))
                .Select(w => (Word: w, Cov: uncovered.Count(m => TitleWords.Covers(w, m)), Sc: Score(chosen.Append(w).ToArray())))
                .Where(x => x.Sc.Precision >= Math.Max(floor, chosenScore.Precision - 0.05))
                .OrderByDescending(x => x.Cov).ThenByDescending(x => x.Sc.Precision)
                .ThenBy(x => x.Word, StringComparer.Ordinal)
                .FirstOrDefault();
            if (next.Word == null) break;
            chosen.Add(next.Word);
            chosenScore = next.Sc;
        }
        chosen.Sort(StringComparer.Ordinal);

        var rule = new LearnedRule(0, app, (IReadOnlyList<string>)chosen, category, tag);
        return new RuleProposal(app, chosen, category, RuleMatcher.Evidence(rule, history), tag);
    }
}

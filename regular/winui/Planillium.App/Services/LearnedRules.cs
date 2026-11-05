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
internal readonly record struct RuleEvidence(int Examples, int Total)
{
    internal (double Lower, double Upper) Range => Wilson.Range(Examples, Total);
    internal bool Confident => Wilson.IsConfident(Examples, Total);
}

internal static class RuleMatcher
{
    private static bool SameApp(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The rule that governs a title: app AND all words must match (whole words); the rule
    /// with the most words wins, equal -> the one with a tag, then the older (lower id). Null when none match.
    /// Whether the winner may act or only suggest is the caller's call (<see cref="RuleEvidence"/>).</summary>
    internal static LearnedRule? Match(IEnumerable<LearnedRule> rules, string app, string? title)
    {
        var words = TitleWords.Normalize(title, app);
        LearnedRule? best = null;
        foreach (var r in rules)
        {
            if (r.Words.Count == 0 || !SameApp(r.App, app)) continue;
            if (!r.Words.All(words.Contains)) continue;
            // Equal words: a rule carrying a tag is the more specific one (G9c), else the older wins -
            // otherwise a tag rule added after a category-only rule on the same words would never apply.
            if (best == null || r.Words.Count > best.Words.Count
                || (r.Words.Count == best.Words.Count
                    && ((r.Tag != null && best.Tag == null) || ((r.Tag == null) == (best.Tag == null) && r.Id < best.Id)))) best = r;
        }
        return best;
    }

    private static bool SameTag(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>Counts over <paramref name="history"/>: rows of the rule's app containing all its
    /// words (Total) and, of those, rows in the rule's category (Examples).</summary>
    internal static RuleEvidence Evidence(LearnedRule rule, IEnumerable<HistoryRow> history)
    {
        int ex = 0, tot = 0;
        foreach (var h in history)
        {
            if (!SameApp(h.App, rule.App) || !rule.Words.All(h.Words.Contains)) continue;
            tot += h.Count;
            if (h.Category == rule.Category && (rule.Tag == null || SameTag(h.Tag, rule.Tag))) ex += h.Count;
        }
        return new RuleEvidence(ex, tot);
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
    /// by most marked rows and rarest among the app's other-category rows (the shared-word method);
    /// adds a second word only if it makes the rule clearly purer. Null when there is no usable word
    /// (never an empty-word rule, A6), nothing was marked, or the category is idle.</summary>
    internal static RuleProposal? Propose(string app, string category, IReadOnlyList<IReadOnlySet<string>> marked,
        IReadOnlyList<HistoryRow> history, string? tag = null)
    {
        if (marked.Count == 0 || category == DiaryCategory.Idle || string.IsNullOrWhiteSpace(category)) return null;

        // Candidate words: present in at least half of the marked rows (always at least one row).
        var minShare = Math.Max(1, (marked.Count + 1) / 2);
        var shares = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in marked) foreach (var w in m) shares[w] = shares.GetValueOrDefault(w) + 1;
        var candidates = shares.Where(kv => kv.Value >= minShare).Select(kv => kv.Key).ToList();
        if (candidates.Count == 0) return null;

        var appRows = history.Where(h => string.Equals(h.App, app, StringComparison.OrdinalIgnoreCase)).ToList();

        (double Precision, int Support) Score(IReadOnlyList<string> words)
        {
            int tot = 0, ex = 0;
            foreach (var h in appRows)
            {
                if (!words.All(h.Words.Contains)) continue;
                tot += h.Count;
                if (h.Category == category && (tag == null || string.Equals(h.Tag ?? "", tag, StringComparison.OrdinalIgnoreCase))) ex += h.Count;
            }
            return (tot == 0 ? 0 : (double)ex / tot, ex);
        }
        int Share(IReadOnlyList<string> words) => marked.Count(m => words.All(m.Contains));

        // Best single word: most shared, then purest, then most supported, then alphabetical (stable).
        var best = candidates
            .Select(w => (Words: (IReadOnlyList<string>)new[] { w }, Sh: shares[w], Sc: Score(new[] { w })))
            .OrderByDescending(x => x.Sh).ThenByDescending(x => x.Sc.Precision)
            .ThenByDescending(x => x.Sc.Support).ThenBy(x => x.Words[0], StringComparer.Ordinal)
            .First();

        var chosen = best.Words;
        var chosenScore = best.Sc;

        // A second word only when the first is not already pure and the pair keeps the rule shared
        // by every row that the first word was shared by, yet clearly purer.
        // 2026-11-05 (user: "not only 2 parameters"): keeps adding words, one at a time, up to MaxWords,
        // while the rule is not yet pure and the extra word clearly improves it.
        while (chosenScore.Precision < 0.95 && chosen.Count < MaxWords)
        {
            var current = chosen;
            var next = candidates.Where(w => !current.Contains(w))
                .Select(w => (IReadOnlyList<string>)current.Append(w).OrderBy(x => x, StringComparer.Ordinal).ToArray())
                .Select(ws => (Words: ws, Sh: Share(ws), Sc: Score(ws)))
                .Where(x => x.Sh == best.Sh && x.Sc.Precision >= chosenScore.Precision + 0.05)
                .OrderByDescending(x => x.Sc.Precision).ThenByDescending(x => x.Sc.Support)
                .ThenBy(x => string.Join(' ', x.Words), StringComparer.Ordinal)
                .FirstOrDefault();
            if (next.Words == null) break;
            chosen = next.Words;
            chosenScore = next.Sc;
        }

        var rule = new LearnedRule(0, app, chosen, category, tag);
        return new RuleProposal(app, chosen, category, RuleMatcher.Evidence(rule, history), tag);
    }
}

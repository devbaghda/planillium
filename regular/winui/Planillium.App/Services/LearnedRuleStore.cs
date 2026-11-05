using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>G9b storage: learned_rules CRUD, the permanent rule_word_counts table, and the
/// history (live Diary rows + counts) that rule evidence is measured against.</summary>
internal static class LearnedRuleStore
{
    private const char Sep = ' ';

    internal static string JoinWords(IEnumerable<string> words) => string.Join(Sep, words.OrderBy(w => w, StringComparer.Ordinal));
    internal static IReadOnlyList<string> SplitWords(string key) =>
        key.Split(Sep, StringSplitOptions.RemoveEmptyEntries);

    internal static List<LearnedRule> LoadRules(Database db)
    {
        var list = new List<LearnedRule>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id, app, words, category, tag FROM learned_rules ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new LearnedRule(r.GetInt64(0), r.GetString(1), SplitWords(r.GetString(2)), r.GetString(3),
                r.IsDBNull(4) || r.GetString(4).Length == 0 ? null : r.GetString(4)));
        return list;
    }

    internal static LearnedRule? GetRule(Database db, long id) => LoadRules(db).FirstOrDefault(x => x.Id == id);

    /// <summary>Adds a rule (words normalised, sorted). Returns the new id; the existing id when the
    /// same app+words+category rule is already saved; 0 when no usable word is left.</summary>
    internal static long AddRule(Database db, string app, IEnumerable<string> words, string category, string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(tag)) tag = null;
        var norm = words.Select(w => TitleWords.NormalizeWord(w)).Where(w => w.Length > 0).Distinct().ToList();
        if (norm.Count == 0 || string.IsNullOrWhiteSpace(app)) return 0;
        var key = JoinWords(norm);
        foreach (var e in LoadRules(db))
            if (string.Equals(e.App, app, StringComparison.OrdinalIgnoreCase) && JoinWords(e.Words) == key && e.Category == category
                && string.Equals(e.Tag ?? "", tag ?? "", StringComparison.OrdinalIgnoreCase))
                return e.Id;
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO learned_rules (app, words, category, created, tag) VALUES ($a,$w,$c,$t,$g); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$g", (object?)tag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$a", app);
        cmd.Parameters.AddWithValue("$w", key);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    internal static void DeleteRule(Database db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM learned_rules WHERE id=$i";
        cmd.Parameters.AddWithValue("$i", id);
        cmd.ExecuteNonQuery();
    }

    private static Dictionary<(string App, string Key, string Cat), int> GroupRows(SqliteCommand cmd)
    {
        var d = new Dictionary<(string App, string Key, string Cat), int>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var window = r.GetString(0);
            var cat = r.GetString(1);
            var n = r.GetInt32(2);
            var app = AppNames.Group(window);
            var key = JoinWords(TitleWords.Normalize(window, app));
            if (key.Length == 0) continue;           // nothing usable in the title: no rule could ever use it
            var k = (app, key, cat);
            d[k] = d.GetValueOrDefault(k) + n;
        }
        return d;
    }

    /// <summary>Tag-aware sibling of <see cref="GroupRows"/>: key includes the diary tag ('' = none).</summary>
    private static Dictionary<(string App, string Key, string Cat, string Tag), int> GroupRowsTagged(SqliteCommand cmd)
    {
        var d = new Dictionary<(string App, string Key, string Cat, string Tag), int>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var window = r.GetString(0);
            var app = AppNames.Group(window);
            var key = JoinWords(TitleWords.Normalize(window, app));
            if (key.Length == 0) continue;
            var k = (app, key, r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2).ToLowerInvariant());
            d[k] = d.GetValueOrDefault(k) + r.GetInt32(3);
        }
        return d;
    }

    /// <summary>Adds the rows older than <paramref name="cutoff"/> (about to be pruned) to the permanent
    /// counts table. Called inside PruneAndRollupDiary's transaction, right before the delete.</summary>
    internal static void TallyBeforePrune(Database db, string cutoff)
    {
        Dictionary<(string App, string Key, string Cat), int> groups;
        using (var sel = db.CreateCommand())
        {
            sel.CommandText = "SELECT window, category, COUNT(*) FROM time_diary " +
                              "WHERE date < $cutoff AND category <> $idle AND window <> 'idle' GROUP BY window, category";
            sel.Parameters.AddWithValue("$cutoff", cutoff);
            sel.Parameters.AddWithValue("$idle", DiaryCategory.Idle);
            groups = GroupRows(sel);
        }
        // G9c: the same rows again, with their diary tag, for tag rules.
        using (var sel2 = db.CreateCommand())
        {
            sel2.CommandText = "SELECT window, category, tag, COUNT(*) FROM time_diary " +
                               "WHERE date < $cutoff AND category <> $idle AND window <> 'idle' GROUP BY window, category, tag";
            sel2.Parameters.AddWithValue("$cutoff", cutoff);
            sel2.Parameters.AddWithValue("$idle", DiaryCategory.Idle);
            foreach (var (k, n) in GroupRowsTagged(sel2))
            {
                using var up = db.CreateCommand();
                up.CommandText = "INSERT INTO rule_tag_counts (app, word_key, category, tag, n) VALUES ($a,$k,$c,$g,$n) " +
                                 "ON CONFLICT(app, word_key, category, tag) DO UPDATE SET n = n + excluded.n";
                up.Parameters.AddWithValue("$a", k.App);
                up.Parameters.AddWithValue("$k", k.Key);
                up.Parameters.AddWithValue("$c", k.Cat);
                up.Parameters.AddWithValue("$g", k.Tag);
                up.Parameters.AddWithValue("$n", n);
                up.ExecuteNonQuery();
            }
        }
        foreach (var (k, n) in groups)
        {
            using var up = db.CreateCommand();
            up.CommandText = "INSERT INTO rule_word_counts (app, word_key, category, n) VALUES ($a,$k,$c,$n) " +
                             "ON CONFLICT(app, word_key, category) DO UPDATE SET n = n + excluded.n";
            up.Parameters.AddWithValue("$a", k.App);
            up.Parameters.AddWithValue("$k", k.Key);
            up.Parameters.AddWithValue("$c", k.Cat);
            up.Parameters.AddWithValue("$n", n);
            up.ExecuteNonQuery();
        }
    }

    /// <summary>Every categorised observation ever: live Diary rows (not idle) + the permanent counts.</summary>
    internal static List<HistoryRow> LoadHistory(Database db)
    {
        var list = new List<HistoryRow>();
        using (var live = db.CreateCommand())
        {
            live.CommandText = "SELECT window, category, COUNT(*) FROM time_diary " +
                               "WHERE category <> $idle AND window <> 'idle' GROUP BY window, category";
            live.Parameters.AddWithValue("$idle", DiaryCategory.Idle);
            foreach (var (k, n) in GroupRows(live))
                list.Add(new HistoryRow(k.App, new HashSet<string>(SplitWords(k.Key)), k.Cat, n));
        }
        using var cnt = db.CreateCommand();
        cnt.CommandText = "SELECT app, word_key, category, n FROM rule_word_counts";
        using var r = cnt.ExecuteReader();
        while (r.Read())
            list.Add(new HistoryRow(r.GetString(0), new HashSet<string>(SplitWords(r.GetString(1))), r.GetString(2), r.GetInt32(3)));
        return list;
    }
}

/// <summary>Tag-aware history for tag rules: live Diary rows with their tag + rule_tag_counts.</summary>
internal static class LearnedRuleTagHistory
{
    internal static List<HistoryRow> Load(Database db)
    {
        var list = new List<HistoryRow>();
        using (var live = db.CreateCommand())
        {
            live.CommandText = "SELECT window, category, tag, COUNT(*) FROM time_diary " +
                               "WHERE category <> $idle AND window <> 'idle' GROUP BY window, category, tag";
            live.Parameters.AddWithValue("$idle", DiaryCategory.Idle);
            using var r = live.ExecuteReader();
            var acc = new Dictionary<(string, string, string, string), int>();
            while (r.Read())
            {
                var window = r.GetString(0);
                var app = AppNames.Group(window);
                var key = LearnedRuleStore.JoinWords(TitleWords.Normalize(window, app));
                if (key.Length == 0) continue;
                var k = (app, key, r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2).ToLowerInvariant());
                acc[k] = acc.GetValueOrDefault(k) + r.GetInt32(3);
            }
            foreach (var (k, n) in acc)
                list.Add(new HistoryRow(k.Item1, new HashSet<string>(LearnedRuleStore.SplitWords(k.Item2)), k.Item3, n, k.Item4));
        }
        using var cnt = db.CreateCommand();
        cnt.CommandText = "SELECT app, word_key, category, tag, n FROM rule_tag_counts";
        using var c = cnt.ExecuteReader();
        while (c.Read())
            list.Add(new HistoryRow(c.GetString(0), new HashSet<string>(LearnedRuleStore.SplitWords(c.GetString(1))),
                c.GetString(2), c.GetInt32(4), c.GetString(3)));
        return list;
    }
}

/// <summary>The rules plus the history they are measured against, loaded once and queried for each
/// window title. A rule acts alone only when its evidence clears the bar (<see cref="Wilson"/>).</summary>
internal sealed class RuleBook
{
    internal sealed record Hit(LearnedRule Rule, RuleEvidence Evidence)
    {
        /// <summary>2026-11-05 (user): a saved rule always acts at once - the user confirmed it. The
        /// evidence is kept for display only; the old 10-example/85% bar no longer gates it.</summary>
        internal bool Confident => true;
        internal string State => RuleStates.Auto;
    }

    private readonly List<LearnedRule> _rules;
    private readonly Dictionary<long, RuleEvidence> _evidence = new();

    internal RuleBook(IEnumerable<LearnedRule> rules, IReadOnlyList<HistoryRow> history,
        IReadOnlyList<HistoryRow>? tagHistory = null)
    {
        _rules = rules.ToList();
        foreach (var r in _rules)
            _evidence[r.Id] = RuleMatcher.Evidence(r, r.Tag != null ? (tagHistory ?? Array.Empty<HistoryRow>()) : history);
    }

    internal static readonly RuleBook Empty = new(Array.Empty<LearnedRule>(), Array.Empty<HistoryRow>());
    internal IReadOnlyList<LearnedRule> Rules => _rules;
    internal RuleEvidence EvidenceFor(long id) => _evidence.GetValueOrDefault(id);

    /// <summary>The most specific rule matching app+title, with its evidence; null when none matches.</summary>
    internal Hit? Match(string app, string? title)
    {
        var r = RuleMatcher.Match(_rules, app, title);
        return r == null ? null : new Hit(r, EvidenceFor(r.Id));
    }

    internal static RuleBook Load(Database db)
    {
        var rules = LearnedRuleStore.LoadRules(db);
        return new(rules, LearnedRuleStore.LoadHistory(db),
            rules.Any(r => r.Tag != null) ? LearnedRuleTagHistory.Load(db) : null);
    }
}

/// <summary>Values of time_diary.rule_state (NULL = no learned rule involved).</summary>
internal static class RuleStates
{
    internal const string Auto = "auto";
    internal const string Suggested = "suggested";
    internal const string Manual = "manual";
}

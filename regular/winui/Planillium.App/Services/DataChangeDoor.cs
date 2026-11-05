using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>
/// The one door every bulk change to past data goes through (G3, 2026-10-28; DECISIONS rule 19):
/// <b>preview</b> (what would change) → <b>automatic backup</b> of progress.db (dated copy, the
/// last 5 kept) → <b>write</b>, storing each changed row's old value → <b>Undo</b>. The Diary's
/// bulk Mark is the first caller; anything that rewrites past rows later (learned rules) is meant
/// to call <see cref="Apply"/> too rather than writing on its own.
///
/// Undo restores a row only if it is exactly as this change left it (every column compared, via
/// the snapshot taken right after the write). A row edited, split, re-marked or deleted since is
/// skipped and counted, never overwritten — Undo must not clobber a later change.
///
/// A category mark may also teach activity keywords (<see cref="TeachKeywords"/>); the door
/// records each one it newly taught with the change, and Undo forgets exactly those (G3 round 2,
/// 2026-10-29) — never a keyword that was already in that list before the mark.
///
/// Writes nothing on the UI thread by design: the callers run Apply/Undo via Task.Run (a backup
/// of a large file takes a moment).
/// </summary>
public static class DataChangeDoor
{
    public const int BackupsKept = 5;
    public const int ChangesKept = 20;
    public const int ChangeDaysKept = 30;

    /// <summary>The past-data fields the door can change. Whitelisted (never a free column name)
    /// so the SQL below can never be pointed at something else.</summary>
    public enum Field { DiaryCategory, DiaryTag, LearnedRules, DiaryAdd }

    public sealed record Preview(int Filled, int Overwritten, int Unchanged, int Missing)
    {
        public int Changed => Filled + Overwritten;
    }

    public sealed record ApplyResult(long ChangeId, int Changed, string BackupFile, IReadOnlyCollection<DateOnly> Dates);

    /// <summary><c>KeywordsForgotten</c>: keywords this change taught that Undo removed again.
    /// <c>KeywordsKept</c>: ones left alone because the user moved or removed them since.
    /// <c>KeywordsFailed</c>: ones that could not be removed (config.json not writable; logged).</summary>
    public sealed record UndoResult(int Restored, int Skipped, IReadOnlyCollection<DateOnly> Dates,
        IReadOnlyList<string> KeywordsForgotten, int KeywordsKept, int KeywordsFailed,
        int RulesRemoved = 0, int RulesRestored = 0);

    public sealed record Change(long Id, DateTime At, string Label, Field Field, int RowCount,
        DateTime? UndoneAt, int Restored, int Skipped, int KeywordsTaught);

    public static string BackupDir => Path.Combine(AppPaths.Root, "data", "backups");

    private static string Column(Field f) => f switch
    {
        Field.DiaryCategory => "category",
        Field.DiaryTag => "tag",
        Field.LearnedRules => "learned_rules",   // rule-only change: no time_diary column is written
        Field.DiaryAdd => "row_added",            // G9c: a row added by hand; Undo deletes it again
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    private static Field FieldOf(string column) => column switch
    {
        "category" => Field.DiaryCategory,
        "tag" => Field.DiaryTag,
        "learned_rules" => Field.LearnedRules,
        "row_added" => Field.DiaryAdd,
        _ => throw new InvalidDataException($"Unknown change_log field '{column}'"),
    };

    /// <summary>Blank and missing count as the same "no value" — a row with category "" and a
    /// row with tag NULL are both "empty" for filled/overwritten/unchanged.</summary>
    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    // ── preview ──────────────────────────────────────────────────────────────

    /// <summary>What <see cref="Apply"/> would do to these rows right now, read from the
    /// database (not from whatever a page has on screen). Filled = had no value; Overwritten =
    /// had a different value; Unchanged = already this value; Missing = row no longer exists.</summary>
    public static Preview PreviewChange(IReadOnlyCollection<long> ids, Field field, string? newValue)
    {
        if (field is Field.LearnedRules or Field.DiaryAdd) throw new ArgumentException("No row preview for this kind of change.", nameof(field));
        using var db = new Database();
        var current = ReadRows(db, ids);
        int filled = 0, overwritten = 0, unchanged = 0;
        var target = Norm(newValue);
        foreach (var id in ids)
        {
            if (!current.TryGetValue(id, out var row)) continue;
            var old = Norm(ValueOf(row, field));
            if (old == target) unchanged++;
            else if (old is null) filled++;
            else overwritten++;
        }
        return new Preview(filled, overwritten, unchanged, ids.Count - current.Count);
    }

    // ── write ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Backs up the database, then sets <paramref name="field"/> to <paramref name="newValue"/>
    /// on every listed row whose value actually differs, recording each one's old value — all in
    /// one transaction. Throws (and writes nothing) if the backup fails. Category changes also
    /// re-score the days they touched, the same as the old bulk Mark did.
    /// </summary>
    public static ApplyResult Apply(string label, IReadOnlyCollection<long> ids, Field field, string? newValue)
    {
        if (field == Field.LearnedRules) throw new ArgumentException("Use DeleteRule for rule changes.", nameof(field));
        if (field == Field.DiaryAdd) throw new ArgumentException("Use AddEntry to add a row.", nameof(field));
        if (field == Field.DiaryCategory && Norm(newValue) is null)
            throw new ArgumentException("A diary category can't be set to empty.", nameof(newValue));
        var backup = MakeBackup();

        using var db = new Database();
        var target = Norm(newValue);
        var dates = new HashSet<DateOnly>();
        long changeId = 0;
        var changed = 0;
        db.RunInTransaction(() =>
        {
            var current = ReadRows(db, ids);
            var toChange = current.Values.Where(r => Norm(ValueOf(r, field)) != target).ToList();

            using (var log = db.CreateCommand())
            {
                log.CommandText =
                    "INSERT INTO change_log (ts, label, tbl, field, new_value, row_count) " +
                    "VALUES ($ts, $label, 'time_diary', $field, $new, $n); SELECT last_insert_rowid();";
                log.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
                log.Parameters.AddWithValue("$label", label);
                log.Parameters.AddWithValue("$field", Column(field));
                log.Parameters.AddWithValue("$new", (object?)target ?? DBNull.Value);
                log.Parameters.AddWithValue("$n", toChange.Count);
                changeId = Convert.ToInt64(log.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using var update = db.CreateCommand();
            // G9b: a category the user sets by hand is 'manual' - a learned rule never overrides it.
            update.CommandText = field == Field.DiaryCategory
                ? "UPDATE time_diary SET category=$v, rule_state='manual' WHERE id=$id"
                : $"UPDATE time_diary SET {Column(field)}=$v WHERE id=$id";
            var pv = update.Parameters.Add("$v", SqliteType.Text);
            var pid = update.Parameters.Add("$id", SqliteType.Integer);
            using var record = db.CreateCommand();
            record.CommandText =
                "INSERT INTO change_log_rows (change_id, row_id, old_value, after_sig, old_rule_state) " +
                "VALUES ($c, $r, $old, $sig, $ors)";
            record.Parameters.AddWithValue("$c", changeId);
            var pr = record.Parameters.Add("$r", SqliteType.Integer);
            var pold = record.Parameters.Add("$old", SqliteType.Text);
            var psig = record.Parameters.Add("$sig", SqliteType.Text);
            var pors = record.Parameters.Add("$ors", SqliteType.Text);

            // Category is NOT NULL in time_diary; only the tag may be stored as NULL.
            var stored = field == Field.DiaryCategory ? target! : (object?)target ?? DBNull.Value;
            foreach (var row in toChange)
            {
                pv.Value = stored;
                pid.Value = row.Id;
                update.ExecuteNonQuery();

                var after = field == Field.DiaryCategory
                    ? row with { Category = target!, RuleState = RuleStates.Manual } : row with { Tag = target };
                pr.Value = row.Id;
                pors.Value = (object?)row.RuleState ?? DBNull.Value;
                pold.Value = (object?)ValueOf(row, field) ?? DBNull.Value;
                psig.Value = Signature(after);
                record.ExecuteNonQuery();
                dates.Add(row.Date);
            }
            changed = toChange.Count;
            Prune(db);
        });

        if (field == Field.DiaryCategory && dates.Count > 0)
            ScoreService.TryRecalculateDayScores(db, dates, "DataChangeDoor.Apply.RecalculateScore");
        return new ApplyResult(changeId, changed, backup, dates);
    }

    // ── a row added by hand (G9c) ────────────────────────────────────────────

    /// <summary>Why an entry can't be saved, or null when it can. Pure: no database access.</summary>
    public static string? ValidateEntry(DateOnly date, TimeOnly start, TimeOnly end, string? category, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Choose a category.";
        if (end <= start) return "The end time must be after the start time.";
        if (date.ToDateTime(start) > now) return "That entry starts in the future.";
        if (date.ToDateTime(end) > now) return "That entry ends in the future.";
        return null;
    }

    /// <summary>The existing row on <paramref name="date"/> that overlaps start-end, as "HH:mm–HH:mm window",
    /// or null. Rows that only touch (09:00–10:00 then 10:00–11:00) don't overlap.</summary>
    public static string? FindOverlap(Database db, string date, string start, string end)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "SELECT start_time, end_time, window FROM time_diary " +
            "WHERE date=$d AND start_time < $e AND end_time > $s ORDER BY start_time LIMIT 1";
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$s", start);
        cmd.Parameters.AddWithValue("$e", end);
        using var r = cmd.ExecuteReader();
        return r.Read() ? $"{r.GetString(0)}–{r.GetString(1)} {r.GetString(2)}" : null;
    }

    /// <summary>
    /// Adds one Diary row by hand: validate → refuse any overlap (names the clashing row) → backup →
    /// insert with rule_state 'manual' and a change-log entry, in one transaction → re-score the day.
    /// Throws <see cref="InvalidOperationException"/> with a user-readable message (nothing written) when refused.
    /// </summary>
    public static ApplyResult AddEntry(DateOnly date, TimeOnly start, TimeOnly end, string category,
        string? tag, string? note, DateTime? nowOverride = null)
    {
        var bad = ValidateEntry(date, start, end, category, nowOverride ?? DateTime.Now);
        if (bad != null) throw new InvalidOperationException(bad);
        var dateText = date.ToIsoDate();
        var s = start.ToIsoTimeOfDay();
        var e = end.ToIsoTimeOfDay();
        using (var pre = new Database())
        {
            var clash = FindOverlap(pre, dateText, s, e);
            if (clash != null) throw new InvalidOperationException($"That overlaps an existing entry: {clash}. Nothing was saved.");
        }
        var backup = MakeBackup();

        using var db = new Database();
        long changeId = 0;
        db.RunInTransaction(() =>
        {
            var clash = FindOverlap(db, dateText, s, e);   // again inside the write: the poll may have added a row
            if (clash != null) throw new InvalidOperationException($"That overlaps an existing entry: {clash}. Nothing was saved.");
            var tagValue = Norm(tag);
            var noteValue = Norm(note);
            var duration = Math.Max(1, (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes);
            long rowId;
            using (var ins = db.CreateCommand())
            {
                ins.CommandText =
                    "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description, tag, rule_state) " +
                    "VALUES ($d,$s,$e,$m,$c,$w,$x,$t,'manual'); SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$d", dateText);
                ins.Parameters.AddWithValue("$s", s);
                ins.Parameters.AddWithValue("$e", e);
                ins.Parameters.AddWithValue("$m", duration);
                ins.Parameters.AddWithValue("$c", category);
                ins.Parameters.AddWithValue("$w", ManualWindow);
                ins.Parameters.AddWithValue("$x", (object?)noteValue ?? DBNull.Value);
                ins.Parameters.AddWithValue("$t", (object?)tagValue ?? DBNull.Value);
                rowId = Convert.ToInt64(ins.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            using (var log = db.CreateCommand())
            {
                log.CommandText =
                    "INSERT INTO change_log (ts, label, tbl, field, new_value, row_count) " +
                    "VALUES ($ts, $label, 'time_diary', 'row_added', NULL, 1); SELECT last_insert_rowid();";
                log.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
                log.Parameters.AddWithValue("$label", $"Added an entry {dateText} {s}–{e}");
                changeId = Convert.ToInt64(log.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            var after = new Row(rowId, date, dateText, s, e, duration, category, ManualWindow, noteValue, tagValue, RuleStates.Manual);
            using (var rec = db.CreateCommand())
            {
                rec.CommandText =
                    "INSERT INTO change_log_rows (change_id, row_id, old_value, after_sig, old_rule_state) " +
                    "VALUES ($c, $r, NULL, $sig, NULL)";
                rec.Parameters.AddWithValue("$c", changeId);
                rec.Parameters.AddWithValue("$r", rowId);
                rec.Parameters.AddWithValue("$sig", Signature(after));
                rec.ExecuteNonQuery();
            }
            Prune(db);
        });
        ScoreService.TryRecalculateDayScores(db, new[] { date }, "DataChangeDoor.AddEntry.RecalculateScore");
        return new ApplyResult(changeId, 1, backup, new[] { date });
    }

    /// <summary>The Window text stamped on rows added by hand.</summary>
    public const string ManualWindow = "Added by hand";

    // ── keywords taught by a change ──────────────────────────────────────────

    /// <summary>
    /// Teaches each keyword to activity_rules under <paramref name="category"/> (as
    /// <see cref="ConfigService.LearnActivityRule"/> does) on behalf of change
    /// <paramref name="changeId"/>, and records the ones it newly taught — with the category each
    /// one lived in before, if the mark moved it — so <see cref="Undo"/> can forget exactly those.
    /// A keyword already in that category's list is neither re-taught nor recorded: it existed
    /// before the mark, and Undo must leave it. Returns the newly taught keywords.
    /// </summary>
    public static List<string> TeachKeywords(long changeId, string category, IEnumerable<string> keywords)
    {
        var taught = new List<(string Keyword, string? Previous)>();
        foreach (var keyword in keywords.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var previous = ConfigService.ActivityRuleCategoryOf(keyword);
                if (previous == category) continue;
                if (ConfigService.LearnActivityRule(keyword, category)) taught.Add((keyword, previous));
            }
            catch (Exception ex) { Log.Error("DataChangeDoor.TeachKeywords", ex); }
        }
        if (taught.Count == 0) return new List<string>();

        try
        {
            using var db = new Database();
            db.RunInTransaction(() =>
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText =
                    "INSERT OR REPLACE INTO change_log_rules (change_id, keyword, category, previous_category) " +
                    "VALUES ($c, $k, $cat, $prev)";
                cmd.Parameters.AddWithValue("$c", changeId);
                cmd.Parameters.AddWithValue("$cat", category);
                var pk = cmd.Parameters.Add("$k", SqliteType.Text);
                var pprev = cmd.Parameters.Add("$prev", SqliteType.Text);
                foreach (var (keyword, previous) in taught)
                {
                    pk.Value = keyword;
                    pprev.Value = (object?)previous ?? DBNull.Value;
                    cmd.ExecuteNonQuery();
                }
            });
        }
        catch (Exception ex)
        {
            // Taught, but Undo won't know about them — logged; the message still lists them and
            // Settings ▸ Activity keywords can remove them by hand.
            Log.Error("DataChangeDoor.TeachKeywords.Record", ex);
        }
        return taught.Select(t => t.Keyword).ToList();
    }

    // ── undo ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores the old value of every row this change touched that is still exactly as the
    /// change left it; rows edited, split, re-marked or deleted since are skipped and counted.
    /// Backs up first (an Undo is itself a bulk change). A change can be undone once.
    /// Also forgets the activity keywords this change taught (<see cref="TeachKeywords"/>): each
    /// leaves the category it was taught to — and goes back to the one it had before, if the mark
    /// moved it — unless the user has changed that keyword since.
    /// </summary>
    public static UndoResult Undo(long changeId)
    {
        using (var check = new Database())
        {
            var c = LoadChange(check, changeId)
                ?? throw new InvalidOperationException("That change is no longer in the history.");
            if (c.UndoneAt is not null)
                throw new InvalidOperationException("That change was already undone.");
        }
        MakeBackup();

        using var db = new Database();
        var dates = new HashSet<DateOnly>();
        int restored = 0, skipped = 0, rulesRemoved = 0, rulesRestored = 0;
        Field field = Field.DiaryCategory;
        var rules = new List<(string Keyword, string Category, string? Previous)>();
        db.RunInTransaction(() =>
        {
            var change = LoadChange(db, changeId)
                ?? throw new InvalidOperationException("That change is no longer in the history.");
            if (change.UndoneAt is not null)
                throw new InvalidOperationException("That change was already undone.");
            field = change.Field;

            var saved = new List<(long RowId, string? Old, string Sig, string? OldRuleState)>();
            using (var read = db.CreateCommand())
            {
                read.CommandText = "SELECT row_id, old_value, after_sig, old_rule_state FROM change_log_rows WHERE change_id=$c";
                read.Parameters.AddWithValue("$c", changeId);
                using var r = read.ExecuteReader();
                while (r.Read())
                    saved.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3)));
            }

            var current = ReadRows(db, saved.Select(s => s.RowId).ToList());
            using var update = db.CreateCommand();
            // A category goes back together with the rule_state it had (G9b); a tag leaves it alone;
            // a row added by hand (G9c) is deleted again.
            update.CommandText = field == Field.DiaryAdd
                ? "DELETE FROM time_diary WHERE id=$id AND $v IS NULL"
                : field == Field.DiaryCategory
                ? "UPDATE time_diary SET category=$v, rule_state=$rs WHERE id=$id"
                : $"UPDATE time_diary SET {Column(field)}=$v WHERE id=$id";
            var pv = update.Parameters.Add("$v", SqliteType.Text);
            var pid = update.Parameters.Add("$id", SqliteType.Integer);
            var prs = field == Field.DiaryCategory ? update.Parameters.Add("$rs", SqliteType.Text) : null;
            foreach (var (rowId, old, sig, oldRuleState) in saved)
            {
                if (!current.TryGetValue(rowId, out var row) || Signature(row) != sig)
                {
                    skipped++;
                    continue;
                }
                // The old value goes back exactly as it was stored ("" stays "", NULL stays NULL)
                // — except a category, which the column never allows to be NULL.
                pv.Value = field == Field.DiaryCategory ? old ?? "" : (object?)old ?? DBNull.Value;
                if (prs != null) prs.Value = (object?)oldRuleState ?? DBNull.Value;
                pid.Value = rowId;
                update.ExecuteNonQuery();
                restored++;
                dates.Add(row.Date);
            }

            using (var readRules = db.CreateCommand())
            {
                readRules.CommandText =
                    "SELECT keyword, category, previous_category FROM change_log_rules WHERE change_id=$c";
                readRules.Parameters.AddWithValue("$c", changeId);
                using var rr = readRules.ExecuteReader();
                while (rr.Read())
                    rules.Add((rr.GetString(0), rr.GetString(1), rr.IsDBNull(2) ? null : rr.GetString(2)));
            }

            (rulesRemoved, rulesRestored) = UndoRuleOps(db, changeId);

            using var mark = db.CreateCommand();
            mark.CommandText = "UPDATE change_log SET undone_at=$ts, restored=$r, skipped=$s WHERE id=$c";
            mark.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
            mark.Parameters.AddWithValue("$r", restored);
            mark.Parameters.AddWithValue("$s", skipped);
            mark.Parameters.AddWithValue("$c", changeId);
            mark.ExecuteNonQuery();
        });

        if (field is Field.DiaryCategory or Field.DiaryAdd && dates.Count > 0)
            ScoreService.TryRecalculateDayScores(db, dates, "DataChangeDoor.Undo.RecalculateScore");

        // After the rows commit: config.json is a separate file, outside the transaction. The
        // keywords belong to the change, so they go even if some rows were skipped.
        var forgotten = new List<string>();
        int kept = 0, failed = 0;
        foreach (var (keyword, category, previous) in rules)
        {
            try
            {
                if (ConfigService.ForgetActivityRule(keyword, category, previous)) forgotten.Add(keyword);
                else kept++;
            }
            catch (Exception ex)
            {
                Log.Error("DataChangeDoor.Undo.ForgetKeyword", ex);
                failed++;
            }
        }
        return new UndoResult(restored, skipped, dates, forgotten, kept, failed, rulesRemoved, rulesRestored);
    }

    /// <summary>The one wording for an Undo's outcome — the Diary message and the Settings
    /// history both show it.</summary>
    public static string DescribeUndo(UndoResult r)
    {
        string Rows(int n) => $"{n.ToString("N0", CultureInfo.CurrentCulture)} {(n == 1 ? "row" : "rows")}";
        string Keywords(int n) => $"{n.ToString("N0", CultureInfo.CurrentCulture)} {(n == 1 ? "keyword" : "keywords")}";
        var text = r.Restored == 0 && r.Skipped == 0 && (r.RulesRemoved + r.RulesRestored) > 0
            ? ""
            : r.Skipped == 0
            ? $"Restored {Rows(r.Restored)} to their old values."
            : $"Restored {Rows(r.Restored)} to their old values. Skipped {Rows(r.Skipped)} changed since " +
              "(edited, split, re-marked or deleted) — they keep their newer values.";
        if (r.RulesRemoved > 0)
            text += $"{(text.Length > 0 ? " " : "")}Removed {r.RulesRemoved} learned {(r.RulesRemoved == 1 ? "rule" : "rules")} this change created.";
        if (r.RulesRestored > 0)
            text += $"{(text.Length > 0 ? " " : "")}Brought back {r.RulesRestored} deleted {(r.RulesRestored == 1 ? "rule" : "rules")}.";
        if (r.KeywordsForgotten.Count > 0)
            text += $" Forgot the {Keywords(r.KeywordsForgotten.Count)} this change taught: " +
                    string.Join(", ", r.KeywordsForgotten.Take(8)) +
                    (r.KeywordsForgotten.Count > 8 ? $" and {r.KeywordsForgotten.Count - 8} more" : "") + ".";
        if (r.KeywordsKept > 0)
            text += $" Kept {Keywords(r.KeywordsKept)} you changed since in Settings ▸ Activity keywords.";
        if (r.KeywordsFailed > 0)
            text += $" Couldn't remove {Keywords(r.KeywordsFailed)} — check Settings ▸ Activity keywords.";
        return text;
    }

    // ── learned rules (G9b) ──────────────────────────────────────────────────

    /// <summary>Records that change <paramref name="changeId"/> created these rules, so its Undo
    /// removes them again (acceptance E3). Called right after the Bulk Mark Apply that confirmed them.</summary>
    public static void RecordRulesAdded(long changeId, IEnumerable<long> ruleIds)
    {
        using var db = new Database();
        db.RunInTransaction(() =>
        {
            foreach (var id in ruleIds.Distinct())
            {
                var rule = LearnedRuleStore.GetRule(db, id);
                if (rule == null) continue;
                InsertRuleOp(db, changeId, "added", rule);
            }
        });
    }

    /// <summary>Deletes a learned rule through the door: backup first, a history entry, then the
    /// delete - Undo brings the rule back. Rows the rule had acted on keep their stored values (E2).
    /// Returns the change id, or 0 when the rule no longer exists.</summary>
    public static long DeleteRule(long ruleId)
    {
        using (var peek = new Database())
            if (LearnedRuleStore.GetRule(peek, ruleId) == null) return 0;
        MakeBackup();
        using var db = new Database();
        long changeId = 0;
        db.RunInTransaction(() =>
        {
            var rule = LearnedRuleStore.GetRule(db, ruleId);
            if (rule == null) return;
            using (var log = db.CreateCommand())
            {
                log.CommandText =
                    "INSERT INTO change_log (ts, label, tbl, field, new_value, row_count) " +
                    "VALUES ($ts, $label, 'learned_rules', 'learned_rules', NULL, 0); SELECT last_insert_rowid();";
                log.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
                log.Parameters.AddWithValue("$label", $"Deleted rule: {rule.App} + {string.Join(" ", rule.Words)}");
                changeId = Convert.ToInt64(log.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            InsertRuleOp(db, changeId, "deleted", rule);
            LearnedRuleStore.DeleteRule(db, ruleId);
            Prune(db);
        });
        return changeId;
    }

    /// <summary>2026-11-05 (user): edits a saved rule's words, category and tag (the app stays). One history
    /// entry holding a "deleted" op for the old rule and an "added" op for the new one, so Undo puts the old
    /// rule back and removes the new. Backup first. Returns the change id, 0 when the rule is gone. Throws
    /// ArgumentException (message is user-readable) for no usable word, a rule identical to the old one, or a
    /// duplicate of another saved rule - checked before anything is written.</summary>
    public static long EditRule(long ruleId, IEnumerable<string> words, string category, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) tag = null;
        var norm = words.Select(w => TitleWords.NormalizeWord(w)).Where(w => w.Length > 0).Distinct().ToList();
        if (norm.Count == 0) throw new ArgumentException("Type at least one word from the window title.");
        var key = LearnedRuleStore.JoinWords(norm);
        using (var peek = new Database())
        {
            var all = LearnedRuleStore.LoadRules(peek);
            var old = all.FirstOrDefault(x => x.Id == ruleId);
            if (old == null) return 0;
            bool Same(LearnedRule x) => string.Equals(x.App, old.App, StringComparison.OrdinalIgnoreCase)
                && LearnedRuleStore.JoinWords(x.Words) == key && x.Category == category
                && string.Equals(x.Tag ?? "", tag ?? "", StringComparison.OrdinalIgnoreCase);
            if (Same(old)) throw new ArgumentException("Nothing changed.");
            if (all.Any(x => x.Id != ruleId && Same(x)))
                throw new ArgumentException("Another saved rule already says exactly this.");
        }
        MakeBackup();
        using var db = new Database();
        long changeId = 0;
        db.RunInTransaction(() =>
        {
            var old = LearnedRuleStore.GetRule(db, ruleId);
            if (old == null) return;
            using (var log = db.CreateCommand())
            {
                log.CommandText =
                    "INSERT INTO change_log (ts, label, tbl, field, new_value, row_count) " +
                    "VALUES ($ts, $label, 'learned_rules', 'learned_rules', NULL, 0); SELECT last_insert_rowid();";
                log.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
                log.Parameters.AddWithValue("$label", $"Edited rule: {old.App} + {key}");
                changeId = Convert.ToInt64(log.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            InsertRuleOp(db, changeId, "deleted", old);
            LearnedRuleStore.DeleteRule(db, ruleId);
            var newId = LearnedRuleStore.AddRule(db, old.App, norm, category, tag);
            var made = LearnedRuleStore.GetRule(db, newId);
            if (made != null) InsertRuleOp(db, changeId, "added", made);
            Prune(db);
        });
        return changeId;
    }

    private static void InsertRuleOp(Database db, long changeId, string op, LearnedRule rule)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO change_log_rule_ops (change_id, op, rule_id, app, words, category, tag) " +
            "VALUES ($c, $op, $id, $app, $w, $cat, $tag)";
        cmd.Parameters.AddWithValue("$tag", (object?)rule.Tag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", changeId);
        cmd.Parameters.AddWithValue("$op", op);
        cmd.Parameters.AddWithValue("$id", rule.Id);
        cmd.Parameters.AddWithValue("$app", rule.App);
        cmd.Parameters.AddWithValue("$w", LearnedRuleStore.JoinWords(rule.Words));
        cmd.Parameters.AddWithValue("$cat", rule.Category);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Inside Undo's transaction: removes rules the change created, re-creates rules it
    /// deleted (a fresh id; the same app/words/category).</summary>
    private static (int Removed, int Restored) UndoRuleOps(Database db, long changeId)
    {
        var ops = new List<(string Op, long RuleId, string App, string Words, string Category, string? Tag)>();
        using (var read = db.CreateCommand())
        {
            read.CommandText = "SELECT op, rule_id, app, words, category, tag FROM change_log_rule_ops WHERE change_id=$c";
            read.Parameters.AddWithValue("$c", changeId);
            using var r = read.ExecuteReader();
            while (r.Read())
                ops.Add((r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4),
                    r.IsDBNull(5) || r.GetString(5).Length == 0 ? null : r.GetString(5)));
        }
        int removed = 0, restored = 0;
        foreach (var (op, ruleId, app, words, category, tag) in ops)
        {
            if (op == "added")
            {
                var existing = LearnedRuleStore.GetRule(db, ruleId);
                // Only the very rule this change made (same app/words/category): never a different
                // rule that later reused the id.
                if (existing != null && existing.App == app && LearnedRuleStore.JoinWords(existing.Words) == words
                    && existing.Category == category && (existing.Tag ?? "") == (tag ?? ""))
                {
                    LearnedRuleStore.DeleteRule(db, ruleId);
                    removed++;
                }
            }
            else if (op == "deleted")
            {
                if (LearnedRuleStore.AddRule(db, app, LearnedRuleStore.SplitWords(words), category, tag) > 0) restored++;
            }
        }
        return (removed, restored);
    }

    // ── history ──────────────────────────────────────────────────────────────

    /// <summary>The kept history, newest first (at most 20 changes, none older than 30 days —
    /// older ones are dropped here as well as on every write, so they expire without a new
    /// change having to happen).</summary>
    public static List<Change> History()
    {
        using var db = new Database();
        db.RunInTransaction(() => Prune(db));
        var result = new List<Change>();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"SELECT {ChangeColumns} FROM change_log ORDER BY id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(ReadChange(r));
        return result;
    }

    private static Change? LoadChange(Database db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"SELECT {ChangeColumns} FROM change_log WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadChange(r) : null;
    }

    private const string ChangeColumns =
        "id, ts, label, field, row_count, undone_at, restored, skipped, " +
        "(SELECT COUNT(*) FROM change_log_rules k WHERE k.change_id = change_log.id)";

    private static Change ReadChange(SqliteDataReader r) => new(
        r.GetInt64(0), ParseTs(r.GetString(1)), r.GetString(2), FieldOf(r.GetString(3)), r.GetInt32(4),
        r.IsDBNull(5) ? null : ParseTs(r.GetString(5)),
        r.IsDBNull(6) ? 0 : r.GetInt32(6), r.IsDBNull(7) ? 0 : r.GetInt32(7), r.GetInt32(8));

    private static DateTime ParseTs(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : DateTime.MinValue;

    /// <summary>Keeps the newest 20 changes and drops any older than 30 days, with their rows and
    /// taught-keyword records (the keywords themselves stay taught — no longer undoable).</summary>
    private static void Prune(Database db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "DELETE FROM change_log WHERE ts < $cutoff " +
            "  OR id NOT IN (SELECT id FROM change_log ORDER BY id DESC LIMIT $keep);" +
            "DELETE FROM change_log_rows WHERE change_id NOT IN (SELECT id FROM change_log);" +
            "DELETE FROM change_log_rules WHERE change_id NOT IN (SELECT id FROM change_log);" +
            "DELETE FROM change_log_rule_ops WHERE change_id NOT IN (SELECT id FROM change_log);";
        cmd.Parameters.AddWithValue("$cutoff", DateTime.Now.AddDays(-ChangeDaysKept).ToIsoTimestamp());
        cmd.Parameters.AddWithValue("$keep", ChangesKept);
        cmd.ExecuteNonQuery();
    }

    // ── backups ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Copies progress.db to data/backups/progress-yyyy-MM-dd_HHmmss-fff.db with SQLite's own online
    /// backup (a consistent copy even while the tracker is writing), then deletes all but the
    /// newest 5. Returns the new file's path. Throws on failure — the caller must not write then.
    /// </summary>
    public static string MakeBackup()
    {
        Directory.CreateDirectory(BackupDir);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss-fff", CultureInfo.InvariantCulture);
        var path = Path.Combine(BackupDir, $"progress-{stamp}.db");
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(BackupDir, $"progress-{stamp}~{n}.db");

        using (var src = AppPaths.OpenConnection())
        // Pooling off: a pooled connection keeps the copy's file handle open after Dispose,
        // which would block deleting it once it falls out of the newest five.
        using (var dst = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = path, Pooling = false }.ToString()))
        {
            dst.Open();
            src.BackupDatabase(dst);
        }

        foreach (var old in ListBackups().Skip(BackupsKept))
        {
            try { File.Delete(old); }
            catch (Exception ex) { Log.Error("DataChangeDoor.PruneBackup", ex); }
        }
        return path;
    }

    /// <summary>Backup files, newest first (the names sort by time).</summary>
    public static List<string> ListBackups() =>
        Directory.Exists(BackupDir)
            ? Directory.GetFiles(BackupDir, "progress-*.db")
                .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).ToList()
            : new List<string>();

    /// <summary>Deletes every backup — called by the Settings "Clear …" actions, which promise
    /// the data is gone; a backup left behind would quietly break that promise. Returns the
    /// names that could not be removed.</summary>
    public static List<string> DeleteBackups()
    {
        var failed = new List<string>();
        foreach (var f in ListBackups())
        {
            try { File.Delete(f); }
            catch (Exception ex)
            {
                Log.Error("DataChangeDoor.DeleteBackup", ex);
                failed.Add(Path.GetFileName(f));
            }
        }
        return failed;
    }

    // ── row access ───────────────────────────────────────────────────────────

    private sealed record Row(long Id, DateOnly Date, string DateText, string Start, string End, int Dur,
        string Category, string Window, string? Desc, string? Tag, string? RuleState = null);

    private static string? ValueOf(Row r, Field f) => f == Field.DiaryCategory ? r.Category : r.Tag;

    /// <summary>The row's full visible state as one string — equal before and after means
    /// "nobody touched this row in between".</summary>
    private static string Signature(Row r) => string.Join('\u001f',
        r.DateText, r.Start, r.End, r.Dur.ToString(CultureInfo.InvariantCulture), r.Category, r.Window,
        r.Desc ?? "\u001e", r.Tag ?? "\u001e")
        // Only when set, so signatures stored before G9b (no rule_state) still match their rows.
        + (r.RuleState is null ? "" : "\u001f" + r.RuleState);

    /// <summary>Current rows by id, read in chunks (SQLite caps the number of parameters).</summary>
    private static Dictionary<long, Row> ReadRows(Database db, IReadOnlyCollection<long> ids)
    {
        var result = new Dictionary<long, Row>(ids.Count);
        foreach (var chunk in ids.Distinct().Chunk(500))
        {
            using var cmd = db.CreateCommand();
            var names = new List<string>(chunk.Length);
            for (var i = 0; i < chunk.Length; i++)
            {
                names.Add($"$i{i}");
                cmd.Parameters.AddWithValue($"$i{i}", chunk[i]);
            }
            cmd.CommandText =
                "SELECT id, date, start_time, end_time, duration_min, category, window, description, tag, rule_state " +
                $"FROM time_diary WHERE id IN ({string.Join(',', names)})";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var dateText = r.GetString(1);
                dateText.TryParseIsoDate(out var date);
                result[r.GetInt64(0)] = new Row(r.GetInt64(0), date, dateText, r.GetString(2), r.GetString(3),
                    r.GetInt32(4), r.GetString(5), r.GetString(6),
                    r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8),
                    r.IsDBNull(9) ? null : r.GetString(9));
            }
        }
        return result;
    }
}

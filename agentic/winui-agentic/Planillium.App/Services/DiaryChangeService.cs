using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>Which diary column a bulk change writes.</summary>
public enum ChangeColumn { Category, Tag }

/// <summary>One bulk change to ask the door for. <paramref name="NewValue"/> null with
/// <see cref="ChangeColumn.Tag"/> means "clear the tag". <paramref name="ScopeText"/> is the
/// human description of what was targeted (stored in the change history).</summary>
public sealed record ChangeRequest(ChangeColumn Column, string? NewValue, IReadOnlyCollection<long> RowIds,
    string ScopeText, bool TeachKeywords);

/// <summary>What a change would do, read fresh from the database. Missing = ids no longer
/// in the table (dropped, not shown).</summary>
public sealed record ChangePreview(int Filled, int Overwritten, int Unchanged, int Missing)
{
    public int Total => Filled + Overwritten;
}

public sealed record ChangeResult(long ChangeId, int Changed, IReadOnlyList<DateOnly> AffectedDates, bool KeywordsChanged);

public sealed record UndoResult(int Restored, int Skipped, IReadOnlyList<DateOnly> AffectedDates, bool KeywordsChanged);

public sealed record ChangeEntry(long Id, DateTime At, ChangeColumn Column, string? NewValue, string ScopeText,
    int RowCount, DateTime? UndoneAt, int? Restored, int? Skipped);

/// <summary>The dated backup before a bulk write could not be made — nothing was written.</summary>
public sealed class BackupFailedException : Exception
{
    public BackupFailedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// G3 (2026-10-29) — the ONE safe door every bulk write to past diary rows goes through
/// (DECISIONS rule 17): preview (counts read fresh from the database) → dated backup of
/// progress.db (newest 5 kept) → one transaction that writes only the target column and logs
/// every changed row's old value plus a fingerprint of the row as written → Undo that restores
/// the old values and skips any row edited or removed since (fingerprint differs / row gone).
/// Plain C#, no WinUI types, so the test project can source-link it.
/// </summary>
public static class DiaryChangeService
{
    public const int KeepBackups = 5;
    public const int KeepEntries = 20;
    public const int KeepDays = 30;
    private const int ChunkSize = 500;

    /// <summary>Test seam for the 30-day rule and backup names.</summary>
    public static Func<DateTime> Now = () => DateTime.Now;

    private static string ColumnName(ChangeColumn c) => c == ChangeColumn.Category ? "category" : "tag";
    private static string KindText(ChangeColumn c) => c == ChangeColumn.Category ? "category" : "tag";

    private static bool NoValue(string? v) => string.IsNullOrWhiteSpace(v);

    /// <summary>Same value as far as this change is concerned: category ordinal; tag ordinal,
    /// with every "no value" form (null / empty / whitespace) counting as the cleared tag.</summary>
    private static bool SameValue(ChangeColumn col, string? stored, string? newValue)
    {
        if (col == ChangeColumn.Tag && newValue is null) return NoValue(stored);
        return string.Equals(stored, newValue, StringComparison.Ordinal);
    }

    private sealed record Row(long Id, string Date, string Start, string End, int Dur, string Category,
        string Window, string? Desc, string? Tag)
    {
        public string? Value(ChangeColumn c) => c == ChangeColumn.Category ? Category : Tag;
        public Row With(ChangeColumn c, string? v) => c == ChangeColumn.Category ? this with { Category = v! } : this with { Tag = v };
    }

    /// <summary>Row fingerprint: date|start|end|dur|category|window|desc|tag (null as \0),
    /// SHA-256 hex. Any later single-row edit or bulk change of that row changes it.</summary>
    private static string Fingerprint(Row r)
    {
        static string F(string? s) => s ?? "\0";
        var text = string.Join("|", r.Date, r.Start, r.End, r.Dur.ToString(CultureInfo.InvariantCulture),
            r.Category, r.Window, F(r.Desc), F(r.Tag));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Current rows for the given ids, read in chunks of 500 per IN (...).</summary>
    private static Dictionary<long, Row> ReadRows(Database db, IEnumerable<long> ids)
    {
        var result = new Dictionary<long, Row>();
        foreach (var chunk in ids.Distinct().Chunk(ChunkSize))
        {
            using var cmd = db.CreateCommand();
            var names = new string[chunk.Length];
            for (var i = 0; i < chunk.Length; i++)
            {
                names[i] = "$p" + i.ToString(CultureInfo.InvariantCulture);
                cmd.Parameters.AddWithValue(names[i], chunk[i]);
            }
            cmd.CommandText =
                "SELECT id, date, start_time, end_time, duration_min, category, window, description, tag " +
                $"FROM time_diary WHERE id IN ({string.Join(",", names)})";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var row = new Row(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4),
                    r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7),
                    r.IsDBNull(8) ? null : r.GetString(8));
                result[row.Id] = row;
            }
        }
        return result;
    }

    // ── preview ──────────────────────────────────────────────────────────

    public static ChangePreview Preview(Database db, ChangeRequest req)
    {
        var rows = ReadRows(db, req.RowIds);
        int filled = 0, overwritten = 0, unchanged = 0;
        foreach (var row in rows.Values)
        {
            var stored = row.Value(req.Column);
            if (SameValue(req.Column, stored, req.NewValue)) unchanged++;
            else if (NoValue(stored)) filled++;
            else overwritten++;
        }
        var missing = req.RowIds.Distinct().Count() - rows.Count;
        return new ChangePreview(filled, overwritten, unchanged, missing);
    }

    // ── apply ────────────────────────────────────────────────────────────

    public static ChangeResult Apply(Database db, ChangeRequest req)
    {
        if (req.Column == ChangeColumn.Category &&
            req.NewValue is not (DiaryCategory.OnPlan or DiaryCategory.OffPlan or DiaryCategory.Neutral))
            throw new ArgumentException($"'{req.NewValue}' is not a category a bulk mark can set.", nameof(req));

        MakeBackup(db);

        var col = ColumnName(req.Column);
        var teach = req.TeachKeywords && req.Column == ChangeColumn.Category;
        var keywords = new List<(string Keyword, string? Prior)>();
        var dates = new HashSet<DateOnly>();
        long changeId = 0;
        var changed = 0;

        db.RunInTransaction(() =>
        {
            var current = ReadRows(db, req.RowIds);
            var toWrite = current.Values
                .Where(r => !SameValue(req.Column, r.Value(req.Column), req.NewValue))
                .OrderBy(r => r.Id)
                .ToList();
            if (toWrite.Count == 0) return;

            db.UpdateDiaryColumn(col, toWrite.Select(r => (r.Id, req.NewValue)));

            using (var head = db.CreateCommand())
            {
                head.CommandText =
                    "INSERT INTO change_history (ts, kind, new_value, scope_text, row_count) " +
                    "VALUES ($ts, $kind, $nv, $scope, $n); SELECT last_insert_rowid();";
                head.Parameters.AddWithValue("$ts", Now().ToIsoTimestamp());
                head.Parameters.AddWithValue("$kind", KindText(req.Column));
                head.Parameters.AddWithValue("$nv", (object?)req.NewValue ?? DBNull.Value);
                head.Parameters.AddWithValue("$scope", req.ScopeText ?? "");
                head.Parameters.AddWithValue("$n", toWrite.Count);
                changeId = Convert.ToInt64(head.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using (var ins = db.CreateCommand())
            {
                ins.CommandText =
                    "INSERT INTO change_history_rows (change_id, diary_id, old_value, new_value, fingerprint) " +
                    "VALUES ($c, $d, $o, $n, $f)";
                var pc = ins.Parameters.Add("$c", SqliteType.Integer);
                var pd = ins.Parameters.Add("$d", SqliteType.Integer);
                var po = ins.Parameters.Add("$o", SqliteType.Text);
                var pn = ins.Parameters.Add("$n", SqliteType.Text);
                var pf = ins.Parameters.Add("$f", SqliteType.Text);
                foreach (var r in toWrite)
                {
                    pc.Value = changeId;
                    pd.Value = r.Id;
                    po.Value = (object?)r.Value(req.Column) ?? DBNull.Value;
                    pn.Value = (object?)req.NewValue ?? DBNull.Value;
                    pf.Value = Fingerprint(r.With(req.Column, req.NewValue));
                    ins.ExecuteNonQuery();
                    if (r.Date.TryParseIsoDate(out var d)) dates.Add(d);
                }
            }

            if (teach)
            {
                keywords = CollectKeywords(toWrite.Select(r => r.Window), req.NewValue!);
                using var kw = db.CreateCommand();
                kw.CommandText =
                    "INSERT INTO change_history_keywords (change_id, keyword, taught_list, prior_list) " +
                    "VALUES ($c, $k, $t, $p)";
                var pc = kw.Parameters.Add("$c", SqliteType.Integer);
                var pk = kw.Parameters.Add("$k", SqliteType.Text);
                var pt = kw.Parameters.Add("$t", SqliteType.Text);
                var pp = kw.Parameters.Add("$p", SqliteType.Text);
                foreach (var (keyword, prior) in keywords)
                {
                    pc.Value = changeId;
                    pk.Value = keyword;
                    pt.Value = req.NewValue!;
                    pp.Value = (object?)prior ?? DBNull.Value;
                    kw.ExecuteNonQuery();
                }
            }
            changed = toWrite.Count;
        });

        var keywordsChanged = false;
        if (keywords.Count > 0)
        {
            // After the commit, one config write. A failure here leaves the diary change in
            // place (same as the old after-commit learning); Undo only touches keywords that
            // are actually in the taught list, so it stays safe.
            try { keywordsChanged = ConfigService.LearnActivityRules(keywords.Select(k => k.Keyword), req.NewValue!); }
            catch (Exception ex) { Log.Error("DiaryChange.Apply.LearnKeywords", ex); }
        }

        try { PruneHistory(db); }
        catch (Exception ex) { Log.Error("DiaryChange.Apply.PruneHistory", ex); }

        var dateList = dates.OrderBy(d => d).ToList();
        if (req.Column == ChangeColumn.Category && dateList.Count > 0)
            ScoreService.TryRecalculateDayScores(db, dateList, "DiaryChange.Apply");

        return new ChangeResult(changeId, changed, dateList, keywordsChanged);
    }

    /// <summary>Keywords a category mark teaches, as the old per-row Mark did (sub-item, else
    /// app group; not "—", not empty, not a bare browser), deduplicated case-insensitively.
    /// Keywords already in the target list are left out (nothing to teach, nothing to undo);
    /// for the rest the list they were in before (or null) is recorded.</summary>
    private static List<(string Keyword, string? Prior)> CollectKeywords(IEnumerable<string> windows, string target)
    {
        var lists = new Dictionary<string, HashSet<string>>();
        var root = ConfigService.Root;
        foreach (var cat in new[] { DiaryCategory.OnPlan, DiaryCategory.OffPlan, DiaryCategory.Neutral })
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("activity_rules", out var rules) && rules.ValueKind == JsonValueKind.Object &&
                rules.TryGetProperty(cat, out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String && e.GetString() is { } s) set.Add(s);
            lists[cat] = set;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string, string?)>();
        foreach (var w in windows)
        {
            var keyword = AppNames.Sub(w) ?? AppNames.Group(w);
            if (string.IsNullOrWhiteSpace(keyword) || keyword == "—") continue;
            if (AppNames.Browsers.Contains(keyword.Trim())) continue;
            if (!seen.Add(keyword)) continue;
            if (lists[target].Contains(keyword)) continue;
            string? prior = null;
            foreach (var cat in new[] { DiaryCategory.OnPlan, DiaryCategory.OffPlan, DiaryCategory.Neutral })
                if (cat != target && lists[cat].Contains(keyword)) { prior = cat; break; }
            result.Add((keyword, prior));
        }
        return result;
    }

    // ── undo ─────────────────────────────────────────────────────────────

    public static UndoResult Undo(Database db, long changeId)
    {
        ChangeColumn column;
        using (var head = db.CreateCommand())
        {
            head.CommandText = "SELECT kind, undone_at FROM change_history WHERE id=$id";
            head.Parameters.AddWithValue("$id", changeId);
            using var r = head.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("This change is no longer in the change history.");
            if (!r.IsDBNull(1)) throw new InvalidOperationException("This change was already undone.");
            column = r.GetString(0) == "tag" ? ChangeColumn.Tag : ChangeColumn.Category;
        }

        MakeBackup(db);

        int restored = 0, skipped = 0;
        var dates = new HashSet<DateOnly>();
        var records = new List<(string Keyword, string TaughtList, string? PriorList)>();

        db.RunInTransaction(() =>
        {
            var logged = new List<(long DiaryId, string? Old, string Fp)>();
            using (var q = db.CreateCommand())
            {
                q.CommandText = "SELECT diary_id, old_value, fingerprint FROM change_history_rows WHERE change_id=$c";
                q.Parameters.AddWithValue("$c", changeId);
                using var r = q.ExecuteReader();
                while (r.Read())
                    logged.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2)));
            }

            var current = ReadRows(db, logged.Select(l => l.DiaryId));
            var toRestore = new List<(long Id, string? Value)>();
            foreach (var (id, old, fp) in logged)
            {
                if (!current.TryGetValue(id, out var row) || Fingerprint(row) != fp) { skipped++; continue; }
                if (column == ChangeColumn.Category && old is null) { skipped++; continue; }
                toRestore.Add((id, old));
                if (row.Date.TryParseIsoDate(out var d)) dates.Add(d);
            }
            if (toRestore.Count > 0) db.UpdateDiaryColumn(ColumnName(column), toRestore);
            restored = toRestore.Count;

            using (var mark = db.CreateCommand())
            {
                mark.CommandText = "UPDATE change_history SET undone_at=$u, restored=$r, skipped=$s WHERE id=$id";
                mark.Parameters.AddWithValue("$u", Now().ToIsoTimestamp());
                mark.Parameters.AddWithValue("$r", restored);
                mark.Parameters.AddWithValue("$s", skipped);
                mark.Parameters.AddWithValue("$id", changeId);
                mark.ExecuteNonQuery();
            }

            using var kq = db.CreateCommand();
            kq.CommandText = "SELECT keyword, taught_list, prior_list FROM change_history_keywords WHERE change_id=$c";
            kq.Parameters.AddWithValue("$c", changeId);
            using var kr = kq.ExecuteReader();
            while (kr.Read())
                records.Add((kr.GetString(0), kr.GetString(1), kr.IsDBNull(2) ? null : kr.GetString(2)));
        });

        var keywordsChanged = false;
        if (records.Count > 0)
        {
            try { keywordsChanged = ConfigService.UndoLearnedRules(records); }
            catch (Exception ex) { Log.Error("DiaryChange.Undo.ForgetKeywords", ex); }
        }

        var dateList = dates.OrderBy(d => d).ToList();
        if (column == ChangeColumn.Category && dateList.Count > 0)
            ScoreService.TryRecalculateDayScores(db, dateList, "DiaryChange.Undo");

        return new UndoResult(restored, skipped, dateList, keywordsChanged);
    }

    // ── history ──────────────────────────────────────────────────────────

    /// <summary>The change log, newest first, after dropping entries older than 30 days and
    /// all but the newest 20.</summary>
    public static IReadOnlyList<ChangeEntry> History(Database db)
    {
        PruneHistory(db);
        var list = new List<ChangeEntry>();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "SELECT id, ts, kind, new_value, scope_text, row_count, undone_at, restored, skipped " +
            "FROM change_history ORDER BY id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ChangeEntry(
                r.GetInt64(0),
                ParseTs(r.GetString(1)) ?? DateTime.MinValue,
                r.GetString(2) == "tag" ? ChangeColumn.Tag : ChangeColumn.Category,
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetString(4),
                r.GetInt32(5),
                r.IsDBNull(6) ? null : ParseTs(r.GetString(6)),
                r.IsDBNull(7) ? null : r.GetInt32(7),
                r.IsDBNull(8) ? null : r.GetInt32(8)));
        }
        return list;
    }

    private static DateTime? ParseTs(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;

    /// <summary>30 days, then newest 20, then the child rows of whatever was removed.</summary>
    private static void PruneHistory(Database db)
    {
        db.RunInTransaction(() =>
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "DELETE FROM change_history WHERE ts < $cut; " +
                "DELETE FROM change_history WHERE id NOT IN (SELECT id FROM change_history ORDER BY id DESC LIMIT $keep); " +
                "DELETE FROM change_history_rows WHERE change_id NOT IN (SELECT id FROM change_history); " +
                "DELETE FROM change_history_keywords WHERE change_id NOT IN (SELECT id FROM change_history);";
            cmd.Parameters.AddWithValue("$cut", Now().AddDays(-KeepDays).ToIsoTimestamp());
            cmd.Parameters.AddWithValue("$keep", KeepEntries);
            cmd.ExecuteNonQuery();
        });
    }

    // ── backups ──────────────────────────────────────────────────────────

    /// <summary>Where the dated backups live: &lt;folder of progress.db&gt;/backups.</summary>
    public static string BackupDir => Path.Combine(Path.GetDirectoryName(AppPaths.DbPath)!, "backups");

    private static List<string> BackupFiles()
    {
        if (!Directory.Exists(BackupDir)) return new List<string>();
        return Directory.GetFiles(BackupDir, "progress-*.db")
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>SQLite online backup of the live database (consistent under WAL, unlike a
    /// file copy), then all but the newest 5 are deleted. Any failure → BackupFailedException,
    /// raised before anything is written.</summary>
    private static void MakeBackup(Database db)
    {
        try
        {
            Directory.CreateDirectory(BackupDir);
            var stem = "progress-" + Now().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(BackupDir, stem + ".db");
            for (var n = 2; File.Exists(path); n++)
                path = Path.Combine(BackupDir, $"{stem}-{n.ToString(CultureInfo.InvariantCulture)}.db");

            using (var dest = new SqliteConnection(new SqliteConnectionStringBuilder
                   { DataSource = path, Pooling = false }.ToString()))
            {
                dest.Open();
                db.Conn.BackupDatabase(dest);
            }
        }
        catch (Exception ex)
        {
            throw new BackupFailedException(ex.Message, ex);
        }

        try
        {
            var files = BackupFiles();
            foreach (var old in files.Take(Math.Max(0, files.Count - KeepBackups)))
                DeleteBackupFile(old);
        }
        catch (Exception ex)
        {
            // Retention is housekeeping; the backup itself exists, so the write may go ahead.
            Log.Error("DiaryChange.PruneBackups", ex);
        }
    }

    private static void DeleteBackupFile(string path)
    {
        File.Delete(path);
        foreach (var side in new[] { path + "-wal", path + "-shm" })
            if (File.Exists(side)) File.Delete(side);
    }

    /// <summary>Backups on disk and change-log entries — for the Clear dialogs' note.</summary>
    public static (int Backups, int Entries) Counts(Database db) => (BackupFiles().Count, db.CountChangeHistory());

    /// <summary>Clear actions: every dated backup and the whole change log go.</summary>
    public static void DeleteAllBackupsAndHistory(Database db)
    {
        db.ClearChangeHistory();
        foreach (var f in BackupFiles()) DeleteBackupFile(f);
    }
}

/// <summary>G3: the door's user-facing wording, shared by the Diary result bar, the confirm
/// dialog and Settings ▸ Change history so the three never drift apart. Numbers N0 invariant;
/// "row" singular only for exactly 1. Skipped rows are "edited or removed since" (lead ruling 6:
/// the skip count covers both).</summary>
public static class DiaryChangeText
{
    public static string N0(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    public static string Rows(int n) => n == 1 ? "1 row" : $"{N0(n)} rows";

    /// <summary>"on-plan" / "off-plan" / "neutral", or the tag's label.</summary>
    public static string ValueWord(ChangeColumn column, string? value) =>
        column == ChangeColumn.Category
            ? (value ?? "").Replace('_', '-')
            : value is null ? "no tag" : DiaryTag.LabelOf(value) ?? value;

    public static string ConfirmTitle(ChangeColumn column, string? value, int total)
    {
        if (total == 0) return "Nothing to change";
        if (column == ChangeColumn.Category) return $"Mark {Rows(total)} as {ValueWord(column, value)}?";
        return value is null ? $"Clear the tag of {Rows(total)}?" : $"Tag {Rows(total)} as {ValueWord(column, value)}?";
    }

    public static string PrimaryText(ChangeColumn column, string? value, int total)
    {
        if (column == ChangeColumn.Category) return $"Mark {Rows(total)}";
        if (value is not null) return $"Tag {Rows(total)}";
        return total == 1 ? "Clear 1 tag" : $"Clear {N0(total)} tags";
    }

    public static string RunningTitle(ChangeColumn column, string? value, int total)
    {
        if (column == ChangeColumn.Category) return $"Marking {Rows(total)} as {ValueWord(column, value)}...";
        return value is null ? $"Clearing the tag of {Rows(total)}..." : $"Tagging {Rows(total)} as {ValueWord(column, value)}...";
    }

    /// <summary>Success bar title and Settings history title.</summary>
    public static string DoneTitle(ChangeColumn column, string? value, int n)
    {
        if (column == ChangeColumn.Category) return $"Marked {Rows(n)} as {ValueWord(column, value)}";
        return value is null ? $"Cleared the tag of {Rows(n)}" : $"Tagged {Rows(n)} as {ValueWord(column, value)}";
    }

    public static string UndoneTitle(int restored) => $"Undone: {Rows(restored)} restored";

    public static string SomeSkipped(int skipped) => skipped == 1
        ? "1 row was skipped because it was edited or removed since."
        : $"{N0(skipped)} rows were skipped because they were edited or removed since.";

    public static string AllSkipped(int skipped) => skipped == 1
        ? "The 1 row was edited or removed since, so it was left as it is."
        : $"All {N0(skipped)} rows were edited or removed since, so they were left as they are.";

    /// <summary>One line for Settings' SaveStatus strip.</summary>
    public static string UndoSummary(int restored, int skipped) =>
        $"Undone: {Rows(restored)} restored." + (skipped > 0 ? $" {N0(skipped)} skipped (edited or removed since)." : "");
}

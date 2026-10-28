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
    public enum Field { DiaryCategory, DiaryTag }

    public sealed record Preview(int Filled, int Overwritten, int Unchanged, int Missing)
    {
        public int Changed => Filled + Overwritten;
    }

    public sealed record ApplyResult(long ChangeId, int Changed, string BackupFile, IReadOnlyCollection<DateOnly> Dates);

    public sealed record UndoResult(int Restored, int Skipped, IReadOnlyCollection<DateOnly> Dates);

    public sealed record Change(long Id, DateTime At, string Label, Field Field, int RowCount,
        DateTime? UndoneAt, int Restored, int Skipped);

    public static string BackupDir => Path.Combine(AppPaths.Root, "data", "backups");

    private static string Column(Field f) => f switch
    {
        Field.DiaryCategory => "category",
        Field.DiaryTag => "tag",
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    private static Field FieldOf(string column) => column switch
    {
        "category" => Field.DiaryCategory,
        "tag" => Field.DiaryTag,
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
            update.CommandText = $"UPDATE time_diary SET {Column(field)}=$v WHERE id=$id";
            var pv = update.Parameters.Add("$v", SqliteType.Text);
            var pid = update.Parameters.Add("$id", SqliteType.Integer);
            using var record = db.CreateCommand();
            record.CommandText =
                "INSERT INTO change_log_rows (change_id, row_id, old_value, after_sig) VALUES ($c, $r, $old, $sig)";
            record.Parameters.AddWithValue("$c", changeId);
            var pr = record.Parameters.Add("$r", SqliteType.Integer);
            var pold = record.Parameters.Add("$old", SqliteType.Text);
            var psig = record.Parameters.Add("$sig", SqliteType.Text);

            // Category is NOT NULL in time_diary; only the tag may be stored as NULL.
            var stored = field == Field.DiaryCategory ? target! : (object?)target ?? DBNull.Value;
            foreach (var row in toChange)
            {
                pv.Value = stored;
                pid.Value = row.Id;
                update.ExecuteNonQuery();

                var after = field == Field.DiaryCategory ? row with { Category = target! } : row with { Tag = target };
                pr.Value = row.Id;
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

    // ── undo ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores the old value of every row this change touched that is still exactly as the
    /// change left it; rows edited, split, re-marked or deleted since are skipped and counted.
    /// Backs up first (an Undo is itself a bulk change). A change can be undone once.
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
        int restored = 0, skipped = 0;
        Field field = Field.DiaryCategory;
        db.RunInTransaction(() =>
        {
            var change = LoadChange(db, changeId)
                ?? throw new InvalidOperationException("That change is no longer in the history.");
            if (change.UndoneAt is not null)
                throw new InvalidOperationException("That change was already undone.");
            field = change.Field;

            var saved = new List<(long RowId, string? Old, string Sig)>();
            using (var read = db.CreateCommand())
            {
                read.CommandText = "SELECT row_id, old_value, after_sig FROM change_log_rows WHERE change_id=$c";
                read.Parameters.AddWithValue("$c", changeId);
                using var r = read.ExecuteReader();
                while (r.Read())
                    saved.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2)));
            }

            var current = ReadRows(db, saved.Select(s => s.RowId).ToList());
            using var update = db.CreateCommand();
            update.CommandText = $"UPDATE time_diary SET {Column(field)}=$v WHERE id=$id";
            var pv = update.Parameters.Add("$v", SqliteType.Text);
            var pid = update.Parameters.Add("$id", SqliteType.Integer);
            foreach (var (rowId, old, sig) in saved)
            {
                if (!current.TryGetValue(rowId, out var row) || Signature(row) != sig)
                {
                    skipped++;
                    continue;
                }
                // The old value goes back exactly as it was stored ("" stays "", NULL stays NULL)
                // — except a category, which the column never allows to be NULL.
                pv.Value = field == Field.DiaryCategory ? old ?? "" : (object?)old ?? DBNull.Value;
                pid.Value = rowId;
                update.ExecuteNonQuery();
                restored++;
                dates.Add(row.Date);
            }

            using var mark = db.CreateCommand();
            mark.CommandText = "UPDATE change_log SET undone_at=$ts, restored=$r, skipped=$s WHERE id=$c";
            mark.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
            mark.Parameters.AddWithValue("$r", restored);
            mark.Parameters.AddWithValue("$s", skipped);
            mark.Parameters.AddWithValue("$c", changeId);
            mark.ExecuteNonQuery();
        });

        if (field == Field.DiaryCategory && dates.Count > 0)
            ScoreService.TryRecalculateDayScores(db, dates, "DataChangeDoor.Undo.RecalculateScore");
        return new UndoResult(restored, skipped, dates);
    }

    /// <summary>The one wording for an Undo's outcome — the Diary message and the Settings
    /// history both show it.</summary>
    public static string DescribeUndo(UndoResult r)
    {
        string Rows(int n) => $"{n.ToString("N0", CultureInfo.CurrentCulture)} {(n == 1 ? "row" : "rows")}";
        return r.Skipped == 0
            ? $"Restored {Rows(r.Restored)} to their old values."
            : $"Restored {Rows(r.Restored)} to their old values. Skipped {Rows(r.Skipped)} changed since " +
              "(edited, split, re-marked or deleted) — they keep their newer values.";
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
            "SELECT id, ts, label, field, row_count, undone_at, restored, skipped FROM change_log ORDER BY id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(ReadChange(r));
        return result;
    }

    private static Change? LoadChange(Database db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "SELECT id, ts, label, field, row_count, undone_at, restored, skipped FROM change_log WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadChange(r) : null;
    }

    private static Change ReadChange(SqliteDataReader r) => new(
        r.GetInt64(0), ParseTs(r.GetString(1)), r.GetString(2), FieldOf(r.GetString(3)), r.GetInt32(4),
        r.IsDBNull(5) ? null : ParseTs(r.GetString(5)),
        r.IsDBNull(6) ? 0 : r.GetInt32(6), r.IsDBNull(7) ? 0 : r.GetInt32(7));

    private static DateTime ParseTs(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : DateTime.MinValue;

    /// <summary>Keeps the newest 20 changes and drops any older than 30 days, with their rows.</summary>
    private static void Prune(Database db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "DELETE FROM change_log WHERE ts < $cutoff " +
            "  OR id NOT IN (SELECT id FROM change_log ORDER BY id DESC LIMIT $keep);" +
            "DELETE FROM change_log_rows WHERE change_id NOT IN (SELECT id FROM change_log);";
        cmd.Parameters.AddWithValue("$cutoff", DateTime.Now.AddDays(-ChangeDaysKept).ToIsoTimestamp());
        cmd.Parameters.AddWithValue("$keep", ChangesKept);
        cmd.ExecuteNonQuery();
    }

    // ── backups ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Copies progress.db to data/backups/progress-yyyy-MM-dd_HHmmss.db with SQLite's own online
    /// backup (a consistent copy even while the tracker is writing), then deletes all but the
    /// newest 5. Returns the new file's path. Throws on failure — the caller must not write then.
    /// </summary>
    public static string MakeBackup()
    {
        Directory.CreateDirectory(BackupDir);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(BackupDir, $"progress-{stamp}.db");
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(BackupDir, $"progress-{stamp}-{n}.db");

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
        string Category, string Window, string? Desc, string? Tag);

    private static string? ValueOf(Row r, Field f) => f == Field.DiaryCategory ? r.Category : r.Tag;

    /// <summary>The row's full visible state as one string — equal before and after means
    /// "nobody touched this row in between".</summary>
    private static string Signature(Row r) => string.Join('\u001f',
        r.DateText, r.Start, r.End, r.Dur.ToString(CultureInfo.InvariantCulture), r.Category, r.Window,
        r.Desc ?? "\u001e", r.Tag ?? "\u001e");

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
                "SELECT id, date, start_time, end_time, duration_min, category, window, description, tag " +
                $"FROM time_diary WHERE id IN ({string.Join(',', names)})";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var dateText = r.GetString(1);
                dateText.TryParseIsoDate(out var date);
                result[r.GetInt64(0)] = new Row(r.GetInt64(0), date, dateText, r.GetString(2), r.GetString(3),
                    r.GetInt32(4), r.GetString(5), r.GetString(6),
                    r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8));
            }
        }
        return result;
    }
}

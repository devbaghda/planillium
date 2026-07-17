using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MentorOverseer.App.Services;

/// <summary>Read/write access to data/progress.db.</summary>
public sealed class Database : IDisposable
{
    private readonly SqliteConnection _conn;

    /// <summary>Shared with ScoreService so it doesn't open a second connection
    /// to the same file on every construction — that was pure avoidable
    /// per-render overhead (measured ~200ms of a ~550ms Reports render).</summary>
    internal SqliteConnection Conn => _conn;

    private SqliteTransaction? _activeTransaction;

    /// <summary>Creates a command on the shared connection, automatically attached to
    /// whatever transaction RunInTransaction currently has open (or none). ScoreService
    /// shares this connection (via Conn) and must go through this too — Microsoft.Data.Sqlite
    /// throws if a command runs against a connection with a pending transaction but doesn't
    /// have that transaction explicitly assigned.</summary>
    internal SqliteCommand CreateCommand()
    {
        var cmd = _conn.CreateCommand();
        if (_activeTransaction is not null) cmd.Transaction = _activeTransaction;
        return cmd;
    }

    /// <summary>
    /// Runs <paramref name="work"/> as one all-or-nothing database transaction — committed
    /// only if it returns without throwing, otherwise rolled back — so a multi-statement
    /// write (e.g. delete-then-reinsert, or shifting several tasks' assigned days) can never
    /// be left half-applied by a failure partway through (round-5 audit finding #1: exactly
    /// this shape of bug in SplitDiaryEntryDialog lost diary data on a mid-loop failure).
    /// Nest-safe: a call while already inside a transaction just runs work() as part of the
    /// outer one, so callers don't need to know whether they're the outermost caller.
    /// </summary>
    public void RunInTransaction(Action work)
    {
        if (_activeTransaction is not null) { work(); return; }
        using var tx = _conn.BeginTransaction();
        _activeTransaction = tx;
        try
        {
            work();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        finally
        {
            _activeTransaction = null;
        }
    }

    // Every page navigation, every checkbox toggle, every button click opens
    // a new Database() — construction used to re-run the full schema check
    // (a sqlite_master scan plus 8 CREATE-IF-NOT-EXISTS statements) EVERY
    // single time, which is pure overhead after the first call in a running
    // process (the schema can't change mid-session). Gate it to once.
    private static bool _schemaEnsured;
    private static readonly object SchemaGate = new();

    /// <summary>Raw time_diary rows older than this are pruned (after being
    /// rolled up into diary_daily_rollup) — see PruneAndRollupDiary.</summary>
    public const int DiaryRetentionDays = 90;

    public Database()
    {
        Directory.CreateDirectory(Path.Combine(AppPaths.Root, "data"));
        _conn = AppPaths.OpenConnection();
        lock (SchemaGate)
        {
            if (!_schemaEnsured)
            {
                EnsureSchema();
                _schemaEnsured = true;
            }
        }
    }

    /// <summary>
    /// Same tables main.py's ensure_data_store() creates (minus its v1-table
    /// migration, which only applies to a pre-multi-plan database that can't
    /// exist on a fresh install) plus tracker/activity.py's time_diary and
    /// activity_log — every table any part of either app touches. Every
    /// statement is idempotent; gated to run once per process (see above)
    /// rather than on every construction.
    /// </summary>
    private void EnsureSchema()
    {
        // sl_reason_date already exists on any pre-existing progress.db with
        // the old (daily_score, overdue_accrual) WHERE clause — "CREATE
        // INDEX IF NOT EXISTS" is a no-op against an existing index of that
        // name regardless of definition, so adding weekly_comeback_bonus to
        // the guard needs an explicit drop-and-recreate, not just a wider
        // CREATE statement below.
        using (var check = CreateCommand())
        {
            check.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='sl_reason_date'";
            if (check.ExecuteScalar() is string sql && !sql.Contains(ScoreReason.WeeklyComebackBonus))
            {
                using var drop = CreateCommand();
                drop.CommandText = "DROP INDEX sl_reason_date";
                drop.ExecuteNonQuery();
            }
        }

        using var cmd = CreateCommand();
        cmd.CommandText =
            "CREATE TABLE IF NOT EXISTS task_completions (" +
            "  id           INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  plan_id      TEXT    NOT NULL DEFAULT ''," +
            "  plan_day     INTEGER NOT NULL," +
            "  task_text    TEXT    NOT NULL," +
            "  completed    INTEGER NOT NULL," +
            "  completed_at TEXT," +
            "  last_updated TEXT    NOT NULL" +
            ");" +
            "CREATE UNIQUE INDEX IF NOT EXISTS tc_plan_idx " +
            "  ON task_completions(plan_id, plan_day, task_text);" +
            "CREATE TABLE IF NOT EXISTS ticktick_sync (" +
            "  id               INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  plan_day         INTEGER NOT NULL," +
            "  task_text        TEXT    NOT NULL," +
            "  ticktick_task_id TEXT," +
            "  ticktick_proj_id TEXT," +
            "  pushed_at        TEXT," +
            "  synced_at        TEXT," +
            "  UNIQUE(plan_day, task_text)" +
            ");" +
            "CREATE TABLE IF NOT EXISTS task_overrides (" +
            "  plan_id      TEXT    NOT NULL," +
            "  task_text    TEXT    NOT NULL," +
            "  original_day INTEGER NOT NULL," +
            "  assigned_day INTEGER NOT NULL," +
            "  PRIMARY KEY (plan_id, task_text)" +
            ");" +
            "CREATE TABLE IF NOT EXISTS plan_days_off (" +
            "  plan_id   TEXT    NOT NULL," +
            "  day       INTEGER NOT NULL," +
            "  marked_at TEXT," +
            "  PRIMARY KEY (plan_id, day)" +
            ");" +
            // The user's own scratchpad per task — separate from the plan
            // JSON's detail/mentor_note (Claude-authored, read-only). Keyed
            // the same way task_overrides is; an empty note just deletes
            // the row rather than storing "".
            "CREATE TABLE IF NOT EXISTS task_notes (" +
            "  plan_id    TEXT    NOT NULL," +
            "  task_text  TEXT    NOT NULL," +
            "  note       TEXT    NOT NULL," +
            "  updated_at TEXT    NOT NULL," +
            "  PRIMARY KEY (plan_id, task_text)" +
            ");" +
            "CREATE TABLE IF NOT EXISTS score_ledger (" +
            "  id     INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  ts     TEXT    NOT NULL," +
            "  date   TEXT    NOT NULL," +
            "  delta  INTEGER NOT NULL," +
            "  reason TEXT    NOT NULL," +
            "  detail TEXT" +
            ");" +
            "CREATE UNIQUE INDEX IF NOT EXISTS sl_reason_date " +
            "  ON score_ledger(reason, date) " +
            $"  WHERE reason IN ('{ScoreReason.DailyScore}', '{ScoreReason.OverdueAccrual}', '{ScoreReason.WeeklyComebackBonus}');" +
            "CREATE TABLE IF NOT EXISTS time_diary (" +
            "  id           INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  date         TEXT NOT NULL," +
            "  start_time   TEXT NOT NULL," +
            "  end_time     TEXT NOT NULL," +
            "  duration_min INTEGER NOT NULL," +
            "  category     TEXT NOT NULL," +
            "  window       TEXT NOT NULL," +
            "  description  TEXT" +
            ");" +
            // activity_log (id, logged_at, window, class) used to be
            // created here too, but nothing in this codebase has ever
            // written to it — it was the retired Python app's per-poll log.
            // Removed rather than kept as always-empty schema noise
            // (2026-07-09 audit finding #35); an existing database that
            // already has the table from an older build is unaffected,
            // SQLite doesn't mind an unused table sitting there.
            // One row per day, written just before that day's raw time_diary
            // rows age out of retention — keeps Year-view minute totals
            // accurate forever even though the per-entry detail is gone.
            "CREATE TABLE IF NOT EXISTS diary_daily_rollup (" +
            "  date        TEXT PRIMARY KEY," +
            "  on_min      INTEGER NOT NULL DEFAULT 0," +
            "  off_min     INTEGER NOT NULL DEFAULT 0," +
            "  neutral_min INTEGER NOT NULL DEFAULT 0," +
            "  paid_min    INTEGER NOT NULL DEFAULT 0," +
            "  idle_min    INTEGER NOT NULL DEFAULT 0" +
            ");" +
            // Evening-review free text. Used to be created only in ScoreService's own
            // constructor rather than here — meaning Database claimed to own/export this
            // table (see ExportedTables below) without actually being the thing that
            // creates it, a hidden dependency on ScoreService having run first that would
            // break "Export all my data" if that ordering ever changed (round-5 audit
            // finding #26).
            "CREATE TABLE IF NOT EXISTS reflections (" +
            "  id   INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  date TEXT NOT NULL UNIQUE," +
            "  text TEXT NOT NULL" +
            ");" +
            // Read-only reporting views — nothing in either app queries
            // these, they exist purely so an external tool (Power Query,
            // Excel, a BI dashboard) can point straight at progress.db
            // without re-deriving the raw-table joins/category math itself.
            // v_diary_daily spans the full history: it unions live
            // time_diary with diary_daily_rollup, so a day's totals read
            // the same whether its raw rows have aged out of the 90-day
            // retention window or not.
            "CREATE VIEW IF NOT EXISTS v_diary_daily AS " +
            "  SELECT date," +
            "    SUM(CASE WHEN category='on_plan'  THEN duration_min ELSE 0 END) AS on_min," +
            "    SUM(CASE WHEN category='off_plan' THEN duration_min ELSE 0 END) AS off_min," +
            "    SUM(CASE WHEN category='neutral'  THEN duration_min ELSE 0 END) AS neutral_min," +
            "    SUM(CASE WHEN category='paid'     THEN duration_min ELSE 0 END) AS paid_min," +
            "    SUM(CASE WHEN category='idle'     THEN duration_min ELSE 0 END) AS idle_min " +
            "  FROM time_diary GROUP BY date " +
            "  UNION ALL " +
            "  SELECT date, on_min, off_min, neutral_min, paid_min, idle_min FROM diary_daily_rollup;" +
            "CREATE VIEW IF NOT EXISTS v_score_daily AS " +
            "  SELECT date, SUM(delta) AS score_delta FROM score_ledger GROUP BY date;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Rolls up (upserts) any day older than DiaryRetentionDays that still
    /// has raw time_diary rows, then deletes those raw rows. Idempotent —
    /// re-running with nothing new to prune is a cheap no-op. Called once at
    /// startup; the search box and Day/Week/Month views only ever need the
    /// last 90 days, so this only ever removes data those views can't reach.
    /// </summary>
    public void PruneAndRollupDiary(int retentionDays = DiaryRetentionDays)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.Today).AddDays(-retentionDays)
            .ToIsoDate();

        // Wrapped so a failure between the rollup INSERT and the raw-row
        // DELETE can't leave a day double-counted (rolled up AND still
        // present as raw rows) — this used to be two independent statements
        // with no transaction around either (2026-07-14 round-6 audit
        // finding #17).
        RunInTransaction(() =>
        {
            using (var rollup = CreateCommand())
            {
                rollup.CommandText =
                    "INSERT INTO diary_daily_rollup (date, on_min, off_min, neutral_min, paid_min, idle_min) " +
                    "SELECT date," +
                    "  SUM(CASE WHEN category='on_plan' THEN duration_min ELSE 0 END)," +
                    "  SUM(CASE WHEN category='off_plan' THEN duration_min ELSE 0 END)," +
                    "  SUM(CASE WHEN category='neutral' THEN duration_min ELSE 0 END)," +
                    "  SUM(CASE WHEN category='paid' THEN duration_min ELSE 0 END)," +
                    "  SUM(CASE WHEN category='idle' THEN duration_min ELSE 0 END) " +
                    "FROM time_diary WHERE date < $cutoff GROUP BY date " +
                    "ON CONFLICT(date) DO UPDATE SET " +
                    "  on_min=excluded.on_min, off_min=excluded.off_min, neutral_min=excluded.neutral_min," +
                    "  paid_min=excluded.paid_min, idle_min=excluded.idle_min";
                rollup.Parameters.AddWithValue("$cutoff", cutoff);
                rollup.ExecuteNonQuery();
            }
            using (var del = CreateCommand())
            {
                del.CommandText = "DELETE FROM time_diary WHERE date < $cutoff";
                del.Parameters.AddWithValue("$cutoff", cutoff);
                del.ExecuteNonQuery();
            }
        });
    }

    /// <summary>Most common past idle-answer descriptions (excludes the "no real
    /// answer" placeholder and blanks), most frequent first — feeds the idle-return
    /// dialog's quick-suggestion chips alongside the fixed Lunch/Break/Errand set.
    /// Excludes both "unaccounted time" (the current placeholder) and the older
    /// "dismissed" (rows saved before that rename) so neither shows up disguised
    /// as a real answer.</summary>
    public List<string> MostFrequentIdleAnswers(int topN = 6)
    {
        var result = new List<string>();
        using var cmd = CreateCommand();
        cmd.CommandText =
            "SELECT description FROM time_diary " +
            "WHERE window='idle' AND description IS NOT NULL AND description <> '' " +
            "AND description NOT IN ('dismissed', 'unaccounted time') " +
            "GROUP BY description COLLATE NOCASE ORDER BY COUNT(*) DESC LIMIT $n";
        cmd.Parameters.AddWithValue("$n", topN);
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetString(0));
        return result;
    }

    /// <summary>End timestamp of the most recent time_diary row, if any — lets the
    /// tracker detect "the app itself wasn't running" gaps (closed and reopened,
    /// machine rebooted) the same way it detects sleep/idle within a running
    /// session, instead of silently dropping that span from the diary.</summary>
    public DateTime? LastDiaryEnd()
    {
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT date, end_time FROM time_diary ORDER BY date DESC, end_time DESC LIMIT 1";
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        var date = r.GetString(0);
        var end = r.GetString(1);
        return DateTime.TryParse($"{date} {end}", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt) ? dt : null;
    }

    /// <summary>Inserts one time_diary row directly — used when splitting an existing
    /// entry into several (unlike ActivityTracker.LogIdleAnswer, this isn't idle-specific:
    /// category is whatever the caller decides, not auto-classified from description text).</summary>
    public void InsertDiaryEntry(string date, string startTime, string endTime, int durationMin,
        string category, string window, string? description)
    {
        using var cmd = CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, $m, $c, $w, $x)";
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$s", startTime);
        cmd.Parameters.AddWithValue("$e", endTime);
        cmd.Parameters.AddWithValue("$m", durationMin);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$w", window);
        cmd.Parameters.AddWithValue("$x", (object?)description ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Rows that ClearActivityHistory would delete — for the confirmation dialog.</summary>
    public (int DiaryRows, int RollupDays) CountActivityHistory()
    {
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT (SELECT COUNT(*) FROM time_diary), (SELECT COUNT(*) FROM diary_daily_rollup)";
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.GetInt32(0), r.GetInt32(1));
    }

    /// <summary>
    /// Wipes the activity/diary history (raw sessions + the rolled-up daily
    /// totals) — the window-title-derived tracking data, not the user's own
    /// plan progress (task_completions/score_ledger/reflections are untouched;
    /// those aren't what a privacy "clear my tracked activity" ask means).
    /// VACUUM afterward since deleted SQLite rows otherwise linger in the
    /// file on disk until compacted.
    /// </summary>
    public void ClearActivityHistory()
    {
        // The two DELETEs are wrapped so a failure between them can't leave
        // raw sessions cleared but the rolled-up totals still sitting there
        // (2026-07-14 round-6 audit finding #17) — VACUUM stays outside,
        // SQLite refuses to run it inside any transaction.
        RunInTransaction(() =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = "DELETE FROM time_diary; DELETE FROM diary_daily_rollup;";
            cmd.ExecuteNonQuery();
        });
        using var vacuum = CreateCommand();
        vacuum.CommandText = "VACUUM";
        vacuum.ExecuteNonQuery();
    }

    private static readonly string[] ExportedTables =
    {
        "task_completions", "task_overrides", "plan_days_off", "task_notes",
        "score_ledger", "reflections", "ticktick_sync", "time_diary", "diary_daily_rollup",
    };

    /// <summary>Total row count across every user-data table — for the "Clear all my
    /// data" confirmation dialog. Cheap enough to run unconditionally (no index needed,
    /// this only ever runs on a Settings button click, never in a hot path).</summary>
    public int CountAllData()
    {
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT " + string.Join(" + ", ExportedTables.Select(t => $"(SELECT COUNT(*) FROM {t})"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Wipes every one of this app's own data tables — task completions, reschedules/
    /// day-offs, task notes, score history, reflections, TickTick sync links, and the
    /// activity diary — in one action. Before this, only the activity diary and
    /// reflections had any delete path at all; everything else (roughly two-thirds of
    /// the tables) could only be removed by hand-editing the SQLite file directly
    /// (audit finding #6). Does NOT touch plans/active/*.json — those are the user's own
    /// authored plan content, not recorded history, and archiving/deleting a plan is its
    /// own separate action on the Plans page.
    /// </summary>
    public void ClearAllData()
    {
        RunInTransaction(() =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = string.Join(" ", ExportedTables.Select(t => $"DELETE FROM {t};"));
            cmd.ExecuteNonQuery();
        });
        using var vacuum = CreateCommand();
        vacuum.CommandText = "VACUUM";
        vacuum.ExecuteNonQuery();
    }

    /// <summary>
    /// Every row of every user-data table, schema-agnostic (reads column
    /// names off each reader rather than hardcoding them) so this doesn't
    /// silently go stale the next time a table gains a column. Used by the
    /// Settings "Export all my data" action — the full picture, not just
    /// the aggregated numbers ReportExport's HTML/CSV reports show.
    /// </summary>
    public Dictionary<string, List<Dictionary<string, object?>>> ExportAllTables()
    {
        var result = new Dictionary<string, List<Dictionary<string, object?>>>();
        foreach (var table in ExportedTables)
        {
            var rows = new List<Dictionary<string, object?>>();
            using var cmd = CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            result[table] = rows;
        }
        return result;
    }

    public Dictionary<(string PlanId, int Day, string Text), bool> LoadCompletions()
    {
        var result = new Dictionary<(string, int, string), bool>();
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT plan_id, plan_day, task_text, completed FROM task_completions";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result[(r.GetString(0), r.GetInt32(1), r.GetString(2))] = r.GetInt32(3) != 0;
        return result;
    }

    /// <summary>Same upsert as main.py save_completion().</summary>
    public void SaveCompletion(string planId, int planDay, string taskText, bool done)
    {
        // Invariant culture: these strings live in the SQLite file shared with
        // the Python app — a non-Gregorian OS calendar must never leak in.
        var now = DateTime.Now.ToIsoTimestamp();
        using var cmd = CreateCommand();
        cmd.CommandText =
            "INSERT INTO task_completions " +
            "  (plan_id, plan_day, task_text, completed, completed_at, last_updated) " +
            "VALUES ($pid, $day, $text, $done, $done_at, $now) " +
            "ON CONFLICT(plan_id, plan_day, task_text) DO UPDATE SET " +
            "  completed=excluded.completed," +
            "  completed_at=excluded.completed_at," +
            "  last_updated=excluded.last_updated";
        cmd.Parameters.AddWithValue("$pid", planId);
        cmd.Parameters.AddWithValue("$day", planDay);
        cmd.Parameters.AddWithValue("$text", taskText);
        cmd.Parameters.AddWithValue("$done", done ? 1 : 0);
        cmd.Parameters.AddWithValue("$done_at", done ? now : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    /// <summary>task_overrides for one plan: task_text → assigned_day.</summary>
    public Dictionary<string, int> LoadOverrides(string planId)
    {
        var result = new Dictionary<string, int>();
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT task_text, assigned_day FROM task_overrides WHERE plan_id=$pid";
        cmd.Parameters.AddWithValue("$pid", planId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result[r.GetString(0)] = r.GetInt32(1);
        return result;
    }

    /// <summary>Same UPDATE as main.py's _edit_diary_entry Save.</summary>
    public void UpdateDiaryEntry(long id, string startTime, string endTime, int durationMin,
        string category, string? description)
    {
        using var cmd = CreateCommand();
        cmd.CommandText =
            "UPDATE time_diary SET start_time=$start, end_time=$end, duration_min=$dur, " +
            "category=$cat, description=$desc WHERE id=$id";
        cmd.Parameters.AddWithValue("$start", startTime);
        cmd.Parameters.AddWithValue("$end", endTime);
        cmd.Parameters.AddWithValue("$dur", durationMin);
        cmd.Parameters.AddWithValue("$cat", category);
        cmd.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Same DELETE as main.py's _delete_diary_entry.</summary>
    public void DeleteDiaryEntry(long id)
    {
        using var cmd = CreateCommand();
        cmd.CommandText = "DELETE FROM time_diary WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>task_notes for one plan: task_text → note.</summary>
    public Dictionary<string, string> LoadTaskNotes(string planId)
    {
        var result = new Dictionary<string, string>();
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT task_text, note FROM task_notes WHERE plan_id=$pid";
        cmd.Parameters.AddWithValue("$pid", planId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result[r.GetString(0)] = r.GetString(1);
        return result;
    }

    /// <summary>Upserts a task's note, or deletes the row if it's now empty.</summary>
    public void SetTaskNote(string planId, string taskText, string note)
    {
        note = note.Trim();
        using var cmd = CreateCommand();
        if (note.Length == 0)
        {
            cmd.CommandText = "DELETE FROM task_notes WHERE plan_id=$pid AND task_text=$text";
        }
        else
        {
            cmd.CommandText =
                "INSERT INTO task_notes (plan_id, task_text, note, updated_at) " +
                "VALUES ($pid, $text, $note, $now) " +
                "ON CONFLICT(plan_id, task_text) DO UPDATE SET " +
                "  note=excluded.note, updated_at=excluded.updated_at";
            cmd.Parameters.AddWithValue("$note", note);
            cmd.Parameters.AddWithValue("$now",
                DateTime.Now.ToIsoTimestamp());
        }
        cmd.Parameters.AddWithValue("$pid", planId);
        cmd.Parameters.AddWithValue("$text", taskText);
        cmd.ExecuteNonQuery();
    }

    public long ScoreBalance()
    {
        using var cmd = CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(delta), 0) FROM score_ledger";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public void Dispose() => _conn.Dispose();
}

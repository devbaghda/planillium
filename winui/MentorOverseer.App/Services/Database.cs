using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MentorOverseer.App.Services;

/// <summary>
/// Read/write access to the Python app's progress.db. SQL mirrors main.py
/// exactly (same tables, same upsert semantics) — schema is the contract.
/// </summary>
public sealed class Database : IDisposable
{
    private readonly SqliteConnection _conn;

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
        _conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        _conn.Open();
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
        using (var check = _conn.CreateCommand())
        {
            check.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='sl_reason_date'";
            if (check.ExecuteScalar() is string sql && !sql.Contains("weekly_comeback_bonus"))
            {
                using var drop = _conn.CreateCommand();
                drop.CommandText = "DROP INDEX sl_reason_date";
                drop.ExecuteNonQuery();
            }
        }

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE IF NOT EXISTS task_completions (" +
            "  id           INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  plan_id      TEXT    NOT NULL DEFAULT 'netherlands'," +
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
            "  WHERE reason IN ('daily_score', 'overdue_accrual', 'weekly_comeback_bonus');" +
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
            "CREATE TABLE IF NOT EXISTS activity_log (" +
            "  id        INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  logged_at TEXT NOT NULL," +
            "  window    TEXT NOT NULL," +
            "  class     TEXT NOT NULL" +
            ");" +
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
            ");";
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
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using (var rollup = _conn.CreateCommand())
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
        using (var del = _conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM time_diary WHERE date < $cutoff";
            del.Parameters.AddWithValue("$cutoff", cutoff);
            del.ExecuteNonQuery();
        }
    }

    public Dictionary<(string PlanId, int Day, string Text), bool> LoadCompletions()
    {
        var result = new Dictionary<(string, int, string), bool>();
        using var cmd = _conn.CreateCommand();
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
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        using var cmd = _conn.CreateCommand();
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
        using var cmd = _conn.CreateCommand();
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
        using var cmd = _conn.CreateCommand();
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
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM time_diary WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public long ScoreBalance()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(delta), 0) FROM score_ledger";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public void Dispose() => _conn.Dispose();
}

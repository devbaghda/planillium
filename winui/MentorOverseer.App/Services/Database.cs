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

    public Database()
    {
        Directory.CreateDirectory(Path.Combine(AppPaths.Root, "data"));
        _conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        _conn.Open();
        EnsureSchema();
    }

    /// <summary>
    /// Same tables main.py's ensure_data_store() creates (minus its v1-table
    /// migration, which only applies to a pre-multi-plan database that can't
    /// exist on a fresh install) plus tracker/activity.py's time_diary and
    /// activity_log — every table any part of either app touches. Every
    /// statement is idempotent, so this runs on every Database construction;
    /// harmless against an existing progress.db, and the only thing standing
    /// between a fresh install and "SQLite Error 1: no such table" on first
    /// launch.
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
            ");";
        cmd.ExecuteNonQuery();
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

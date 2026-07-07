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
        _conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        _conn.Open();
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

using System.Globalization;
using Microsoft.Data.Sqlite;
using MentorOverseer.App.Models;

namespace MentorOverseer.App.Services;

/// <summary>
/// Score economy v2 — the "balanced coach" rules from the approved design:
///   • same base formula and ledger rows as the Python app (daily_score /
///     overdue_accrual, guarded once per date), PLUS
///   • a daily floor of −10 (a bad day is a setback, not a spiral),
///   • overdue accrual capped at 3 days per task, then the task goes stale,
///   • "Replan all overdue" — one flat −10 instead of per-task bleeding.
/// </summary>
public sealed class ScoreService : IDisposable
{
    public const int DailyFloor = -10;
    public const int OverdueAccrualCapDays = 3;
    public const int ReplanFlatFee = -10;
    public const int ReplanDailyBudgetMin = 240;

    private readonly SqliteConnection _conn;
    private readonly List<Plan> _plans;
    private readonly Database _db;
    private readonly Dictionary<(string, int, string), bool> _completions;

    public ScoreService(List<Plan> plans, Database db)
    {
        _plans = plans;
        _db = db;
        _completions = db.LoadCompletions();
        _conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        _conn.Open();
        _conn.CreateCommand()
            .Also(c => c.CommandText =
                "CREATE TABLE IF NOT EXISTS reflections (" +
                "  id   INTEGER PRIMARY KEY AUTOINCREMENT," +
                "  date TEXT NOT NULL UNIQUE," +
                "  text TEXT NOT NULL)")
            .ExecuteNonQuery();
        // Same definition as main.py's ensure_data_store — the DB-level net
        // under the SELECT-first guards, so a cross-process race between the
        // two apps can't double-credit a date.
        _conn.CreateCommand()
            .Also(c => c.CommandText =
                "CREATE UNIQUE INDEX IF NOT EXISTS sl_reason_date " +
                "ON score_ledger(reason, date) " +
                "WHERE reason IN ('daily_score', 'overdue_accrual')")
            .ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();

    // ── day stats (ports of _day_task_counts / _day_diary_minutes) ───────

    public (int Total, int Done) DayTaskCounts(DateOnly d)
    {
        int total = 0, done = 0;
        foreach (var plan in _plans)
        {
            var dayNum = d.DayNumber - plan.StartDateParsed.DayNumber + 1;
            var overrides = _db.LoadOverrides(plan.Id);
            foreach (var phase in plan.Phases)
                foreach (var task in phase.Tasks)
                {
                    var assigned = overrides.TryGetValue(task.Text, out var o) ? o : task.Day;
                    if (assigned != dayNum) continue;
                    total++;
                    if (_completions.TryGetValue((plan.Id, assigned, task.Text), out var c) && c)
                        done++;
                }
        }
        return (total, done);
    }

    public (int OnMin, int OffMin) DayDiaryMinutes(DateOnly d)
    {
        int on = 0, off = 0;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT category, SUM(duration_min) FROM time_diary " +
                          "WHERE date=$d GROUP BY category";
        cmd.Parameters.AddWithValue("$d", d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var cat = r.GetString(0);
            var min = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            if (cat == "on_plan") on = min;
            else if (cat == "off_plan") off = min;
        }
        return (on, off);
    }

    /// <summary>Base formula from config["scoring"], then the v2 floor.</summary>
    public int DayScore(int done, int total, int onMin, int offMin, int streak = 0)
    {
        var raw = done * ConfigService.ScoringRate("task_completed", 10)
                + Math.Max(0, total - done) * ConfigService.ScoringRate("task_overdue_penalty", -5)
                + (int)(onMin / 60.0 * ConfigService.ScoringRate("on_plan_hour", 3))
                + (int)(offMin / 60.0 * ConfigService.ScoringRate("off_plan_hour", -2))
                + streak * ConfigService.ScoringRate("streak_bonus_per_day", 5);
        return Math.Max(raw, DailyFloor);
    }

    public int CurrentStreak()
    {
        var streak = 0;
        for (var i = 1; i <= 7; i++)
        {
            var d = DateOnly.FromDateTime(DateTime.Today).AddDays(-i);
            var (total, done) = DayTaskCounts(d);
            if (total > 0 && done == total) streak++;
            else break;
        }
        return streak;
    }

    // ── ledger (same reasons + once-per-date guards as main.py) ──────────

    private bool LedgerHas(string reason, DateOnly d)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM score_ledger WHERE reason=$r AND date=$d";
        cmd.Parameters.AddWithValue("$r", reason);
        cmd.Parameters.AddWithValue("$d", d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return cmd.ExecuteScalar() != null;
    }

    public void AddLedger(int delta, string reason, string? detail = null, DateOnly? forDate = null)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO score_ledger (ts, date, delta, reason, detail) " +
                          "VALUES ($ts, $d, $delta, $r, $x)";
        cmd.Parameters.AddWithValue("$ts", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$d", (forDate ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$delta", delta);
        cmd.Parameters.AddWithValue("$r", reason);
        cmd.Parameters.AddWithValue("$x", (object?)detail ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public int? CreditDayScoreIfMissing(DateOnly d)
    {
        if (LedgerHas("daily_score", d)) return null;
        var (total, done) = DayTaskCounts(d);
        var (on, off) = DayDiaryMinutes(d);
        var streak = d == DateOnly.FromDateTime(DateTime.Today) ? CurrentStreak() : 0;
        var score = DayScore(done, total, on, off, streak);
        try
        {
            AddLedger(score, "daily_score", $"day score {score}", d);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return null;  // the other app credited this date between check and insert
        }
        return score;
    }

    /// <summary>All overdue tasks as of date d, with how many days overdue each is.</summary>
    public List<(Plan Plan, AssignedTask Task, int DaysOverdue)> OverdueAsOf(DateOnly d)
    {
        var result = new List<(Plan, AssignedTask, int)>();
        foreach (var plan in _plans)
        {
            var dayNum = d.DayNumber - plan.StartDateParsed.DayNumber + 1;
            var overrides = _db.LoadOverrides(plan.Id);
            foreach (var phase in plan.Phases)
                foreach (var task in phase.Tasks)
                {
                    var assigned = overrides.TryGetValue(task.Text, out var o) ? o : task.Day;
                    if (assigned >= dayNum) continue;
                    var done = _completions.TryGetValue((plan.Id, assigned, task.Text), out var c) && c;
                    if (done) continue;
                    result.Add((plan, new AssignedTask
                    {
                        Task = task, OriginalDay = task.Day,
                        AssignedDay = assigned, Overdue = true,
                    }, dayNum - assigned));
                }
        }
        return result;
    }

    /// <summary>
    /// v2 accrual: unlike the Python version, a task only bleeds points for
    /// its first 3 overdue days — after that it's stale and costs nothing
    /// further (the replan flow is the intended way out).
    /// </summary>
    public int? CreditOverdueAccrualIfMissing(DateOnly d)
    {
        if (LedgerHas("overdue_accrual", d)) return null;
        var count = OverdueAsOf(d).Count(x => x.DaysOverdue <= OverdueAccrualCapDays);
        if (count == 0) return 0;
        var delta = count * ConfigService.ScoringRate("task_overdue_penalty", -5);
        try
        {
            AddLedger(delta, "overdue_accrual", $"{count} task(s) still overdue (3-day cap)", d);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return null;  // the other app credited this date between check and insert
        }
        return delta;
    }

    public void EnsureScoreCaughtUp()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 7; i >= 1; i--)
        {
            var d = today.AddDays(-i);
            CreditDayScoreIfMissing(d);
            CreditOverdueAccrualIfMissing(d);
        }
    }

    // ── replan all overdue (the "declare bankruptcy" move) ───────────────

    /// <summary>
    /// Redistributes every overdue task across the coming days (starting
    /// tomorrow), at most ReplanDailyBudgetMin planned minutes per day, for a
    /// single flat fee. Returns the number of tasks moved.
    /// </summary>
    public int ReplanAllOverdue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdue = OverdueAsOf(today).OrderBy(x => x.Task.AssignedDay).ToList();
        if (overdue.Count == 0) return 0;

        foreach (var group in overdue.GroupBy(x => x.Plan.Id))
        {
            var plan = group.First().Plan;
            var day = plan.PlanDay + 1;
            var budget = ReplanDailyBudgetMin;
            foreach (var (_, item, _) in group)
            {
                var dur = item.Task.DurationMin ?? 30;
                if (budget - dur < 0 && budget < ReplanDailyBudgetMin)
                {
                    day++;
                    budget = ReplanDailyBudgetMin;
                }
                SaveOverride(plan.Id, item.Task.Text, item.OriginalDay, day);
                budget -= dur;
            }
        }

        AddLedger(ReplanFlatFee, "replan_overdue",
            $"replanned {overdue.Count} overdue task(s), flat fee");
        return overdue.Count;
    }

    /// <summary>Same upsert as main.py _save_override.</summary>
    private void SaveOverride(string planId, string taskText, int originalDay, int assignedDay)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO task_overrides (plan_id, task_text, original_day, assigned_day) " +
            "VALUES ($pid, $text, $orig, $day) " +
            "ON CONFLICT(plan_id, task_text) DO UPDATE SET " +
            "  original_day=excluded.original_day, assigned_day=excluded.assigned_day";
        cmd.Parameters.AddWithValue("$pid", planId);
        cmd.Parameters.AddWithValue("$text", taskText);
        cmd.Parameters.AddWithValue("$orig", originalDay);
        cmd.Parameters.AddWithValue("$day", assignedDay);
        cmd.ExecuteNonQuery();
    }

    // ── reflections (new, additive table) ────────────────────────────────

    public void SaveReflection(DateOnly d, string text)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO reflections (date, text) VALUES ($d, $t) " +
            "ON CONFLICT(date) DO UPDATE SET text=excluded.text";
        cmd.Parameters.AddWithValue("$d", d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$t", text);
        cmd.ExecuteNonQuery();
    }
}

internal static class ObjectExtensions
{
    public static T Also<T>(this T self, Action<T> block) { block(self); return self; }
}

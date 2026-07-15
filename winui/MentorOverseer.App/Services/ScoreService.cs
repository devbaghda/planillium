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
/// <summary>The day-score formula's individual terms, for display (the
/// evening review's ledger) — see ScoreService.ComputeDayScore, the single
/// place this formula is written.</summary>
public sealed record DayScoreBreakdown(
    int TaskPoints, int MultiTaskBonus, int OnPlanPoints, int OffPlanPoints,
    int MissedPoints, int StreakBonus)
{
    public int RawTotal => TaskPoints + MultiTaskBonus + OnPlanPoints + OffPlanPoints + MissedPoints + StreakBonus;
    public int FlooredTotal => Math.Max(RawTotal, ScoreService.DailyFloor);
}

public sealed class ScoreService : IDisposable
{
    public const int DailyFloor = -10;
    public const int OverdueAccrualCapDays = 3;
    public const int ReplanFlatFee = -10;

    /// <summary>SQLite's "UNIQUE/PRIMARY KEY constraint violated" error code —
    /// what a ledger insert throws when another connection already wrote
    /// today's row first. Named once so the three catch clauses below agree
    /// on what they're actually checking for.</summary>
    private const int SqliteConstraintViolation = 19;

    /// <summary>The "strong day" bar Reports/ReviewDialog/ReportExport all
    /// use to color/celebrate a score — named once so the three copies can't
    /// silently drift out of agreement if it's ever tuned.</summary>
    public const int GreatDayThreshold = 20;

    private readonly List<Plan> _plans;
    private readonly Database _db;
    private readonly Dictionary<(string, int, string), bool> _completions;

    // Same "once per process, not once per construction" reasoning as
    // Database.cs — ScoreService is constructed on nearly every page render
    // and button click, and re-running two schema statements every time was
    // pure avoidable overhead on the UI thread.
    private static bool _schemaEnsured;
    private static readonly object SchemaGate = new();

    public ScoreService(List<Plan> plans, Database db)
    {
        _plans = plans;
        _db = db;
        _completions = db.LoadCompletions();
        // reflections is now created by Database.EnsureSchema (round-5 audit finding #26 —
        // it used to be created only here, a hidden dependency that made "Export all my
        // data" quietly depend on a ScoreService having been constructed first).
        lock (SchemaGate)
        {
            if (!_schemaEnsured)
            {
                // Belt-and-suspenders no-op: Database's constructor (already
                // run via db.LoadCompletions() above) owns creating/migrating
                // this index — see Database.EnsureSchema for the drop-and-
                // recreate logic needed because "CREATE INDEX IF NOT EXISTS"
                // won't widen an existing one. _db.CreateCommand() (not raw
                // _conn.CreateCommand()) so this can't throw if a future
                // caller ever constructs a ScoreService from inside an
                // already-open RunInTransaction block (2026-07-14 round-6
                // audit finding #15 — not reachable today since this only
                // ever runs once per process, gated by _schemaEnsured, but
                // every other command in this class already goes through
                // the wrapper).
                using var indexCmd = _db.CreateCommand();
                indexCmd.CommandText =
                    "CREATE UNIQUE INDEX IF NOT EXISTS sl_reason_date " +
                    "ON score_ledger(reason, date) " +
                    "WHERE reason IN ('daily_score', 'overdue_accrual', 'weekly_comeback_bonus')";
                indexCmd.ExecuteNonQuery();
                _schemaEnsured = true;
            }
        }
    }

    // No-op: this class shares its connection with (owned and disposed by)
    // the Database it was constructed with. Every call site disposes its
    // own Database separately, so this is never the last reference standing.
    public void Dispose() { }

    // ── day stats (ports of _day_task_counts / _day_diary_minutes) ───────

    /// <summary>True if d is a day off for this ONE plan — either a
    /// recurring weekly exclusion or a specific day manually marked off.
    /// Used only to exempt scoring; overdue tasks still display as overdue
    /// in the UI on a day off, they just don't cost anything further that
    /// day. Deliberately distinct from PlanStore.AllPlansExclude — that one
    /// answers "should tracking itself pause today" (all active plans,
    /// recurring exclusions only, no manual override) for a different
    /// purpose; the two rules differ on purpose, don't unify them by
    /// mistake if one is ever renamed or refactored near the other.</summary>
    private bool IsScoringExemptFor(Plan plan, DateOnly d)
    {
        if (plan.IsExcluded(d)) return true;
        return DaysOff(plan.Id).Contains(plan.PlanDayForDate(d));
    }

    public (int Total, int Done) DayTaskCounts(DateOnly d)
    {
        int total = 0, done = 0;
        foreach (var plan in _plans)
        {
            // A day off contributes nothing either way — and for a
            // recurring exclusion specifically, PlanDayForDate resolves an
            // excluded date to the SAME day-number as the last valid day
            // before it, so without this skip an excluded Saturday would
            // silently re-count Friday's already-credited tasks.
            if (IsScoringExemptFor(plan, d)) continue;
            var dayNum = plan.PlanDayForDate(d);
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
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT category, SUM(duration_min) FROM time_diary " +
                          "WHERE date=$d GROUP BY category";
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
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

    /// <summary>Base formula from config["scoring"], then the v2 floor.
    /// Beyond the flat per-task rate, each task completed past the first
    /// one on the same day adds a "multi-task" bonus — rewards a day where
    /// more than one task got done (working ahead included, now that a
    /// pulled-forward task counts as done for the day it was actually
    /// finished on) on top of the linear per-task credit.
    ///
    /// This is a thin wrapper over ComputeDayScore — the single source of
    /// truth for the formula. Kept as its own method (rather than having
    /// every caller unpack a breakdown) because most callers (e.g.
    /// CreditDayScoreIfMissing) only ever need the final number; ReviewDialog
    /// is the one caller that needs the per-term breakdown and calls
    /// ComputeDayScore directly instead of re-deriving these terms itself
    /// (2026-07-09 audit finding #4 — the two had been computed
    /// independently and could silently drift out of sync).</summary>
    public int DayScore(int done, int total, int onMin, int offMin, int streak = 0) =>
        ComputeDayScore(done, total, onMin, offMin, streak).FlooredTotal;

    /// <summary>Same formula as DayScore, broken into its individual terms
    /// for display (the evening review's line-by-line ledger).</summary>
    public DayScoreBreakdown ComputeDayScore(int done, int total, int onMin, int offMin, int streak = 0) =>
        new(
            TaskPoints: done * ConfigService.ScoringRate("task_completed", 10),
            MultiTaskBonus: Math.Max(0, done - 1) * ConfigService.ScoringRate("multi_task_bonus_per_extra_task", 3),
            OnPlanPoints: (int)(onMin / 60.0 * ConfigService.ScoringRate("on_plan_hour", 3)),
            OffPlanPoints: (int)(offMin / 60.0 * ConfigService.ScoringRate("off_plan_hour", -2)),
            MissedPoints: Math.Max(0, total - done) * ConfigService.ScoringRate("task_overdue_penalty", -5),
            StreakBonus: streak * ConfigService.ScoringRate("streak_bonus_per_day", 5));

    public int CurrentStreak()
    {
        var streak = 0;
        for (var i = 1; i <= 7; i++)
        {
            var d = DateOnly.FromDateTime(DateTime.Today).AddDays(-i);
            // A day off doesn't break a streak — skip it rather than
            // treating its (necessarily zero) task count as a miss.
            if (_plans.Any(p => IsScoringExemptFor(p, d))) continue;
            var (total, done) = DayTaskCounts(d);
            if (total > 0 && done == total) streak++;
            else break;
        }
        return streak;
    }

    // ── ledger (same reasons + once-per-date guards as main.py) ──────────

    private bool LedgerHas(string reason, DateOnly d)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM score_ledger WHERE reason=$r AND date=$d";
        cmd.Parameters.AddWithValue("$r", reason);
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        return cmd.ExecuteScalar() != null;
    }

    public void AddLedger(int delta, string reason, string? detail = null, DateOnly? forDate = null)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO score_ledger (ts, date, delta, reason, detail) " +
                          "VALUES ($ts, $d, $delta, $r, $x)";
        cmd.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
        cmd.Parameters.AddWithValue("$d", (forDate ?? DateOnly.FromDateTime(DateTime.Today)).ToIsoDate());
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
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintViolation)
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
            var dayNum = plan.PlanDayForDate(d);
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
        // A day off (recurring exclusion or manually marked) costs nothing —
        // tasks a plan already had overdue don't accrue further that day.
        // They're still shown as overdue everywhere in the UI (OverdueAsOf
        // itself is untouched) and resume accruing the day after.
        var count = OverdueAsOf(d)
            .Where(x => !IsScoringExemptFor(x.Plan, d))
            .Count(x => x.DaysOverdue <= OverdueAccrualCapDays);
        if (count == 0) return 0;
        var delta = count * ConfigService.ScoringRate("task_overdue_penalty", -5);
        try
        {
            AddLedger(delta, "overdue_accrual", $"{count} task(s) still overdue (3-day cap)", d);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintViolation)
        {
            return null;  // the other app credited this date between check and insert
        }
        return delta;
    }

    /// <summary>Catches up to 7 missed days of scoring on launch. Each
    /// individual credit call already self-heals (guarded by its own
    /// UNIQUE-constraint check, so a partial run just leaves the remaining
    /// days to catch up next launch) — wrapped in one transaction anyway for
    /// consistency with every other multi-write sequence in this class, not
    /// because a failure here was ever observed to lose anything
    /// (2026-07-14 round-6 audit finding #17).</summary>
    public void EnsureScoreCaughtUp()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _db.RunInTransaction(() =>
        {
            for (var i = 7; i >= 1; i--)
            {
                var d = today.AddDays(-i);
                CreditDayScoreIfMissing(d);
                CreditOverdueAccrualIfMissing(d);
                CreditWeeklyComebackIfMissing(d);
            }
        });
    }

    // ── weekly comeback bonus ("amplify wins" — recovering from a losing week) ──

    private int SumLedgerRange(DateOnly from, DateOnly to)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(delta), 0) FROM score_ledger WHERE date >= $from AND date <= $to";
        cmd.Parameters.AddWithValue("$from", from.ToIsoDate());
        cmd.Parameters.AddWithValue("$to", to.ToIsoDate());
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Read-only preview (no writes) — a "comeback" is a full calendar week
    /// (Mon-Sun) that closed non-negative, immediately after a full week
    /// that closed negative. Only evaluates anything on a Monday, since
    /// that's the first day both of the weeks it compares are fully closed.
    /// Returns the bonus that would be credited, or 0 if none applies.
    /// </summary>
    public int ComputeWeeklyComeback(DateOnly d)
    {
        if (d.DayOfWeek != DayOfWeek.Monday) return 0;
        var lastWeekStart = d.AddDays(-7);
        var lastWeekEnd = d.AddDays(-1);
        var prevWeekStart = d.AddDays(-14);
        var prevWeekEnd = d.AddDays(-8);
        if (SumLedgerRange(prevWeekStart, prevWeekEnd) >= 0) return 0;
        if (SumLedgerRange(lastWeekStart, lastWeekEnd) < 0) return 0;
        if (LedgerHas("weekly_comeback_bonus", lastWeekStart)) return 0;
        return ConfigService.ScoringRate("weekly_comeback_bonus", 20);
    }

    public int? CreditWeeklyComebackIfMissing(DateOnly d)
    {
        var bonus = ComputeWeeklyComeback(d);
        if (bonus == 0) return null;
        try
        {
            AddLedger(bonus, "weekly_comeback_bonus",
                "recovered from a losing week", d.AddDays(-7));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintViolation)
        {
            return null;  // the other app credited this date between check and insert
        }
        return bonus;
    }

    // ── replan all overdue (the "declare bankruptcy" move) ───────────────

    /// <summary>
    /// Moves every overdue task to the caller-picked day for it — see
    /// Dialogs/ReplanOverdueDialog, the only caller. Each entry goes through
    /// RescheduleTask (so the day it lands on, and everything after, shifts
    /// forward by one instead of doubling up), applied in the order given;
    /// the flat fee then covers the whole batch once, same as the old
    /// automatic version this replaced (2026-07-14 — the user wanted to choose
    /// the days himself instead of an automatic time-budget spread).
    /// </summary>
    public void ReplanOverdueTo(List<(Plan Plan, string TaskText, int OriginalDay, int NewDay)> assignments)
    {
        if (assignments.Count == 0) return;
        // One transaction for the whole batch (RescheduleTask nests safely inside it) — a
        // failure partway through used to leave some tasks re-keyed and others not, or the
        // flat fee charged even though not every reschedule in the batch actually landed.
        _db.RunInTransaction(() =>
        {
            foreach (var (plan, taskText, originalDay, newDay) in assignments)
                RescheduleTask(plan, taskText, originalDay, newDay);
            AddLedger(ReplanFlatFee, "replan_overdue",
                $"replanned {assignments.Count} overdue task(s), flat fee, user-picked days");
        });
    }

    // ── schedule operations (ports of _swap_task_to_today / _mark_day_off) ──

    /// <summary>
    /// Pull a future task to today. If that empties out its old day (no
    /// other tasks left assigned there), every later pending task shifts
    /// back one day to close the gap — finishing something ahead of
    /// schedule should compress the remaining plan, not leave a dead day
    /// sitting in the middle of it. No-op if the task is already due or
    /// overdue. (This replaces an earlier "shift everything between today
    /// and the old slot forward" rule ported from the Python app's
    /// _swap_task_to_today, which assumed one task per day; multiple tasks
    /// per day is normal in the current plan format, so there was never
    /// really an overlap to avoid by pushing other days later.)
    /// Already-completed tasks are never shifted, here or in the backward
    /// compaction — a completion is a historical record of what happened on
    /// a given day, not a schedule slot, and re-shelving it silently
    /// orphaned its task_completions row (keyed by assigned day) so a
    /// finished task on today's day would appear undone and pushed forward.
    /// </summary>
    public void MoveTaskToToday(Plan plan, string taskText)
    {
        var planDay = plan.PlanDay;
        var tasks = PlanStore.TasksFor(plan, _db, _completions);
        var target = tasks.FirstOrDefault(t => t.Task.Text == taskText);
        if (target is null || target.AssignedDay <= planDay) return;

        var oldDay = target.AssignedDay;
        SaveOverride(plan.Id, taskText, target.OriginalDay, planDay);

        var dayNowEmpty = tasks.All(t => t.Task.Text == taskText || t.AssignedDay != oldDay);
        if (dayNowEmpty)
        {
            foreach (var t in tasks)
            {
                if (t.Task.Text == taskText || t.Completed) continue;
                if (t.AssignedDay > oldDay)
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, t.AssignedDay - 1);
            }
        }
    }

    /// <summary>
    /// Move a single overdue task to a specific, user-picked future day.
    /// Whatever was already on that day — and everything after it — shifts
    /// forward by one day first, so the rescheduled task gets its own slot
    /// instead of doubling up with whatever was already there. Already-
    /// completed tasks are excluded from the shift for the same reason as
    /// MoveTaskToToday — see its doc comment. The overdue penalty already
    /// accrued for the days it was late stands; this only stops it from
    /// accruing further.
    ///
    /// Deliberately NOT the same shift-avoidance rule MoveTaskToToday uses
    /// (confirmed with the user 2026-07-09, after an audit flagged the
    /// difference as a possible inconsistency): the two actions represent
    /// different intents. MoveTaskToToday means "I got ahead of schedule" —
    /// pulling work earlier should compress the remaining plan, so it closes
    /// the gap it leaves instead of pushing other days later. RescheduleTask
    /// means "place this specific task on this specific day" — the user's
    /// stated preference is a strict one-task-per-day steady state (a day
    /// holding two tasks should only ever be a transient "I did extra today"
    /// fact, not a permanent state a manual reschedule creates), so this
    /// still needs to push the target day's existing task later rather than
    /// double up on it. Don't "fix" this to match MoveTaskToToday without
    /// re-confirming intent first.
    /// </summary>
    public void RescheduleTask(Plan plan, string taskText, int originalDay, int newAssignedDay)
    {
        // All-or-nothing: a failure partway through the shift loop used to leave some
        // tasks re-keyed to their new day and others not (round-5 audit finding #27).
        _db.RunInTransaction(() =>
        {
            var tasks = PlanStore.TasksFor(plan, _db, _completions);
            foreach (var t in tasks)
            {
                if (t.Task.Text == taskText || t.Completed) continue;
                if (t.AssignedDay >= newAssignedDay)
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, t.AssignedDay + 1);
            }
            SaveOverride(plan.Id, taskText, originalDay, newAssignedDay);
        });
    }

    public HashSet<int> DaysOff(string planId)
    {
        var result = new HashSet<int>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT day FROM plan_days_off WHERE plan_id=$pid";
        cmd.Parameters.AddWithValue("$pid", planId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetInt32(0));
        return result;
    }

    /// <summary>Mark a day as non-working: tasks on or after it shift +1.
    /// Completed tasks are excluded — see MoveTaskToToday's doc comment.</summary>
    public void MarkDayOff(Plan plan, int day)
    {
        _db.RunInTransaction(() =>
        {
            foreach (var t in PlanStore.TasksFor(plan, _db, _completions))
                if (!t.Completed && t.AssignedDay >= day)
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, t.AssignedDay + 1);
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                "INSERT INTO plan_days_off (plan_id, day, marked_at) VALUES ($pid, $day, $ts) " +
                "ON CONFLICT(plan_id, day) DO NOTHING";
            cmd.Parameters.AddWithValue("$pid", plan.Id);
            cmd.Parameters.AddWithValue("$day", day);
            cmd.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
            cmd.ExecuteNonQuery();
        });
    }

    /// <summary>Inverse of MarkDayOff: tasks after the day shift back −1.
    /// Completed tasks are excluded — see MoveTaskToToday's doc comment.</summary>
    public void UnmarkDayOff(Plan plan, int day)
    {
        _db.RunInTransaction(() =>
        {
            foreach (var t in PlanStore.TasksFor(plan, _db, _completions))
                if (!t.Completed && t.AssignedDay > day)
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, t.AssignedDay - 1);
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM plan_days_off WHERE plan_id=$pid AND day=$day";
            cmd.Parameters.AddWithValue("$pid", plan.Id);
            cmd.Parameters.AddWithValue("$day", day);
            cmd.ExecuteNonQuery();
        });
    }

    /// <summary>Same upsert as main.py _save_override.</summary>
    private void SaveOverride(string planId, string taskText, int originalDay, int assignedDay)
    {
        using var cmd = _db.CreateCommand();
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
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO reflections (date, text) VALUES ($d, $t) " +
            "ON CONFLICT(date) DO UPDATE SET text=excluded.text";
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        cmd.Parameters.AddWithValue("$t", text);
        cmd.ExecuteNonQuery();
    }

    public int CountReflections()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM reflections";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Read back a day's reflection, if any — previously written but
    /// never read anywhere in the app (2026-07-09 audit finding #12).</summary>
    public string? LoadReflection(DateOnly d)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT text FROM reflections WHERE date=$d";
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>Deliberately separate from Database.ClearActivityHistory —
    /// reflections are the user's own reflective text, not tracked activity
    /// data (see that method's doc comment), so clearing them is its own
    /// explicit choice, not folded into "clear activity history"
    /// (2026-07-09 audit finding #12: reflections previously had no delete
    /// path anywhere in the app).</summary>
    public void ClearReflections()
    {
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM reflections";
            cmd.ExecuteNonQuery();
        }
        using var vacuum = _db.CreateCommand();
        vacuum.CommandText = "VACUUM";
        vacuum.ExecuteNonQuery();
    }
}

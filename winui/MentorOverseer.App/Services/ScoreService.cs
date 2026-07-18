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

    /// <summary>The "one week" window CurrentStreak, EnsureScoreCaughtUp, and
    /// ComputeWeeklyComeback each used to hardcode as a bare 7/-7 literal — named once
    /// so a future retune can't update three of the four call sites and miss the fourth
    /// (audit finding #21).</summary>
    private const int LookbackDays = 7;

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

    public ScoreService(List<Plan> plans, Database db)
    {
        _plans = plans;
        _db = db;
        _completions = db.LoadCompletions();
        // reflections is now created by Database.EnsureSchema (round-5 audit finding #26 —
        // it used to be created only here, a hidden dependency that made "Export all my
        // data" quietly depend on a ScoreService having been constructed first).
        //
        // This constructor used to also re-issue its own "belt-and-suspenders" copy of the
        // sl_reason_date index-creation SQL — removed (2026-07-18 audit finding R11-05):
        // `db` is a constructor parameter, so Database's own constructor (which
        // unconditionally runs EnsureSchema, the one place that actually knows how to
        // drop-and-recreate this index when its reason list widens — see
        // Database.EnsureSchema) has already run by the time this code executes. The
        // second copy could never fix anything Database.EnsureSchema hadn't already fixed,
        // and unlike that copy, had no drop-and-recreate logic of its own — a real risk if
        // the two copies were ever left to drift (they didn't, but nothing enforced that).
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
    private bool IsScoringExemptFor(Plan plan, DateOnly d) =>
        plan.IsOffOn(d, (planId, day) => DaysOff(planId).Contains(day));

    /// <summary>Whether EVERY active plan is off on d — the gate for "should today's
    /// passive scoring (on/off-plan minutes, missed-task penalty, streak bonus) apply at
    /// all," confirmed with the user 2026-07-17: a day off on one plan while another still
    /// has real work due should still score normally, so this only fires when there's
    /// truly nothing expected of you anywhere. Task-completion points are NOT gated by
    /// this — see ComputeDayScore's isExemptDay parameter — bringing in and finishing a
    /// task on an otherwise-off day still earns its own credit.</summary>
    public bool AllPlansScoringExempt(DateOnly d) => _plans.Count > 0 && _plans.All(p => IsScoringExemptFor(p, d));

    /// <summary>Every date in [from, to] where AllPlansScoringExempt holds — used to keep
    /// Reports' aggregate totals (weekly/monthly/yearly, Time-by-App, distractions)
    /// consistent with the score: day-off time is still tracked and visible in the raw
    /// Diary list, it just doesn't count toward any total (2026-07-17 request). Unlike the
    /// score formula's isExemptDay, there's no task-completion exception here — a
    /// completed task earns its own score credit regardless, but doesn't make that day's
    /// incidental on/off-plan minutes count toward a total.</summary>
    public HashSet<DateOnly> ScoringExemptDates(DateOnly from, DateOnly to)
    {
        var result = new HashSet<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (AllPlansScoringExempt(d)) result.Add(d);
        return result;
    }

    public (int Total, int Done) DayTaskCounts(DateOnly d)
    {
        int total = 0, done = 0;
        foreach (var plan in _plans)
        {
            // Recurring exclusion needs skipping to avoid double-counting: PlanDayForDate
            // resolves an excluded date to the SAME day-number as the last valid day
            // before it, so without this skip an excluded Saturday would silently
            // re-count Friday's already-credited tasks. Manual day-off does NOT skip here
            // (changed 2026-07-17) — its day-number is real and unique (MarkDayOff shifts
            // tasks away from it, it doesn't get reused), so if a task WAS deliberately
            // brought back onto this specific day-off date (e.g. via Move-to-today), it
            // should still be counted and credited rather than silently ignored.
            if (plan.IsExcluded(d)) continue;
            var dayNum = plan.PlanDayForDate(d);
            var overrides = Overrides(plan.Id);
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
            if (cat == DiaryCategory.OnPlan) on = min;
            else if (cat == DiaryCategory.OffPlan) off = min;
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
    public int DayScore(int done, int total, int onMin, int offMin, int streak = 0, bool isExemptDay = false) =>
        ComputeDayScore(done, total, onMin, offMin, streak, isExemptDay).FlooredTotal;

    /// <summary>Same formula as DayScore, broken into its individual terms
    /// for display (the evening review's line-by-line ledger).
    /// <paramref name="isExemptDay"/> (AllPlansScoringExempt — every active plan off,
    /// 2026-07-17 request) suppresses every passive term — on/off-plan minutes, the
    /// missed-task penalty, and the streak bonus — since nothing is actually expected of
    /// you on a day off. TaskPoints/MultiTaskBonus are NOT suppressed: bringing in and
    /// finishing a task on an otherwise-off day still earns its own credit, the one
    /// explicit exception the user asked for.</summary>
    public DayScoreBreakdown ComputeDayScore(int done, int total, int onMin, int offMin, int streak = 0,
        bool isExemptDay = false) =>
        new(
            TaskPoints: done * ConfigService.ScoringRate("task_completed", 10),
            MultiTaskBonus: Math.Max(0, done - 1) * ConfigService.ScoringRate("multi_task_bonus_per_extra_task", 3),
            OnPlanPoints: isExemptDay ? 0 : (int)(onMin / 60.0 * ConfigService.ScoringRate("on_plan_hour", 3)),
            OffPlanPoints: isExemptDay ? 0 : (int)(offMin / 60.0 * ConfigService.ScoringRate("off_plan_hour", -2)),
            MissedPoints: isExemptDay ? 0 : Math.Max(0, total - done) * ConfigService.ScoringRate("task_overdue_penalty", -5),
            StreakBonus: isExemptDay ? 0 : streak * ConfigService.ScoringRate("streak_bonus_per_day", 5));

    /// <summary>Consecutive fully-completed days immediately before <paramref name="asOf"/>
    /// (defaults to today) — how long the streak was *as of that date*, not necessarily the
    /// live streak. Needed because a day's streak bonus is part of its score at the moment
    /// it's first credited; recomputing that same day's score later (RecalculateDayScore,
    /// after an unrelated diary edit) must reproduce the same streak it originally saw, not
    /// today's, or the recalculation silently changes a term the edit had nothing to do with
    /// (2026-07-18 audit finding R8-01).</summary>
    public int CurrentStreak(DateOnly? asOf = null)
    {
        var anchor = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var streak = 0;
        for (var i = 1; i <= LookbackDays; i++)
        {
            var d = anchor.AddDays(-i);
            // A day only doesn't break a streak when EVERY plan is off — matching
            // AllPlansScoringExempt's 2026-07-17 "every plan, not just one" scope; a day
            // off on one plan while another still had real, unfinished work due should
            // still break the streak, since something genuinely was expected of you.
            if (AllPlansScoringExempt(d)) continue;
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
        if (LedgerHas(ScoreReason.DailyScore, d)) return null;
        return RecomputeDayScoreCore(d);
    }

    /// <summary>Recomputes d's daily_score from scratch and overwrites whatever ledger row
    /// is already there — unlike CreditDayScoreIfMissing, which only ever credits a date
    /// once. Needed because editing a diary entry's category after its day was already
    /// scored changes that day's on/off-plan minutes without anything else re-running the
    /// formula (2026-07-17 request) — call this from wherever a diary entry's category can
    /// change (EditDiaryEntryDialog, SplitDiaryEntryDialog, ReportsPage.Diary.MarkSelected).
    /// Only daily_score is affected; overdue_accrual/weekly_comeback_bonus don't depend on
    /// diary categories, so they're untouched.</summary>
    public int RecalculateDayScore(DateOnly d)
    {
        int? result = null;
        _db.RunInTransaction(() =>
        {
            using var del = _db.CreateCommand();
            // Parameterized like every other reason-filtering query in this file (e.g.
            // LedgerHas above) instead of interpolated — ScoreReason.DailyScore is a fixed
            // const today, but interpolating it here was the one place in the file that
            // didn't match that convention (2026-07-18 audit finding R11-15).
            del.CommandText = "DELETE FROM score_ledger WHERE reason=$r AND date=$d";
            del.Parameters.AddWithValue("$r", ScoreReason.DailyScore);
            del.Parameters.AddWithValue("$d", d.ToIsoDate());
            del.ExecuteNonQuery();
            result = RecomputeDayScoreCore(d);
        });
        return result ?? 0;
    }

    /// <summary>Best-effort RecalculateDayScore for one or more dates — a failure here
    /// shouldn't turn an otherwise-successful diary edit into a reported failure, so it's
    /// logged rather than thrown. Centralizes what EditDiaryEntryDialog,
    /// SplitDiaryEntryDialog, and ReportsPage.Diary.MarkSelected each used to hand-roll
    /// independently (2026-07-18 audit finding R8-12).</summary>
    public static void TryRecalculateDayScores(Database db, IEnumerable<DateOnly> dates, string logContext)
    {
        try
        {
            using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
            foreach (var d in dates) score.RecalculateDayScore(d);
        }
        catch (Exception ex)
        {
            Log.Error(logContext, ex);
        }
    }

    private int? RecomputeDayScoreCore(DateOnly d)
    {
        var (total, done) = DayTaskCounts(d);
        var (on, off) = DayDiaryMinutes(d);
        var streak = CurrentStreak(d);
        var isExempt = AllPlansScoringExempt(d);
        var score = DayScore(done, total, on, off, streak, isExempt);
        try
        {
            AddLedger(score, ScoreReason.DailyScore, $"day score {score}" + (isExempt ? " (day off)" : ""), d);
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
            var overrides = Overrides(plan.Id);
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

    /// <summary>How many overdue tasks actually accrue a penalty on d — a day off (recurring
    /// exclusion or manually marked) costs nothing for that plan's already-overdue tasks, and
    /// only the first OverdueAccrualCapDays days of lateness count. Single source of truth for
    /// this count: CreditOverdueAccrualIfMissing (the write path) and ReviewDialog's preview
    /// (the display path) used to compute this independently and could show two different
    /// numbers for the same day when a per-plan exemption was in play (2026-07-18 audit
    /// finding R8-02). They're still shown as overdue everywhere else in the UI (OverdueAsOf
    /// itself is untouched) and resume accruing the day after.</summary>
    public int OverdueAccrualCount(DateOnly d) =>
        OverdueAsOf(d).Where(x => !IsScoringExemptFor(x.Plan, d)).Count(x => x.DaysOverdue <= OverdueAccrualCapDays);

    /// <summary>
    /// v2 accrual: unlike the Python version, a task only bleeds points for
    /// its first 3 overdue days — after that it's stale and costs nothing
    /// further (the replan flow is the intended way out).
    /// </summary>
    public int? CreditOverdueAccrualIfMissing(DateOnly d)
    {
        if (LedgerHas(ScoreReason.OverdueAccrual, d)) return null;
        var count = OverdueAccrualCount(d);
        if (count == 0) return 0;
        var delta = count * ConfigService.ScoringRate("task_overdue_penalty", -5);
        try
        {
            AddLedger(delta, ScoreReason.OverdueAccrual, $"{count} task(s) still overdue (3-day cap)", d);
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
            for (var i = LookbackDays; i >= 1; i--)
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
        var lastWeekStart = d.AddDays(-LookbackDays);
        var lastWeekEnd = d.AddDays(-1);
        var prevWeekStart = d.AddDays(-2 * LookbackDays);
        var prevWeekEnd = d.AddDays(-(LookbackDays + 1));
        if (SumLedgerRange(prevWeekStart, prevWeekEnd) >= 0) return 0;
        if (SumLedgerRange(lastWeekStart, lastWeekEnd) < 0) return 0;
        if (LedgerHas(ScoreReason.WeeklyComebackBonus, lastWeekStart)) return 0;
        return ConfigService.ScoringRate(ScoreReason.WeeklyComebackBonus, 20);
    }

    public int? CreditWeeklyComebackIfMissing(DateOnly d)
    {
        var bonus = ComputeWeeklyComeback(d);
        if (bonus == 0) return null;
        try
        {
            AddLedger(bonus, ScoreReason.WeeklyComebackBonus,
                "recovered from a losing week", d.AddDays(-LookbackDays));
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
    /// the days themselves instead of an automatic time-budget spread).
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
            AddLedger(ReplanFlatFee, ScoreReason.ReplanOverdue,
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

        // All-or-nothing, same as RescheduleTask/MarkDayOff/UnmarkDayOff — this method was
        // the one of the four shift operations still missing it (2026-07-18 audit finding
        // R8-03); a failure partway through the compaction loop below could otherwise leave
        // some tasks re-keyed to their compacted day and others not.
        _db.RunInTransaction(() =>
        {
            SaveOverride(plan.Id, taskText, target.OriginalDay, planDay);

            var dayNowEmpty = tasks.All(t => t.Task.Text == taskText || t.AssignedDay != oldDay);
            if (dayNowEmpty)
            {
                // Compacting back must hop over any day marked off in between —
                // otherwise a plain "-1" can walk a task straight onto a day-off
                // day (making it look occupied) while the day it vacated, which
                // was never off, is left looking like an orphaned gap instead.
                var daysOff = DaysOff(plan.Id);
                foreach (var t in tasks)
                {
                    if (t.Task.Text == taskText || t.Completed) continue;
                    if (t.AssignedDay > oldDay)
                        SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, PrevWorkingDay(t.AssignedDay - 1, daysOff));
                }
            }
        });
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
            var daysOff = DaysOff(plan.Id);
            // Defensive: the date picker doesn't filter out days marked off, so a
            // picked date could land exactly on one — bump to the next working day
            // rather than placing a task on a day meant to hold none.
            newAssignedDay = NextWorkingDay(newAssignedDay, daysOff);

            var tasks = PlanStore.TasksFor(plan, _db, _completions);
            foreach (var t in tasks)
            {
                if (t.Task.Text == taskText || t.Completed) continue;
                if (t.AssignedDay >= newAssignedDay)
                    // Skip over any other day already marked off instead of a flat
                    // "+1" — a naive shift can walk a task straight onto a day-off
                    // day (making it look occupied) while a plain working day
                    // further along is left empty instead (2026-07-16 bug report).
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, NextWorkingDay(t.AssignedDay + 1, daysOff));
            }
            SaveOverride(plan.Id, taskText, originalDay, newAssignedDay);
        });
    }

    // Memoized per plan for this ScoreService instance's lifetime — AllPlansScoringExempt
    // (2026-07-17) can now call this hundreds of times in one Reports render (once per
    // date in a year range), and every instance of this class is short-lived and
    // throwaway (constructed fresh per page render/action), so there's no staleness risk
    // across instances. Callers get a defensive copy, never the cached set itself, so
    // UnmarkDayOff's in-place .Remove() on its own local copy can't corrupt the cache.
    private readonly Dictionary<string, HashSet<int>> _daysOffCache = new();

    public HashSet<int> DaysOff(string planId)
    {
        if (_daysOffCache.TryGetValue(planId, out var cached)) return new HashSet<int>(cached);
        var result = new HashSet<int>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT day FROM plan_days_off WHERE plan_id=$pid";
        cmd.Parameters.AddWithValue("$pid", planId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetInt32(0));
        _daysOffCache[planId] = result;
        return new HashSet<int>(result);
    }

    /// <summary>Mark a day as non-working: tasks on or after it shift forward
    /// to the next working day (skipping over any OTHER day already marked
    /// off, rather than a flat +1 — see NextWorkingDay). Completed tasks are
    /// excluded — see MoveTaskToToday's doc comment.</summary>
    public void MarkDayOff(Plan plan, int day)
    {
        _db.RunInTransaction(() =>
        {
            var daysOff = DaysOff(plan.Id);
            foreach (var t in PlanStore.TasksFor(plan, _db, _completions))
                if (!t.Completed && t.AssignedDay >= day)
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, NextWorkingDay(t.AssignedDay + 1, daysOff));
            using var cmd = _db.CreateCommand();
            cmd.CommandText =
                "INSERT INTO plan_days_off (plan_id, day, marked_at) VALUES ($pid, $day, $ts) " +
                "ON CONFLICT(plan_id, day) DO NOTHING";
            cmd.Parameters.AddWithValue("$pid", plan.Id);
            cmd.Parameters.AddWithValue("$day", day);
            cmd.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
            cmd.ExecuteNonQuery();
            _daysOffCache.Remove(plan.Id);
        });
    }

    /// <summary>Inverse of MarkDayOff: tasks after the day shift back to the
    /// previous working day (skipping over any OTHER day still marked off,
    /// rather than a flat −1 — see PrevWorkingDay), so the task immediately
    /// after lands exactly back on the day being un-marked. Completed tasks
    /// are excluded — see MoveTaskToToday's doc comment.</summary>
    public void UnmarkDayOff(Plan plan, int day)
    {
        _db.RunInTransaction(() =>
        {
            var daysOff = DaysOff(plan.Id);
            daysOff.Remove(day);  // this day is the one being un-marked — no longer a barrier
            foreach (var t in PlanStore.TasksFor(plan, _db, _completions))
                if (!t.Completed && t.AssignedDay > day)
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, PrevWorkingDay(t.AssignedDay - 1, daysOff));
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM plan_days_off WHERE plan_id=$pid AND day=$day";
            cmd.Parameters.AddWithValue("$pid", plan.Id);
            cmd.Parameters.AddWithValue("$day", day);
            cmd.ExecuteNonQuery();
            _daysOffCache.Remove(plan.Id);
        });
    }

    /// <summary>Smallest day >= start that isn't marked off — used whenever a
    /// task shifts forward, so it never lands on a day meant to hold none.</summary>
    private static int NextWorkingDay(int day, HashSet<int> daysOff)
    {
        while (daysOff.Contains(day)) day++;
        return day;
    }

    /// <summary>Largest day &lt;= start that isn't marked off — the backward
    /// counterpart of NextWorkingDay, used when compacting a gap closed.</summary>
    private static int PrevWorkingDay(int day, HashSet<int> daysOff)
    {
        while (daysOff.Contains(day)) day--;
        return day;
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
        // This is the only write path to task_overrides in the whole class — a single
        // invalidation point here keeps Overrides() below correct without needing to
        // remember to invalidate at every one of SaveOverride's several call sites.
        _overridesCache.Remove(planId);
    }

    // Memoized per plan for this ScoreService instance's lifetime — same reasoning as
    // DaysOff above (2026-07-18 audit finding R10-03: DayTaskCounts/OverdueAsOf each
    // re-queried task_overrides fresh on every call, unlike the near-identical DaysOff
    // lookup that already got this treatment in round 7 specifically because
    // AllPlansScoringExempt could call it hundreds of times in one Reports render —
    // DayTaskCounts/OverdueAsOf are called just as often from the same render paths).
    // Callers get a defensive copy, never the cached dictionary itself.
    private readonly Dictionary<string, Dictionary<string, int>> _overridesCache = new();

    private Dictionary<string, int> Overrides(string planId)
    {
        if (_overridesCache.TryGetValue(planId, out var cached)) return new Dictionary<string, int>(cached);
        var result = _db.LoadOverrides(planId);
        _overridesCache[planId] = result;
        return new Dictionary<string, int>(result);
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

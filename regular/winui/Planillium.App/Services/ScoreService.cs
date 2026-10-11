using System.Globalization;
using Microsoft.Data.Sqlite;
using Planillium.App.Models;

namespace Planillium.App.Services;

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
    // The five rules below used to be compile-time consts — the only scoring numbers in the
    // app that weren't editable, while every rate sitting right next to them in the formula
    // (task_completed, on_plan_hour, streak_bonus_per_day…) already came from config.json's
    // "scoring" block. Now they read from that same block, keeping these values as their
    // defaults, so an absent key behaves exactly as before (2026-08-04 request). Properties,
    // not consts: config.json is editable while the app runs, and ConfigService caches/
    // invalidates on its own.

    /// <summary>Floor under a single day's score — a bad day is a setback, not a spiral.</summary>
    public static int DailyFloor => ConfigService.ScoringRate("daily_floor");

    /// <summary>How many days an overdue task keeps accruing its penalty before going
    /// stale. Clamped at 0: a negative would make the accrual window run backwards.</summary>
    public static int OverdueAccrualCapDays =>
        Math.Max(0, ConfigService.ScoringRate("overdue_accrual_cap_days"));

    /// <summary>"Replan all overdue" — one flat fee instead of per-task bleeding.</summary>
    public static int ReplanFlatFee => ConfigService.ScoringRate("replan_flat_fee");

    /// <summary>The "one week" window CurrentStreak, EnsureScoreCaughtUp, and
    /// ComputeWeeklyComeback each used to hardcode as a bare 7/-7 literal — named once
    /// so a future retune can't update three of the four call sites and miss the fourth
    /// (audit finding #21). Clamped to at least 1: a 0 or negative window would make the
    /// catch-up loop below cover no days at all, silently stopping daily scoring.</summary>
    private static int LookbackDays =>
        Math.Max(1, ConfigService.ScoringRate("comeback_lookback_days"));

    /// <summary>SQLite's "UNIQUE/PRIMARY KEY constraint violated" error code —
    /// what a ledger insert throws when another connection already wrote
    /// today's row first. Named once so the three catch clauses below agree
    /// on what they're actually checking for.</summary>
    private const int SqliteConstraintViolation = 19;

    /// <summary>The "strong day" bar Reports/ReviewDialog/ReportExport all
    /// use to color/celebrate a score — named once so the three copies can't
    /// silently drift out of agreement if it's ever tuned.</summary>
    public static int GreatDayThreshold => ConfigService.ScoringRate("great_day_threshold");

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

    /// <summary>Convenience wrapper around <see cref="AllPlansScoringExempt"/> for callers
    /// (KickoffDialog.ShouldShow, ReviewDialog.ShouldOffer) that don't already have a plans
    /// list/Database/ScoreService in scope — those two automatic prompts used to fire on any
    /// day at all, including a recurring rest day or a manually-marked day off, with no
    /// exemption check whatsoever (unlike the late-day-task reminder and the off-plan nag
    /// alert, which both already skip a fully-off day). Fails open (returns false, i.e. "not
    /// exempt") on any error so a transient DB problem can't silently suppress the actual
    /// prompt for the whole day.</summary>
    public static bool AllPlansScoringExemptToday()
    {
        try
        {
            var plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            return score.AllPlansScoringExempt(DateOnly.FromDateTime(DateTime.Today));
        }
        catch (Exception ex)
        {
            Log.Error("ScoreService.AllPlansScoringExemptToday", ex);
            return false;
        }
    }

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

    /// <summary>Distinct calendar dates within [from, to] where the user explicitly marked a
    /// plan day off via SchedulePage's "Day off" button (a plan_days_off row) — deliberately
    /// narrower than <see cref="ScoringExemptDates"/>, which also counts a plan's recurring
    /// weekday rest days (ExcludedWeekdays). Those are an ongoing rule, not something "added"
    /// on any particular occasion, so they're excluded here (2026-08-13 request: totals for
    /// "day-offs ... added by me manually").
    ///
    /// Takes an explicit [from, to] rather than assuming "through today" the way DailyRows does —
    /// a day off is known as soon as it's marked, so a date later in the range is real ahead of
    /// time. ReportData.PeriodStats still chooses to cap `to` at today, deliberately matching every
    /// other figure on the same card rather than giving this one its own, smarter boundary (the
    /// user's own call, 2026-08-13) — that's a caller choice, not a constraint of this method.
    ///
    /// Two plans separately marking the same calendar date off still count once: this answers
    /// "how many days did I take off", not "how many mark-off actions did I take".</summary>
    public HashSet<DateOnly> ManuallyMarkedDaysOff(DateOnly from, DateOnly to)
    {
        var result = new HashSet<DateOnly>();
        foreach (var plan in _plans)
            foreach (var day in DaysOff(plan.Id))
            {
                var d = plan.DateForPlanDay(day);
                if (d >= from && d <= to) result.Add(d);
            }
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
            TaskPoints: done * ConfigService.ScoringRate("task_completed"),
            MultiTaskBonus: Math.Max(0, done - 1) * ConfigService.ScoringRate("multi_task_bonus_per_extra_task"),
            OnPlanPoints: isExemptDay ? 0 : (int)(onMin / 60.0 * ConfigService.ScoringRate("on_plan_hour")),
            OffPlanPoints: isExemptDay ? 0 : (int)(offMin / 60.0 * ConfigService.ScoringRate("off_plan_hour")),
            MissedPoints: isExemptDay ? 0 : Math.Max(0, total - done) * ConfigService.ScoringRate("task_overdue_penalty"),
            StreakBonus: isExemptDay ? 0 : streak * ConfigService.ScoringRate("streak_bonus_per_day"));

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
        // Read once, not per iteration: LookbackDays is a config lookup now, not a const.
        var lookback = LookbackDays;
        for (var i = 1; i <= lookback; i++)
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
                        Task = task,
                        OriginalDay = task.Day,
                        AssignedDay = assigned,
                        Overdue = true,
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
        var delta = count * ConfigService.ScoringRate("task_overdue_penalty");
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
        // Read once, not per iteration — see CurrentStreak.
        var lookback = LookbackDays;
        _db.RunInTransaction(() =>
        {
            for (var i = lookback; i >= 1; i--)
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
        // One read for all four offsets — a config value could otherwise be re-read
        // mid-method and, if edited at exactly the wrong moment, describe two different
        // week lengths inside one comparison.
        var lookback = LookbackDays;
        var lastWeekStart = d.AddDays(-lookback);
        var lastWeekEnd = d.AddDays(-1);
        var prevWeekStart = d.AddDays(-2 * lookback);
        var prevWeekEnd = d.AddDays(-(lookback + 1));
        if (SumLedgerRange(prevWeekStart, prevWeekEnd) >= 0) return 0;
        if (SumLedgerRange(lastWeekStart, lastWeekEnd) < 0) return 0;
        if (LedgerHas(ScoreReason.WeeklyComebackBonus, lastWeekStart)) return 0;
        return ConfigService.ScoringRate(ScoreReason.WeeklyComebackBonus);
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
    /// Originally deliberately NOT the same shift-avoidance rule MoveTaskToToday
    /// uses (confirmed with the user 2026-07-09, after an audit flagged the
    /// difference as a possible inconsistency): MoveTaskToToday's "I got ahead
    /// of schedule" closes the gap it leaves; this one's "place this specific
    /// task on this specific day" only pushed the target day's existing task
    /// later, leaving the vacated day empty. Re-confirmed and reversed
    /// 2026-08-05 (user report: a plan with Sat/Sun already excluded still
    /// showed two consecutive empty weekdays after individual reschedules moved
    /// their tasks elsewhere) — now closes the gap it leaves too, same as
    /// MoveTaskToToday, while still pushing the target day forward so the
    /// moved task never doubles up with whatever's already there. See the
    /// comment on the two-step day computation below for why this can't be two
    /// independent shift loops (they'd fight over any task caught between the
    /// old and new day) and has to be one combined formula per task instead.
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
            var oldDay = tasks.FirstOrDefault(t => t.Task.Text == taskText)?.AssignedDay;

            // Only close the gap if this move actually empties the old day out — a day
            // holding more than one task (a transient "did extra today" state, per
            // MoveTaskToToday's own doc comment) shouldn't compact just because one of
            // its tasks moved elsewhere; the others are still there. Also only for a
            // vacated day that's today or later: ReplanOverdueDialog reuses this same
            // method for tasks whose old day is by definition already overdue/past, and
            // compacting there would pull a currently-future task backward across
            // planDay, silently making it overdue too — renumbering days already lived
            // through, not filling a hole in the upcoming schedule.
            var closeGap = oldDay is { } od && od != newAssignedDay && od >= plan.PlanDay &&
                tasks.All(t => t.Task.Text == taskText || t.AssignedDay != od);

            foreach (var t in tasks)
            {
                if (t.Task.Text == taskText || t.Completed) continue;

                // Step 1 — close the gap: every task after the vacated day shifts back
                // one to fill it (skipping over days already off, same as
                // MoveTaskToToday's PrevWorkingDay compaction).
                var day = closeGap && t.AssignedDay > oldDay!.Value
                    ? PrevWorkingDay(t.AssignedDay - 1, daysOff)
                    : t.AssignedDay;

                // Step 2 — open a slot at the target day: whatever ends up sitting on
                // newAssignedDay after step 1 shifts forward one instead, so the moved
                // task still gets its own day. Chaining off `day` (not the original
                // t.AssignedDay) is what keeps this from double-shifting a task that
                // both compaction and the push would otherwise each want to move —
                // worked through on paper: moving a task later compacts the whole block
                // between old and new day back by one and then only the actual overflow
                // past the new day gets pushed forward again, netting to no change for
                // it; moving a task earlier needs no separate gap-close step at all,
                // since the forward push alone shifts the whole block right by one and
                // lands its last member exactly on the vacated day.
                if (day >= newAssignedDay)
                    day = NextWorkingDay(day + 1, daysOff);

                if (day != t.AssignedDay)
                    // Skip over any other day already marked off instead of a flat
                    // "+1"/"-1" — a naive shift can walk a task straight onto a day-off
                    // day (making it look occupied) while a plain working day further
                    // along is left empty instead (2026-07-16 bug report).
                    SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, day);
            }
            SaveOverride(plan.Id, taskText, originalDay, newAssignedDay);
        });
    }

    /// <summary>
    /// One-time cleanup for gaps that predate 2026-08-05's gap-closing fix — every path that
    /// can create a hole in the *future* schedule (RescheduleTask, MoveTaskToToday, MarkDayOff/
    /// UnmarkDayOff) now closes it itself as it happens, so this exists purely to retroactively
    /// fix up holes that were left behind by RescheduleTask calls made before that fix shipped
    /// (the "two working days with no tasks" bug report). Not wired to any UI — every
    /// gap-creating action is now self-healing going forward, so this should only ever need to
    /// run once per plan, by hand, against real data the user has explicitly confirmed.
    ///
    /// Repeatedly finds the earliest empty, not-marked-off day at or after today that has any
    /// occupied day later than it, and pulls everything after that hole back by one to close it
    /// — same PrevWorkingDay hop-over-days-off logic as every other compaction in this class —
    /// until no such hole remains. A loop rather than one pass because closing one hole can only
    /// ever reveal at most the *next* hole (each pass fully closes the earliest one), not create
    /// a new one, so this always terminates within (number of remaining holes) passes.
    /// </summary>
    public void CompactFutureGaps(Plan plan)
    {
        _db.RunInTransaction(() =>
        {
            var daysOff = DaysOff(plan.Id);
            var planDay = plan.PlanDay;
            while (true)
            {
                var tasks = PlanStore.TasksFor(plan, _db, _completions);
                var movable = tasks.Where(t => !t.Completed && t.AssignedDay >= planDay).ToList();
                if (movable.Count == 0) break;

                // A completed task still anchors its day — it just never moves. Deriving
                // "occupied" from movable alone would treat a completed task's own day as an
                // empty hole to close, which it isn't.
                var occupied = new HashSet<int>(tasks.Select(t => t.AssignedDay));
                var lastDay = movable.Max(t => t.AssignedDay);

                var hole = -1;
                for (var day = planDay; day < lastDay; day++)
                {
                    if (daysOff.Contains(day) || occupied.Contains(day)) continue;
                    hole = day;
                    break;
                }
                if (hole < 0) break;  // no gap left before the last occupied day — done

                foreach (var t in movable)
                    if (t.AssignedDay > hole)
                        SaveOverride(plan.Id, t.Task.Text, t.OriginalDay, PrevWorkingDay(t.AssignedDay - 1, daysOff));
            }
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

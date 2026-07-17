using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MentorOverseer.App.Services;

public enum ReportPeriod { Day, Week, Month, Year }

/// <summary>
/// Period-aware report queries — port of main.py's _week_stats /
/// _period_table_rows / _waste_patterns / _app_time_breakdown. Shared by
/// ReportsPage and the HTML/CSV exports so all three agree on the numbers.
/// Distractions and the app breakdown group by AppNames labels, so ten
/// YouTube tabs read as one "Chrome - YouTube" row.
/// </summary>
public static class ReportData
{
    public sealed record DayStat(DateOnly Date, int Done, int Total, int OnMin, int OffMin, int Score);
    public sealed record BucketStat(string Label, int OnMin, int OffMin);

    public sealed class AppUsage
    {
        public int Total, On, Off, Neutral, Paid, Idle;
        public SortedDictionary<string, AppUsage>? Subs;

        public void Add(string category, int mins)
        {
            Total += mins;
            switch (category)
            {
                case DiaryCategory.OnPlan: On += mins; break;
                case DiaryCategory.OffPlan: Off += mins; break;
                case DiaryCategory.Neutral: Neutral += mins; break;
                case DiaryCategory.Paid: Paid += mins; break;
                // Previously uncounted here — Total (and so the row's minutes label)
                // included idle time, but no bucket did, so an idle-heavy row's bar
                // visibly fell short of its own label with no explanation why
                // (round-5 audit finding #22).
                case DiaryCategory.Idle: Idle += mins; break;
            }
        }
    }

    public static string PeriodName(ReportPeriod p) => p switch
    {
        ReportPeriod.Day => "TODAY",
        ReportPeriod.Month => "THIS MONTH",
        ReportPeriod.Year => "THIS YEAR",
        _ => "THIS WEEK",
    };

    /// <summary>
    /// This calendar week so far — Monday through today, oldest first —
    /// backing the day/week summary table. Future days of the week are
    /// omitted (they'd be empty rows); the last element is always today, so
    /// callers that want "today" can take <c>[^1]</c>.
    /// </summary>
    public static List<DayStat> WeekStats(ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var streak = score.CurrentStreak();
        var rows = new List<DayStat>();
        for (var d = MondayOf(today); d <= today; d = d.AddDays(1))
        {
            var (total, done) = score.DayTaskCounts(d);
            var (on, off) = score.DayDiaryMinutes(d);
            var isExempt = score.AllPlansScoringExempt(d);
            var s = score.DayScore(done, total, on, off, d == today ? streak : 0, isExempt);
            // A day off is still tracked in the raw Diary list, but its on/off-plan
            // minutes don't count toward this summary table any more than they count
            // toward the score (2026-07-17 request).
            if (isExempt) { on = 0; off = 0; }
            rows.Add(new DayStat(d, done, total, on, off, s));
        }
        return rows;
    }

    /// <summary>Week buckets within the current calendar month — only ever called for
    /// ReportPeriod.Month (both call sites branch Day/Week away before reaching this).
    /// Split from the old combined Buckets(period, conn) — two genuinely unrelated
    /// queries (month-as-weeks vs year-as-months) living in one 82-line method with a
    /// period-check branch was exactly the "two unrelated code paths in one function"
    /// shape the code-quality checklist flags (audit finding #5).</summary>
    public static List<BucketStat> MonthBuckets(SqliteConnection conn, ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = PeriodStart(ReportPeriod.Month, today);
        // Ordered week buckets keyed by the Monday of each week.
        var weeks = new SortedDictionary<string, (DateOnly WeekStart, int On, int Off)>();
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            var ws = MondayOf(d);
            weeks.TryAdd(ws.ToIsoDate(), (ws, 0, 0));
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT date, category, SUM(duration_min) FROM time_diary " +
            "WHERE date >= $from GROUP BY date, category";
        cmd.Parameters.AddWithValue("$from", start.ToIsoDate());
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!DateOnly.TryParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d)) continue;
            // Tracked (still visible in the raw Diary list) but doesn't count toward this
            // bucket, same as the score and the weekly summary table (2026-07-17 request).
            if (score.AllPlansScoringExempt(d)) continue;
            var cat = r.GetString(1);
            if (cat != DiaryCategory.OnPlan && cat != DiaryCategory.OffPlan) continue;
            var ws = MondayOf(d);
            var key = ws.ToIsoDate();
            if (!weeks.TryGetValue(key, out var w)) continue;
            var mins = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            weeks[key] = cat == DiaryCategory.OnPlan ? (w.WeekStart, w.On + mins, w.Off)
                                          : (w.WeekStart, w.On, w.Off + mins);
        }
        return weeks.Values.Select(w => new BucketStat(
            $"{w.WeekStart.ToString("dd MMM", CultureInfo.InvariantCulture)} – " +
            $"{w.WeekStart.AddDays(6).ToString("dd MMM", CultureInfo.InvariantCulture)}",
            w.On, w.Off)).ToList();
    }

    /// <summary>Calendar-month buckets for this year, January through the current
    /// month — only ever called for ReportPeriod.Year (see MonthBuckets' doc comment
    /// for why this was split out of it). Raw time_diary only covers the diary's
    /// configured retention window (ConfigService.DiaryRetentionDays();
    /// Database.DiaryRetentionDays is only its fallback default), so earlier months of
    /// the year come from diary_daily_rollup instead — the two never overlap (a date
    /// only gets a rollup row once its raw rows are pruned), so summing both sources
    /// is safe.</summary>
    public static List<BucketStat> YearBuckets(SqliteConnection conn, ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromStr = PeriodStart(ReportPeriod.Year, today).ToIsoDate();
        var months = new SortedDictionary<string, (int On, int Off)>();

        // Per-date (not per-month) so a day-off date can be excluded before it's folded
        // into its month — same rule as the score and the other bucket views
        // (2026-07-17 request). Both sources (raw time_diary and the older rollup rows)
        // go through this one local function so neither can drift from the other.
        void AddToMonth(DateOnly d, int onMins, int offMins)
        {
            if (score.AllPlansScoringExempt(d)) return;
            var mo = d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            months.TryGetValue(mo, out var m);
            months[mo] = (m.On + onMins, m.Off + offMins);
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT date, category, SUM(duration_min) FROM time_diary " +
            "WHERE date >= $from GROUP BY date, category";
        cmd.Parameters.AddWithValue("$from", fromStr);
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                if (!DateOnly.TryParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var d)) continue;
                var cat = r.GetString(1);
                if (cat != DiaryCategory.OnPlan && cat != DiaryCategory.OffPlan) continue;
                var mins = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                AddToMonth(d, cat == DiaryCategory.OnPlan ? mins : 0, cat == DiaryCategory.OffPlan ? mins : 0);
            }

        using var rollupCmd = conn.CreateCommand();
        rollupCmd.CommandText =
            "SELECT date, on_min, off_min FROM diary_daily_rollup WHERE date >= $from";
        rollupCmd.Parameters.AddWithValue("$from", fromStr);
        using (var r = rollupCmd.ExecuteReader())
            while (r.Read())
            {
                if (!DateOnly.TryParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var d)) continue;
                var on = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                var off = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                AddToMonth(d, on, off);
            }

        return months.Select(kv => new BucketStat(MonthLabel(kv.Key), kv.Value.On, kv.Value.Off))
                     .ToList();
    }

    private static string MonthLabel(string yyyyMm) =>
        DateTime.TryParseExact(yyyyMm, "yyyy-MM", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d.ToString("MMMM yyyy", CultureInfo.InvariantCulture) : yyyyMm;

    /// <summary>
    /// "idle" is a placeholder window, not a real app — once the user gives
    /// an idle row a description (renames it in the diary editor), that
    /// description becomes its identity for grouping purposes, so it shows
    /// as its own line in Time-by-App/Top Distractions instead of staying
    /// lumped under generic "idle" (whose own total correspondingly shrinks
    /// as renamed rows move out of it).
    /// </summary>
    private static string EffectiveWindow(string window, string? description) =>
        window == DiaryCategory.Idle && description is { Length: > 0 } ? description : window;

    /// <summary>Off-plan minutes grouped by "App - sub" label, biggest first.</summary>
    public static List<(string Label, int Minutes)> TopDistractions(ReportPeriod period,
        SqliteConnection conn, ScoreService score, int limit = 8)
    {
        var aggregated = new Dictionary<string, int>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT date, window, description, SUM(duration_min) FROM time_diary " +
            "WHERE category='off_plan' AND " + DateFilter(period, cmd) +
            " GROUP BY date, window, description";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            // Grouping by date too (not just window/description) lets a day-off date be
            // excluded here, same as every other Reports total (2026-07-17 request) —
            // still tracked in the raw Diary list, it just doesn't count toward this.
            if (!DateOnly.TryParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d) || score.AllPlansScoringExempt(d)) continue;
            var window = r.GetString(1);
            var desc = r.IsDBNull(2) ? null : r.GetString(2);
            var label = AppNames.Label(EffectiveWindow(window, desc));
            var mins = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            aggregated[label] = aggregated.GetValueOrDefault(label) + mins;
        }
        return aggregated.OrderByDescending(kv => kv.Value)
                         .Take(limit)
                         .Select(kv => (kv.Key, kv.Value))
                         .ToList();
    }

    /// <summary>All time grouped app → sub-item, biggest app first.</summary>
    public static List<(string App, AppUsage Usage)> AppBreakdown(ReportPeriod period,
        SqliteConnection conn, ScoreService score, int limit = 12)
    {
        var groups = new Dictionary<string, AppUsage>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT date, window, category, description, SUM(duration_min) FROM time_diary " +
            "WHERE " + DateFilter(period, cmd) + " GROUP BY date, window, category, description";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            // Same day-off exclusion as TopDistractions above (2026-07-17 request).
            if (!DateOnly.TryParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d) || score.AllPlansScoringExempt(d)) continue;
            var window = r.GetString(1);
            var cat = r.GetString(2);
            var desc = r.IsDBNull(3) ? null : r.GetString(3);
            var mins = r.IsDBNull(4) ? 0 : r.GetInt32(4);
            var effective = EffectiveWindow(window, desc);
            var grp = AppNames.Group(effective);
            if (!groups.TryGetValue(grp, out var g))
                groups[grp] = g = new AppUsage { Subs = new SortedDictionary<string, AppUsage>() };
            g.Add(cat, mins);
            if (AppNames.Sub(effective) is { Length: > 0 } sub)
            {
                if (!g.Subs!.TryGetValue(sub, out var s))
                    g.Subs[sub] = s = new AppUsage();
                s.Add(cat, mins);
            }
        }
        return groups.OrderByDescending(kv => kv.Value.Total)
                     .Take(limit)
                     .Select(kv => (kv.Key, kv.Value))
                     .ToList();
    }

    public sealed record DiaryEntry(long Id, DateOnly Date, string Start, string End,
        int Dur, string Cat, string Window, string? Desc);

    /// <summary>
    /// Raw time_diary rows in a date range, newest first — backs the diary
    /// search/list view. Deliberately opens its own short-lived connection
    /// (unlike Buckets/TopDistractions/AppBreakdown, which now take the
    /// caller's) — this is called from RenderDiaryResults, a closure that
    /// outlives ReportsPage.Render's own `db` (every keystroke in the
    /// search box, every mark-selected/select-all click fires it again,
    /// long after Render has returned and disposed its connection).
    /// Microsoft.Data.Sqlite pools connections by default, so repeated
    /// Open/Dispose cycles here are cheap — reusing a connection would
    /// require holding one open for the page's whole lifetime instead.
    /// </summary>
    public static List<DiaryEntry> DiaryInRange(DateOnly from, DateOnly to)
    {
        using var conn = AppPaths.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, date, start_time, end_time, duration_min, category, window, description " +
            "FROM time_diary WHERE date BETWEEN $from AND $to ORDER BY date DESC, start_time DESC";
        cmd.Parameters.AddWithValue("$from", from.ToIsoDate());
        cmd.Parameters.AddWithValue("$to", to.ToIsoDate());
        var result = new List<DiaryEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!DateOnly.TryParseExact(r.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d)) continue;
            result.Add(new DiaryEntry(r.GetInt64(0), d, r.GetString(2), r.GetString(3), r.GetInt32(4),
                r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)));
        }
        return result;
    }

    public static string FmtMins(int mins) =>
        mins >= 60 ? $"{mins / 60}h {mins % 60:00}m" : $"{mins}m";

    /// <summary>Monday of the week containing <paramref name="d"/>.</summary>
    internal static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>
    /// First day of the calendar period containing today — Monday of this
    /// week, the 1st of this month, or 1 January of this year. Periods are
    /// *calendar* windows ("this week/month/year"), not rolling look-backs
    /// ("the last 7/30/365 days"), so a Monday report shows only Monday's
    /// data rather than dragging in the tail of the previous week.
    /// </summary>
    internal static DateOnly PeriodStart(ReportPeriod period, DateOnly today) => period switch
    {
        ReportPeriod.Day => today,
        ReportPeriod.Month => new DateOnly(today.Year, today.Month, 1),
        ReportPeriod.Year => new DateOnly(today.Year, 1, 1),
        _ => MondayOf(today),
    };

    /// <summary>SQL date predicate for the period; adds its parameter to cmd.</summary>
    private static string DateFilter(ReportPeriod period, SqliteCommand cmd)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (period == ReportPeriod.Day)
        {
            cmd.Parameters.AddWithValue("$pd",
                today.ToIsoDate());
            return "date = $pd";
        }
        cmd.Parameters.AddWithValue("$pd",
            PeriodStart(period, today).ToIsoDate());
        return "date >= $pd";
    }
}

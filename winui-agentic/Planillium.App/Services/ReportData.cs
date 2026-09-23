using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

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
    /// <summary>One date's (or bucket's) minutes split across all five diary categories.
    /// <see cref="TotalMin"/> is every category summed — all the tracked time — which is a
    /// deliberately larger figure than the on-plan + off-plan pair the summary tables used to
    /// total (2026-08-05 request, "add all the categories and summary for the rows as well").</summary>
    public sealed record CategoryMinutes(int On, int Off, int Neutral, int Paid, int Idle)
    {
        public static readonly CategoryMinutes Zero = new(0, 0, 0, 0, 0);
        public int TotalMin => On + Off + Neutral + Paid + Idle;

        public CategoryMinutes Plus(CategoryMinutes o) =>
            new(On + o.On, Off + o.Off, Neutral + o.Neutral, Paid + o.Paid, Idle + o.Idle);

        /// <summary>The minutes for one category name, or 0 for anything that isn't one of the
        /// five — so an unrecognised `time_diary.category` can never be silently folded into a
        /// total under a category it doesn't belong to.</summary>
        public int Of(string category) => category switch
        {
            DiaryCategory.OnPlan => On,
            DiaryCategory.OffPlan => Off,
            DiaryCategory.Neutral => Neutral,
            DiaryCategory.Paid => Paid,
            DiaryCategory.Idle => Idle,
            _ => 0,
        };
    }

    public sealed record DayStat(DateOnly Date, int Done, int Total, CategoryMinutes Minutes,
        int Score, bool IsDayOff)
    {
        public int OnMin => Minutes.On;
        public int OffMin => Minutes.Off;
    }

    public sealed record BucketStat(string Label, CategoryMinutes Minutes, int Score, int Done, int Total)
    {
        public int OnMin => Minutes.On;
        public int OffMin => Minutes.Off;
    }

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
    public static List<DayStat> WeekStats(SqliteConnection conn, ScoreService score) =>
        [.. DailyRows(conn, score, MondayOf(DateOnly.FromDateTime(DateTime.Today)))
             .Select(r => new DayStat(r.Date, r.Done, r.Total, r.Minutes, r.Score, r.IsExempt))];

    /// <summary>
    /// Every date from <paramref name="from"/> through today, once, with that date's minutes,
    /// task counts and score. The single place any Reports figure comes from: the week table,
    /// the month/year buckets and the score card all fold this same sequence, so the score in a
    /// table can't disagree with the score on the card a few pixels above it — which is this
    /// project's most-repeated complaint shape (consolidated 2026-08-05; before that, four
    /// separate walks each re-derived their own numbers).
    ///
    /// Two rules are baked in here rather than repeated at each caller, because they used to be:
    ///   * A day off contributes NO minutes — tracked, still in the raw diary, but excluded from
    ///     every Reports total the same way it's excluded from scoring (2026-07-17 request).
    ///   * A day off DOES contribute its score: a task genuinely completed on a day off still
    ///     earns its credit. The bucket tables previously skipped exempt dates outright, so once
    ///     they gained a score column they would have quietly disagreed with the card.
    /// The streak is each day's own as-of-that-date value, not today's — the week table used to
    /// pass a hardcoded 0, understating a past day mid-streak (2026-07-18 audit finding R8-01).
    /// </summary>
    private static IEnumerable<(DateOnly Date, CategoryMinutes Minutes, int Score, int Done,
        int Total, bool IsExempt)> DailyRows(SqliteConnection conn, ScoreService score, DateOnly from)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // One pair of queries for the whole range rather than per-day round trips: on the Year
        // view that would be 365 of them, and Reports has a slow-load report in its history.
        var minutes = DailyMinutes(conn, from);
        for (var d = from; d <= today; d = d.AddDays(1))
        {
            var (total, done) = score.DayTaskCounts(d);
            var m = minutes.TryGetValue(d, out var found) ? found : CategoryMinutes.Zero;
            var isExempt = score.AllPlansScoringExempt(d);
            var s = score.DayScore(done, total, m.On, m.Off, score.CurrentStreak(d), isExempt);
            yield return (d, isExempt ? CategoryMinutes.Zero : m, s, done, total, isExempt);
        }
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
        return Bucket(conn, score, start,
            d => MondayOf(d).ToIsoDate(),
            key => {
                var ws = DateOnly.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                return $"{ws.ToString("dd MMM", CultureInfo.InvariantCulture)} – " +
                       $"{ws.AddDays(6).ToString("dd MMM", CultureInfo.InvariantCulture)}";
            },
            // Every week of the month gets a row, even one with no activity, so the month's
            // shape stays visible.
            keepEmpty: true);
    }

    /// <summary>Folds <see cref="DailyRows"/> into labelled buckets — the one implementation
    /// behind both bucket tables, which were near-identical apart from how a date maps to a
    /// bucket and how that bucket is labelled.</summary>
    private static List<BucketStat> Bucket(SqliteConnection conn, ScoreService score, DateOnly from,
        Func<DateOnly, string> keyOf, Func<string, string> labelOf, bool keepEmpty)
    {
        var buckets = new SortedDictionary<string, BucketStat>();
        foreach (var r in DailyRows(conn, score, from))
        {
            var key = keyOf(r.Date);
            var b = buckets.TryGetValue(key, out var cur)
                ? cur : new BucketStat(labelOf(key), CategoryMinutes.Zero, 0, 0, 0);
            buckets[key] = b with
            {
                Minutes = b.Minutes.Plus(r.Minutes),
                Score = b.Score + r.Score,
                Done = b.Done + r.Done,
                Total = b.Total + r.Total,
            };
        }
        // The Year view drops months with nothing in them at all: a fresh install would
        // otherwise render eleven empty rows, which reads as broken rather than as simply
        // empty. The Month view keeps its empty weeks, so the month's shape stays visible.
        return [.. buckets.Values.Where(b => keepEmpty ||
            b.Minutes.TotalMin > 0 || b.Score != 0 || b.Total > 0)];
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
        // Per-date (not per-month) so a day-off date is handled before it's folded into its
        // month — same rule as the score and the other views (2026-07-17 request), now applied
        // once inside DailyRows rather than repeated here.
        return Bucket(conn, score, PeriodStart(ReportPeriod.Year, today),
            d => d.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            MonthLabel, keepEmpty: false);
    }

    /// <summary>Everything the score card and the insights panel need, aggregated over the
    /// selected period instead of over today/this-week (2026-08-04 request: the whole page above
    /// the diary should follow the Day/Week/Month/Year selector — DayOffs joined the rest of this
    /// record 2026-08-13 for the same reason, after first shipping as its own always-three-numbers
    /// card, which the user then corrected: "everything besides the diary should update based on
    /// the chosen timescale... the same about the day-offs statistics", "we do not need an
    /// additional card for it"). Score is the sum of each day's own score — <b>points earned in
    /// the period</b>, which is a different figure from the sidebar's running balance, since that
    /// also nets off entertainment purchases.</summary>
    public sealed record PeriodTotals(int Score, int Done, int Total, int OnMin, int OffMin, int DayOffs);

    /// <summary>
    /// Per-date on/off-plan minutes across [from, today], from both sources: raw `time_diary`,
    /// plus `diary_daily_rollup` for dates whose per-entry detail has already aged out of the
    /// retention window. The two never overlap (a date only gets a rollup row once its raw rows
    /// are pruned), so summing both is safe — the same reasoning YearBuckets documents.
    ///
    /// Two queries for the whole period rather than one per day: ScoreService.DayDiaryMinutes
    /// would be 365 round-trips on the Year view, and Reports has a slow-load report in its
    /// history already (2026-07-21).
    /// </summary>
    private static Dictionary<DateOnly, CategoryMinutes> DailyMinutes(SqliteConnection conn, DateOnly from)
    {
        var byDate = new Dictionary<DateOnly, CategoryMinutes>();
        void Add(DateOnly d, CategoryMinutes m) =>
            byDate[d] = (byDate.TryGetValue(d, out var cur) ? cur : CategoryMinutes.Zero).Plus(m);

        var fromStr = from.ToIsoDate();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT date, category, SUM(duration_min) FROM time_diary " +
            "WHERE date >= $from GROUP BY date, category";
        cmd.Parameters.AddWithValue("$from", fromStr);
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                if (!r.GetString(0).TryParseIsoDate(out var d)) continue;
                var mins = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                // Explicitly per known category, so a category string that isn't one of the
                // five is dropped rather than landing in some total by accident.
                Add(d, r.GetString(1) switch
                {
                    DiaryCategory.OnPlan => new CategoryMinutes(mins, 0, 0, 0, 0),
                    DiaryCategory.OffPlan => new CategoryMinutes(0, mins, 0, 0, 0),
                    DiaryCategory.Neutral => new CategoryMinutes(0, 0, mins, 0, 0),
                    DiaryCategory.Paid => new CategoryMinutes(0, 0, 0, mins, 0),
                    DiaryCategory.Idle => new CategoryMinutes(0, 0, 0, 0, mins),
                    _ => CategoryMinutes.Zero,
                });
            }

        using var rollupCmd = conn.CreateCommand();
        rollupCmd.CommandText =
            "SELECT date, on_min, off_min, neutral_min, paid_min, idle_min " +
            "FROM diary_daily_rollup WHERE date >= $from";
        rollupCmd.Parameters.AddWithValue("$from", fromStr);
        using (var r = rollupCmd.ExecuteReader())
            while (r.Read())
            {
                if (!r.GetString(0).TryParseIsoDate(out var d)) continue;
                int Col(int i) => r.IsDBNull(i) ? 0 : r.GetInt32(i);
                // The rollup has carried all five columns since it was created — only on/off
                // were ever read back, so an aged-out month silently lost its neutral/paid/idle
                // detail in the Year view even though the numbers were sitting right there.
                Add(d, new CategoryMinutes(Col(1), Col(2), Col(3), Col(4), Col(5)));
            }
        return byDate;
    }

    /// <summary>
    /// Task counts, on/off-plan minutes, score and manually-marked day-offs summed over the
    /// selected period. Day-off dates contribute no minutes (same rule as every other Reports
    /// total, 2026-07-17) but their score is still included, because a task genuinely completed on
    /// a day off still earns its credit.
    ///
    /// Scores are recomputed per day rather than read back from `score_ledger` deliberately: the
    /// ledger only holds days the app was running to credit, so a stretch where it wasn't open
    /// would silently read as zero rather than as the score those days actually earned. Task
    /// counts and streaks are in-memory lookups, so the per-day loop stays cheap even over a year.
    ///
    /// DayOffs is bounded the same as everything else here — period start through today, never
    /// into the future — even though a day off can legitimately be marked ahead of time for a date
    /// later in the period. Deliberate: the user asked for this to follow the selector exactly like
    /// every other figure on this card, not to invent its own, smarter boundary.
    /// </summary>
    public static PeriodTotals PeriodStats(ReportPeriod period, SqliteConnection conn, ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periodStart = PeriodStart(period, today);
        int scoreSum = 0, done = 0, total = 0, onSum = 0, offSum = 0;
        // The same sequence the tables below the card fold — see DailyRows for the two day-off
        // rules that used to be restated at each of these call sites.
        foreach (var r in DailyRows(conn, score, periodStart))
        {
            scoreSum += r.Score;
            total += r.Total;
            done += r.Done;
            onSum += r.Minutes.On;
            offSum += r.Minutes.Off;
        }
        var dayOffs = score.ManuallyMarkedDaysOff(periodStart, today).Count;
        return new PeriodTotals(scoreSum, done, total, onSum, offSum, dayOffs);
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
            $"WHERE category='{DiaryCategory.OffPlan}' AND " + DateFilter(period, cmd) +
            " GROUP BY date, window, description";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            // Grouping by date too (not just window/description) lets a day-off date be
            // excluded here, same as every other Reports total (2026-07-17 request) —
            // still tracked in the raw Diary list, it just doesn't count toward this.
            if (!r.GetString(0).TryParseIsoDate(out var d) || score.AllPlansScoringExempt(d)) continue;
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
            if (!r.GetString(0).TryParseIsoDate(out var d) || score.AllPlansScoringExempt(d)) continue;
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
        int Dur, string Cat, string Window, string? Desc, string? Tag);

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
            "SELECT id, date, start_time, end_time, duration_min, category, window, description, tag " +
            "FROM time_diary WHERE date BETWEEN $from AND $to ORDER BY date DESC, start_time DESC";
        cmd.Parameters.AddWithValue("$from", from.ToIsoDate());
        cmd.Parameters.AddWithValue("$to", to.ToIsoDate());
        var result = new List<DiaryEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!r.GetString(1).TryParseIsoDate(out var d)) continue;
            result.Add(new DiaryEntry(r.GetInt64(0), d, r.GetString(2), r.GetString(3), r.GetInt32(4),
                r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8)));
        }
        return result;
    }

    /// <summary>
    /// Minutes as decimal hours — "5,5 h", not "5h 30m" (2026-08-05 request). Everything on
    /// Reports above the diary reads through here; the diary keeps its own hours-and-minutes
    /// formatter deliberately, since a single entry is a clock event ("14:05–14:20, 15m") where a
    /// column of durations is a quantity you compare and add up.
    ///
    /// One decimal, and the separator is the user's own (CurrentCulture) — the request was
    /// written as "5,5". Rounding is per figure, so a column of rounded rows can differ from its
    /// rounded total by 0.1: the underlying minutes are summed first and only the result is
    /// rounded, which is the honest way round — the total is exact and the rows are what's
    /// approximate, rather than a total that visibly disagrees with the arithmetic behind it.
    /// </summary>
    public static string FmtHours(int mins) =>
        (mins / 60.0).ToString("0.#", CultureInfo.CurrentCulture) + " h";

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

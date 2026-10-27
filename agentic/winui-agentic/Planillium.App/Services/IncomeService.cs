using Planillium.App.Models;

namespace Planillium.App.Services;

/// <summary>
/// Ledger-based loss/gain-of-income tracker — mirrors ScoreService's ledger+catch-up shape,
/// posting one row per already-closed calendar day with a fixed delta and employed flag.
/// </summary>
public sealed class IncomeService : IDisposable
{
    /// <summary>Working hours per month: 21 days × 8 h. User decision 2026-10-01.</summary>
    public const int WorkingHoursPerMonth = 168;

    private readonly Database _db;

    public IncomeService(Database db)
    {
        _db = db;
    }

    // No-op: this class shares its connection with (owned and disposed by)
    // the Database it was constructed with. Every call site disposes its
    // own Database separately, so this is never the last reference standing.
    public void Dispose() { }

    /// <summary>Fixed hourly rate derived from monthly income: potential monthly EUR divided by
    /// working hours per month (168). Constant per config; used for per-row EUR in distraction list
    /// and the income card's per-hour line.</summary>
    public static double HourValueEur() => ConfigService.PotentialMonthlyIncomeEur() / WorkingHoursPerMonth;

    /// <summary>Daily share of the configured potential monthly income. Computed fresh each
    /// time to respond to config changes, but applied only to past days that haven't posted yet.</summary>
    private double DailyRate(DateOnly d)
    {
        return ConfigService.PotentialMonthlyIncomeEur() / DateTime.DaysInMonth(d.Year, d.Month);
    }

    /// <summary>Backfill the income ledger from the most recent posted date (or 2025-12-04 if
    /// the table is empty) through yesterday, one transaction. Each day posts with the
    /// employment status and monthly-income config as they read at the moment this catch-up
    /// pass runs — no retroactive recomputation of past days. Wraps in RunInTransaction so
    /// all days are either posted together or the whole pass rolls back.</summary>
    public void EnsureIncomeCaughtUp()
    {
        const string StartDate = "2025-12-04";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        _db.RunInTransaction(() =>
        {
            // Find the last posted date, or start from 2025-12-04 if the table is empty
            DateOnly start;
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "SELECT MAX(date) FROM income_ledger";
                var lastDateStr = cmd.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(lastDateStr))
                {
                    start = DateOnly.ParseExact(StartDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    start = DateOnly.ParseExact(lastDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).AddDays(1);
                }
            }

            // Post each day from start through yesterday
            for (var d = start; d <= yesterday; d = d.AddDays(1))
            {
                var employed = ConfigService.IsEmployed() ? 1 : 0;
                var dailyRate = DailyRate(d);
                var delta = employed == 1 ? dailyRate : -dailyRate;
                var ts = DateTime.Now.ToString("O");
                var dateStr = d.ToString("yyyy-MM-dd");

                using var cmd = _db.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO income_ledger (date, delta_eur, employed, ts) " +
                    "VALUES ($d, $delta, $e, $ts)";
                cmd.Parameters.AddWithValue("$d", dateStr);
                cmd.Parameters.AddWithValue("$delta", delta);
                cmd.Parameters.AddWithValue("$e", employed);
                cmd.Parameters.AddWithValue("$ts", ts);
                cmd.ExecuteNonQuery();
            }
        });
    }

    /// <summary>Sum of delta in the income ledger from one date through another (inclusive).</summary>
    public double SumPostedRange(DateOnly from, DateOnly to)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(delta_eur), 0) FROM income_ledger WHERE date >= $from AND date <= $to";
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        return Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);
    }

    /// <summary>Preview of today's delta if it were posted — employed flag at this moment,
    /// no persistence.</summary>
    public double TodayPreview()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var dailyRate = DailyRate(today);
        return ConfigService.IsEmployed() ? dailyRate : -dailyRate;
    }

    /// <summary>Calculate total credit earned from completed tasks over a range of dates.
    /// Credit applies only to unemployed days (employed=0) and is based on the ratio of
    /// tasks completed to tasks due. Requires a populated ScoreService with active plans.</summary>
    public double CreditRange(DateOnly from, DateOnly to, ScoreService score)
    {
        double credit = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var (total, done) = score.DayTaskCounts(d);
            if (total <= 0) continue;

            // Get the ledger row for this day to check employed flag and delta
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT delta_eur, employed FROM income_ledger WHERE date = $d";
            cmd.Parameters.AddWithValue("$d", d.ToString("yyyy-MM-dd"));
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var employed = Convert.ToInt32(reader["employed"]);
                if (employed == 0) // Only credit unemployed days
                {
                    var delta = Convert.ToDouble(reader["delta_eur"]);
                    // delta is negative for unemployed days, so credit = -delta * (done/total)
                    credit += -delta * (double)done / total;
                }
            }
        }
        return credit;
    }

    /// <summary>Today's earned credit from completed tasks, if unemployed. Zero if employed or no tasks due.</summary>
    public double TodayCredit(ScoreService score)
    {
        if (ConfigService.IsEmployed()) return 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var (total, done) = score.DayTaskCounts(today);
        if (total <= 0) return 0;

        var dailyRate = DailyRate(today);
        // For today, credit = dailyRate * (done/total), since we don't have a ledger row yet
        return dailyRate * (double)done / total;
    }

    /// <summary>Sidebar balance: sum of posted income plus credit for all closed days
    /// (through yesterday only, not including today's preview).</summary>
    public double PostedBalanceWithCredit(ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        // Get the posted sum for all days through yesterday
        var posted = SumPostedRange(DateOnly.FromDateTime(DateTime.Today.AddYears(-100)), yesterday);

        // Find the first posted date to start credit calculation from
        DateOnly creditFrom;
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT MIN(date) FROM income_ledger";
            var minDateStr = cmd.ExecuteScalar() as string;
            if (string.IsNullOrEmpty(minDateStr))
                return posted + PaidEarnings(LedgerStart, yesterday).Sum(e => e.Eur); // No ledger entries yet
            creditFrom = DateOnly.ParseExact(minDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Calculate credit for closed days through yesterday only
        var credit = CreditRange(creditFrom, yesterday, score);
        return posted + credit + PaidEarnings(LedgerStart, yesterday).Sum(e => e.Eur);
    }

    private static readonly DateOnly LedgerStart = new(2025, 12, 4);

    /// <summary>One payable tag's share of a period: diary minutes carrying that tag × its hourly value.</summary>
    public sealed record PaidEarning(PaidTag Tag, int Minutes)
    {
        public double Eur => Minutes / 60.0 * Tag.HourlyEur;
    }

    /// <summary>Earnings from payable-tagged diary time, <paramref name="from"/>..<paramref name="to"/>
    /// inclusive (user decision 2026-10-01). Computed at read time from the diary, like the
    /// plan-completion credit, so editing a rate re-prices history consistently and no ledger row
    /// is rewritten. Tags with no time in the range are omitted.</summary>
    public List<PaidEarning> PaidEarnings(DateOnly from, DateOnly to)
    {
        var result = new List<PaidEarning>();
        if (from < LedgerStart) from = LedgerStart;
        if (from > to) return result;
        foreach (var tag in PaidTags.All())
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(duration_min), 0) FROM time_diary " +
                              "WHERE tag=$t AND date >= $from AND date <= $to";
            cmd.Parameters.AddWithValue("$t", tag.Value);
            cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            var min = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            if (min > 0) result.Add(new PaidEarning(tag, min));
        }
        return result;
    }

    /// <summary>Earnings for the period Reports is showing, today included.</summary>
    public List<PaidEarning> PaidEarningsForPeriod(ReportPeriod period)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return PaidEarnings(ReportData.PeriodStart(period, today), today);
    }

    /// <summary>Sum of posted income for a reporting period, plus today's live preview and credit.</summary>
    public (double Net, double Credit) SumForPeriodWithCredit(ReportPeriod period, ScoreService score)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periodStart = ReportData.PeriodStart(period, today);
        var yesterday = today.AddDays(-1);

        double posted = 0;
        double credit = 0;

        // Sum posted deltas through yesterday
        if (periodStart < today)
            posted = SumPostedRange(periodStart, yesterday);

        // Calculate credit for the period
        var creditStart = periodStart < today ? periodStart : today;
        var creditEnd = yesterday;
        if (creditStart <= creditEnd)
            credit = CreditRange(creditStart, creditEnd, score);

        // Add today's preview (posted) and today's credit
        var todayPreview = TodayPreview();
        var todayCredit = TodayCredit(score);

        var paid = PaidEarnings(periodStart, today).Sum(e => e.Eur);
        return (posted + todayPreview + credit + todayCredit + paid, credit + todayCredit);
    }

    /// <summary>Per-day parts of <see cref="SumForPeriodWithCredit"/>'s Net (paid work included) and of
    /// <see cref="PaidEarnings"/>, <paramref name="from"/>..today inclusive. Same terms as the period
    /// figure, split by day, so the days add up to it (up to float addition order). Two grouped
    /// reads instead of per-day range calls (Reports has a slow-load history).</summary>
    public Dictionary<DateOnly, (double Net, double Paid)> DailyIncome(DateOnly from, ScoreService score)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        var posted = new Dictionary<DateOnly, double>();
        if (from <= yesterday)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT date, delta_eur FROM income_ledger WHERE date >= $from AND date <= $to";
            cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd", inv));
            cmd.Parameters.AddWithValue("$to", yesterday.ToString("yyyy-MM-dd", inv));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                posted[DateOnly.ParseExact(r.GetString(0), "yyyy-MM-dd", inv)] = Convert.ToDouble(r.GetValue(1), inv);
        }

        // Minutes per (date, tag); the clamp to LedgerStart is the one PaidEarnings applies.
        var minutes = new Dictionary<(DateOnly, string), int>();
        var paidFrom = from < LedgerStart ? LedgerStart : from;
        if (paidFrom <= today)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT date, tag, SUM(duration_min) FROM time_diary " +
                              "WHERE date >= $from AND date <= $to GROUP BY date, tag";
            cmd.Parameters.AddWithValue("$from", paidFrom.ToString("yyyy-MM-dd", inv));
            cmd.Parameters.AddWithValue("$to", today.ToString("yyyy-MM-dd", inv));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (r.IsDBNull(1)) continue;
                minutes[(DateOnly.ParseExact(r.GetString(0), "yyyy-MM-dd", inv), r.GetString(1))] =
                    Convert.ToInt32(r.GetValue(2), inv);
            }
        }

        var tags = PaidTags.All();
        var result = new Dictionary<DateOnly, (double Net, double Paid)>();
        for (var d = from; d <= today; d = d.AddDays(1))
        {
            double paid = 0;
            if (d >= LedgerStart)
                foreach (var tag in tags)   // per entry, duplicates included, like PaidEarnings
                    if (minutes.TryGetValue((d, tag.Value), out var m) && m > 0)
                        paid += new PaidEarning(tag, m).Eur;

            double net;
            if (d < today)
                net = (posted.TryGetValue(d, out var p) ? p : 0) + CreditRange(d, d, score);
            else
                net = TodayPreview() + TodayCredit(score);
            result[d] = (net + paid, paid);
        }
        return result;
    }

    /// <summary>Sum of posted income for a reporting period, plus today's live preview (backwards compatible).</summary>
    public double SumForPeriod(ReportPeriod period)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periodStart = ReportData.PeriodStart(period, today);
        var yesterday = today.AddDays(-1);

        // If period starts today (the Day tab), the posted sum is 0
        if (periodStart >= today)
            return TodayPreview();

        return SumPostedRange(periodStart, yesterday) + TodayPreview();
    }
}

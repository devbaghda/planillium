using Planillium.App.Models;

namespace Planillium.App.Services;

/// <summary>
/// Ledger-based loss/gain-of-income tracker — mirrors ScoreService's ledger+catch-up shape,
/// posting one row per already-closed calendar day with a fixed delta and employed flag.
/// </summary>
public sealed class IncomeService : IDisposable
{
    private readonly Database _db;

    public IncomeService(Database db)
    {
        _db = db;
    }

    // No-op: this class shares its connection with (owned and disposed by)
    // the Database it was constructed with. Every call site disposes its
    // own Database separately, so this is never the last reference standing.
    public void Dispose() { }

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
                    "INSERT INTO income_ledger (date, delta, employed, ts) " +
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
        cmd.CommandText = "SELECT COALESCE(SUM(delta), 0) FROM income_ledger WHERE date >= $from AND date <= $to";
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

    /// <summary>Sum of posted income for a reporting period, plus today's live preview.</summary>
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

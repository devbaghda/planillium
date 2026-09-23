using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>
/// Lost/gained-earnings counter (2026-09-22 request) — a EUR figure tracking what the
/// configured potential net monthly income costs (while unemployed) or adds (while
/// employed), one entry per calendar day, backdated to a real start date. Deliberately
/// separate from score_ledger/ScoreService: EUR opportunity cost and the accountability
/// point system are different concepts and don't share a table or a reason/date guard.
/// </summary>
public sealed class IncomeService : IDisposable
{
    /// <summary>The real date the user's income stopped — confirmed live 2026-09-22
    /// after two conflicting corrections (2026-12-04 was a misreading; 2025-12-04 is
    /// final). Not user-editable — the settings request only covers the monthly figure
    /// and the employment toggle, not this seed date.</summary>
    public static readonly DateOnly StartDate = new(2025, 12, 4);

    private const int SqliteConstraintViolation = 19;

    private readonly Database _db;

    public IncomeService(Database db) => _db = db;

    // No-op, same reasoning as ScoreService.Dispose: this shares its connection with
    // (and is disposed by) the Database it was constructed with.
    public void Dispose() { }

    /// <summary>Daily rate for the month containing <paramref name="d"/> — the configured
    /// monthly figure divided by however many calendar days that specific month actually
    /// has (28-31), so a full month always sums to exactly the configured figure rather
    /// than drifting under a flat /30 (settled decision, 2026-09-22 clarifying round).</summary>
    public static double DailyRate(DateOnly d) =>
        ConfigService.PotentialMonthlyIncomeEur() / DateTime.DaysInMonth(d.Year, d.Month);

    private bool LedgerHasDate(DateOnly d)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM income_ledger WHERE date=$d";
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        return cmd.ExecuteScalar() != null;
    }

    private void PostDay(DateOnly d, bool employed)
    {
        var delta = DailyRate(d) * (employed ? 1 : -1);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO income_ledger (ts, date, delta_eur, employed) " +
                          "VALUES ($ts, $d, $delta, $emp)";
        cmd.Parameters.AddWithValue("$ts", DateTime.Now.ToIsoTimestamp());
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        cmd.Parameters.AddWithValue("$delta", delta);
        cmd.Parameters.AddWithValue("$emp", employed ? 1 : 0);
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintViolation)
        {
            // the other app instance posted this date between check and insert — fine,
            // the day is covered either way.
        }
    }

    /// <summary>Backfills every calendar day from the later of StartDate/the day after the
    /// last posted entry, through yesterday — never today, which isn't over yet (same
    /// "only ever post a fully-closed day" shape as ScoreService.EnsureScoreCaughtUp,
    /// except unbounded rather than a fixed lookback window, since a first run has to
    /// cover the whole backdated span from 2025-12-04, not just the last week). Employment
    /// status is read once before the loop, not per day: every day this loop can reach is
    /// already in the past by definition, and the only historical record of that status is
    /// "whatever the toggle currently says," so a mid-run re-read couldn't be more correct,
    /// only more likely to split one backfill pass inconsistently down the middle. Each
    /// insert is independently guarded by income_ledger's UNIQUE(date) constraint, so a
    /// partial run (app closed mid-catch-up) just leaves the remainder for next launch.</summary>
    public void EnsureIncomeCaughtUp()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        if (yesterday < StartDate) return;

        var lastPosted = LastPostedDate();
        var from = lastPosted is DateOnly lp ? lp.AddDays(1) : StartDate;
        if (from > yesterday) return;

        var employed = ConfigService.IsEmployed();
        _db.RunInTransaction(() =>
        {
            for (var d = from; d <= yesterday; d = d.AddDays(1))
                PostDay(d, employed);
        });
    }

    private DateOnly? LastPostedDate()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT MAX(date) FROM income_ledger";
        return cmd.ExecuteScalar() is string s ? DateOnly.Parse(s) : null;
    }

    /// <summary>Today's not-yet-posted delta, as if the day closed right now with the
    /// current toggle state — the same "live preview ahead of the ledger" role Reports'
    /// "Today's Score" plays next to the score system's BALANCE chip. Reports adds this to
    /// the posted-days ledger sum so a figure covering "today" (or any period that includes
    /// it) isn't stuck showing yesterday's total for the first ~24 hours of every day; the
    /// sidebar chip deliberately does NOT include this (Database.IncomeBalance is
    /// ledger-only), so the two numbers can differ during the day by design, same as score.</summary>
    public static double TodayPreview()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return DailyRate(today) * (ConfigService.IsEmployed() ? 1 : -1);
    }

    /// <summary>Sum of posted days from <paramref name="from"/> (inclusive) through
    /// yesterday (inclusive) — never today, which TodayPreview covers separately.</summary>
    public double SumPostedRange(DateOnly from)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        if (from > yesterday) return 0;
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(delta_eur), 0) FROM income_ledger WHERE date >= $from AND date <= $to";
        cmd.Parameters.AddWithValue("$from", from.ToIsoDate());
        cmd.Parameters.AddWithValue("$to", yesterday.ToIsoDate());
        return Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);
    }

    /// <summary>The figure Reports shows for a period — posted days in that calendar
    /// window (Day/Week/Month/Year, same calendar-window convention as
    /// ReportData.PeriodStart, not a rolling lookback) plus today's live preview, since
    /// every period Reports offers includes today.</summary>
    public double SumForPeriod(ReportPeriod period)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periodStart = ReportData.PeriodStart(period, today);
        return SumPostedRange(periodStart) + TodayPreview();
    }
}

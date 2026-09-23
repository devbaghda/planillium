using System.Text.Json.Nodes;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// Covers IncomeService — the lost/gained-earnings counter (2026-09-22 request). Three
/// things carry real risk here and none had a safety net before this file: the daily-rate
/// formula (must divide by the actual days in that specific month, not a flat /30, so a
/// full month always sums to exactly the configured figure — settled decision 1), the
/// non-retroactive sign rule (a toggle flip must only affect days from that point forward,
/// never relabel an already-posted historical day — settled decision 3, explicitly flagged
/// in REQUEST.md as needing a deterministic rule), and the posted-vs-preview split that
/// keeps the sidebar chip and Reports' period figures consistent with each other.
///
/// income_ledger has no per-test key the way task_completions/score_ledger do (it's a
/// deliberate global singleton — one date, one row, see Database.cs's schema comment) — so
/// unlike the rest of this assembly, which relies on unique plan ids to avoid collisions on
/// the one shared SQLite file (see TestRootFixture), these tests explicitly clear
/// income_ledger at the start of each one instead. That substitutes for uniqueness here.
/// </summary>
[Collection("TestRoot")]
public sealed class IncomeServiceTests
{
    private static void ClearIncomeLedger()
    {
        using var conn = AppPaths.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM income_ledger";
        cmd.ExecuteNonQuery();
    }

    private static void DeleteIncomeLedgerFrom(DateOnly d)
    {
        using var conn = AppPaths.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM income_ledger WHERE date >= $d";
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        cmd.ExecuteNonQuery();
    }

    private static void SetIncomeConfig(double monthlyEur, bool employed) =>
        ConfigService.Mutate(o => o["income"] = new JsonObject
        {
            ["monthly_net_eur"] = monthlyEur,
            ["employed"] = employed,
        });

    [Fact]
    public void DailyRate_FullCalendarMonth_SumsToExactlyTheConfiguredFigure()
    {
        // The whole point of decision 1: a full month must sum to exactly the configured
        // amount regardless of whether it has 28, 30 or 31 days — not drift under a flat
        // /30. Checked against three different month lengths at once.
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3100, employed: false);

        var jan2026 = SumDailyRateOverMonth(2026, 1);  // 31 days
        var apr2026 = SumDailyRateOverMonth(2026, 4);  // 30 days
        var feb2026 = SumDailyRateOverMonth(2026, 2);  // 28 days, non-leap

        Assert.Equal(3100, jan2026, precision: 6);
        Assert.Equal(3100, apr2026, precision: 6);
        Assert.Equal(3100, feb2026, precision: 6);
    }

    private static double SumDailyRateOverMonth(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        double sum = 0;
        for (var day = 1; day <= days; day++)
            sum += IncomeService.DailyRate(new DateOnly(year, month, day));
        return sum;
    }

    [Fact]
    public void DailyRate_UsesActualDaysInThatMonth_NotAFlatThirty()
    {
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3000, employed: false);

        var januaryRate = IncomeService.DailyRate(new DateOnly(2026, 1, 15));   // /31
        var februaryRate = IncomeService.DailyRate(new DateOnly(2026, 2, 15));  // /28

        Assert.Equal(3000.0 / 31, januaryRate, precision: 6);
        Assert.Equal(3000.0 / 28, februaryRate, precision: 6);
        Assert.NotEqual(3000.0 / 30, januaryRate, precision: 6);
        Assert.NotEqual(3000.0 / 30, februaryRate, precision: 6);
    }

    [Fact]
    public void EnsureIncomeCaughtUp_FullCalendarMonth_PostsExactlyTheConfiguredFigure()
    {
        // Same decision-1 guarantee as the pure DailyRate test above, but exercised through
        // the real backfill path (EnsureIncomeCaughtUp -> income_ledger -> SumPostedRange)
        // rather than calling DailyRate directly, so the loop/rounding/accumulation in the
        // actual write path is what's under test here, not just the formula in isolation.
        // February 2026 is safely in the past (today is 2026-09-22) and fully inside the
        // backfilled range from IncomeService.StartDate (2025-12-04).
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3100, employed: false);

        using var svc = new IncomeService(db);
        svc.EnsureIncomeCaughtUp();

        var febStart = new DateOnly(2026, 2, 1);
        var marStart = new DateOnly(2026, 3, 1);
        var februarySum = svc.SumPostedRange(febStart) - svc.SumPostedRange(marStart);

        // Unemployed the whole time -> the full configured figure lost, sign negative.
        Assert.Equal(-3100, februarySum, precision: 2);
    }

    [Fact]
    public void EnsureIncomeCaughtUp_Employed_PostsPositiveDeltas()
    {
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 2800, employed: true);

        using var svc = new IncomeService(db);
        svc.EnsureIncomeCaughtUp();

        var febStart = new DateOnly(2026, 2, 1);
        var marStart = new DateOnly(2026, 3, 1);
        var februarySum = svc.SumPostedRange(febStart) - svc.SumPostedRange(marStart);

        Assert.Equal(2800, februarySum, precision: 2);
    }

    [Fact]
    public void EnsureIncomeCaughtUp_SignFlip_IsForwardOnly_DoesNotRelabelAlreadyPostedDays()
    {
        // The one edge case REQUEST.md explicitly flagged as needing a deterministic rule
        // (decision 3): flipping the employment toggle must only change the sign of days
        // posted from that point forward. Simulated here by backfilling everything as
        // unemployed, then erasing the last few days as if they hadn't been posted yet
        // (the same state a partial/interrupted catch-up would leave), flipping to
        // employed, and re-running catch-up. Only the re-posted days should flip sign;
        // everything before them must keep its original, negative sign.
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3000, employed: false);

        using var svc = new IncomeService(db);
        svc.EnsureIncomeCaughtUp();

        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var cutover = yesterday.AddDays(-4);
        DeleteIncomeLedgerFrom(cutover);

        SetIncomeConfig(monthlyEur: 3000, employed: true);
        svc.EnsureIncomeCaughtUp();

        var dayBeforeCutover = SumSingleDay(svc, cutover.AddDays(-1));
        var dayAtCutover = SumSingleDay(svc, cutover);
        var dayAfterCutover = SumSingleDay(svc, cutover.AddDays(1));

        Assert.True(dayBeforeCutover < 0, "historical day predating the flip must keep its original negative sign");
        Assert.True(dayAtCutover > 0, "the first re-posted day must carry the new, positive sign");
        Assert.True(dayAfterCutover > 0, "days after the flip must carry the new, positive sign");
    }

    private static double SumSingleDay(IncomeService svc, DateOnly d) =>
        svc.SumPostedRange(d) - svc.SumPostedRange(d.AddDays(1));

    [Fact]
    public void SumForPeriod_Day_EqualsTodaysLivePreviewOnly()
    {
        // EnsureIncomeCaughtUp never posts today (it isn't over yet) — so the Day period,
        // which starts today, must come entirely from TodayPreview, mirroring the same
        // "BALANCE chip vs. Today's Score preview" split the score system already uses.
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3000, employed: false);

        using var svc = new IncomeService(db);
        svc.EnsureIncomeCaughtUp();

        Assert.Equal(IncomeService.TodayPreview(), svc.SumForPeriod(ReportPeriod.Day), precision: 6);
    }

    [Fact]
    public void IncomeBalance_MatchesLedgerOnlySum_ExcludingTodaysPreview()
    {
        // The sidebar chip (Database.IncomeBalance) is deliberately posted-days-only — it
        // must equal SumPostedRange from the very start, and must NOT move when today's
        // live preview would (i.e. it must differ from SumForPeriod, which does include it,
        // whenever today's preview is non-zero).
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3000, employed: false);

        using var svc = new IncomeService(db);
        svc.EnsureIncomeCaughtUp();

        Assert.Equal(svc.SumPostedRange(IncomeService.StartDate), db.IncomeBalance(), precision: 6);
        Assert.NotEqual(db.IncomeBalance(), svc.SumForPeriod(ReportPeriod.Year), precision: 6);
    }

    [Fact]
    public void EnsureIncomeCaughtUp_IsIdempotent_RunningTwiceDoesNotDoublePost()
    {
        // The UNIQUE(date) guard's whole reason to exist: a second catch-up run (e.g. a
        // second page navigation constructing another IncomeService) must leave the ledger
        // exactly as it was, not double up every day's delta.
        using var db = new Database();
        ClearIncomeLedger();
        SetIncomeConfig(monthlyEur: 3000, employed: false);

        using var svc = new IncomeService(db);
        svc.EnsureIncomeCaughtUp();
        var first = svc.SumPostedRange(IncomeService.StartDate);

        svc.EnsureIncomeCaughtUp();
        var second = svc.SumPostedRange(IncomeService.StartDate);

        Assert.Equal(first, second, precision: 6);
    }
}

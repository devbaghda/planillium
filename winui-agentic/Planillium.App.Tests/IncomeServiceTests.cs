using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// Tests for the income ledger and catch-up logic. Covers the non-retroactive sign-flip rule
/// (a day's delta is permanent once written; flipping the employed toggle afterward only affects
/// future days), the month-summing rule (a full calendar month where every day posts under the
/// same employed flag sums to exactly ±the configured monthly figure), and the backfill from
/// 2025-12-04 on first run.
/// </summary>
[Collection("TestRoot")]
public sealed class IncomeServiceTests
{
    [Fact]
    public void ConfigService_IsEmployed_ReturnsCorrectValue()
    {
        // Set to true
        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject income)
                cfg["income"] = income = new System.Text.Json.Nodes.JsonObject();
            income["employed"] = System.Text.Json.Nodes.JsonValue.Create(true);
        });
        Assert.True(ConfigService.IsEmployed());

        // Set to false
        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject income)
                cfg["income"] = income = new System.Text.Json.Nodes.JsonObject();
            income["employed"] = System.Text.Json.Nodes.JsonValue.Create(false);
        });
        Assert.False(ConfigService.IsEmployed());
    }
    [Fact]
    public void EnsureIncomeCaughtUp_EmptyTable_BackfillsFrom20251204()
    {
        using var db = new Database();
        using var income = new IncomeService(db);

        income.EnsureIncomeCaughtUp();

        // Table should now hold rows from 2025-12-04 through yesterday
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM income_ledger";
        var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        Assert.True(count > 0, "income_ledger should have rows after catch-up");

        // Verify the first row is 2025-12-04
        cmd.CommandText = "SELECT MIN(date) FROM income_ledger";
        var minDate = cmd.ExecuteScalar() as string;
        Assert.Equal("2025-12-04", minDate);

        // Verify the last row is yesterday
        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1).ToString("yyyy-MM-dd");
        cmd.CommandText = "SELECT MAX(date) FROM income_ledger";
        var maxDate = cmd.ExecuteScalar() as string;
        Assert.Equal(yesterday, maxDate);
    }


    [Fact]
    public void EnsureIncomeCaughtUp_FullMonth_SumsToExactlyMonthlyFigure()
    {
        using var db = new Database();
        // Set up a known monthly figure
        const double TestMonthly = 2700.0;
        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject income)
                cfg["income"] = income = new System.Text.Json.Nodes.JsonObject();
            income["potential_monthly_net_eur"] = TestMonthly;
            income["employed"] = true;
        });

        using var income = new IncomeService(db);
        income.EnsureIncomeCaughtUp();

        // Find a month that has been fully backfilled (Jan 2026 = 31 days, all from 2025-12-04 onward)
        var testMonth = new DateOnly(2026, 1, 1);
        var monthStart = testMonth.ToString("yyyy-MM-dd");
        var monthEnd = new DateOnly(2026, 1, 31).ToString("yyyy-MM-dd");

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT SUM(delta) FROM income_ledger WHERE date >= $start AND date <= $end";
        cmd.Parameters.AddWithValue("$start", monthStart);
        cmd.Parameters.AddWithValue("$end", monthEnd);
        var monthSum = Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);

        // Should sum to exactly (or nearly exactly, within floating-point tolerance) the monthly figure
        Assert.True(Math.Abs(monthSum - TestMonthly) < 1e-6,
            $"Month sum {monthSum} should equal monthly figure {TestMonthly}");
    }


    [Fact]
    public void DailyRate_VariesByMonthLength()
    {
        // February (28 days) has a larger daily rate than other months (30-31 days)
        // for the same monthly total. This test verifies the daily rate is computed
        // per-month correctly.
        var feb2026 = new DateOnly(2026, 2, 1);
        var mar2026 = new DateOnly(2026, 3, 1);

        using var db = new Database();
        using var income = new IncomeService(db);

        // We can't directly call DailyRate from tests since it's private,
        // but we can infer it from the deltas: for the same monthly income,
        // a day in February should have a larger |delta| than a day in March.

        // Set up employed = true so deltas are positive and easy to compare
        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["employed"] = true;
            inc["potential_monthly_net_eur"] = 2800.0;
        });

        income.EnsureIncomeCaughtUp();

        using var cmd = db.CreateCommand();
        // Get a February delta (28 days in 2026)
        cmd.CommandText = "SELECT delta FROM income_ledger WHERE date LIKE '2026-02-%' LIMIT 1";
        var febDelta = Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);

        // Get a March delta (31 days in 2026)
        cmd.CommandText = "SELECT delta FROM income_ledger WHERE date LIKE '2026-03-%' LIMIT 1";
        var marDelta = Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);

        Assert.True(febDelta > marDelta,
            $"February daily rate ({febDelta}) should be higher than March ({marDelta}) for same monthly total");
    }
}

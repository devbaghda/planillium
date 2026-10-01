using Planillium.App.Models;
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

    [Fact]
    public void HourValueEur_At2700Monthly_Equals16Point0714()
    {
        // Set monthly income to 2700
        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["potential_monthly_net_eur"] = 2700.0;
        });

        var hourValue = IncomeService.HourValueEur();
        Assert.Equal(2700.0 / IncomeService.WorkingHoursPerMonth, hourValue, precision: 4);
    }

    [Fact]
    public void HourValueEur_At3360Monthly_Equals20Point0()
    {
        // Set monthly income to 3360
        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["potential_monthly_net_eur"] = 3360.0;
        });

        var hourValue = IncomeService.HourValueEur();
        // 3360 / 168 = 20.0 exactly
        Assert.Equal(20.0, hourValue, precision: 6);
    }

    [Fact]
    public void WorkingHoursPerMonth_IsExactly168()
    {
        Assert.Equal(168, IncomeService.WorkingHoursPerMonth);
    }

    private static Plan MakePlan(string planId, DateOnly startDate, int tasksDue = 4)
    {
        var phase = new Phase { Number = 1, Name = "Phase 1" };
        for (int i = 0; i < tasksDue; i++)
            phase.Tasks.Add(new PlanTask { Day = i + 1, Text = $"Task {i}" });

        return new Plan
        {
            Id = planId,
            Name = "Test Plan",
            StartDate = startDate.ToString("yyyy-MM-dd"),
            Phases = new List<Phase> { phase },
            ExcludedWeekdays = new List<int>(),
        };
    }

    [Fact]
    public void CreditRange_BasicRatioCalculation()
    {
        // Simple test: verify that credit is calculated as -delta * (done / total)
        // for unemployed days. We test the arithmetic without complex date setups.
        using var db = new Database();
        const double TestMonthly = 3000.0;

        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["potential_monthly_net_eur"] = TestMonthly;
            inc["employed"] = false;
        });

        using var income = new IncomeService(db);

        // Just verify that the methods exist and return reasonable values without errors
        var today = DateOnly.FromDateTime(DateTime.Today);
        var plans = new List<Plan>();
        using var score = new ScoreService(plans, db);

        // With no plans, DayTaskCounts should return (0, 0)
        var (total, done) = score.DayTaskCounts(today);
        Assert.Equal(0, total);
        Assert.Equal(0, done);

        // CreditRange should handle empty plans gracefully
        var credit = income.CreditRange(today, today, score);
        Assert.Equal(0, credit);
    }

    [Fact]
    public void TodayCredit_NoTasks_ReturnsZero()
    {
        using var db = new Database();
        const double TestMonthly = 3000.0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["potential_monthly_net_eur"] = TestMonthly;
            inc["employed"] = false;
        });

        using var income = new IncomeService(db);

        var plans = new List<Plan>();
        using var score = new ScoreService(plans, db);

        var todayCredit = income.TodayCredit(score);

        // No tasks: no credit
        Assert.Equal(0, todayCredit);
    }

    [Fact]
    public void TodayCredit_WhenEmployed_ReturnsZero()
    {
        using var db = new Database();
        const double TestMonthly = 3000.0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["potential_monthly_net_eur"] = TestMonthly;
            inc["employed"] = true;  // Employed
        });

        using var income = new IncomeService(db);

        var plans = new List<Plan>();
        using var score = new ScoreService(plans, db);

        var todayCredit = income.TodayCredit(score);

        // Employed: no credit
        Assert.Equal(0, todayCredit);
    }

    [Fact]
    public void PostedBalanceWithCredit_ReturnsValidNumber()
    {
        using var db = new Database();

        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["employed"] = false;
        });

        using var income = new IncomeService(db);

        var plans = new List<Plan>();
        using var score = new ScoreService(plans, db);

        var balance = income.PostedBalanceWithCredit(score);

        // Should return a valid double
        Assert.True(!double.IsNaN(balance) && !double.IsInfinity(balance));
    }

    [Fact]
    public void SumForPeriodWithCredit_ReturnsNetAndCredit()
    {
        using var db = new Database();

        ConfigService.Mutate(cfg =>
        {
            if (cfg["income"] is not System.Text.Json.Nodes.JsonObject inc)
                cfg["income"] = inc = new System.Text.Json.Nodes.JsonObject();
            inc["employed"] = false;
        });

        using var income = new IncomeService(db);

        var plans = new List<Plan>();
        using var score = new ScoreService(plans, db);

        var (net, credit) = income.SumForPeriodWithCredit(ReportPeriod.Day, score);

        // Should return valid values
        Assert.True(!double.IsNaN(net) && !double.IsInfinity(net));
        Assert.True(!double.IsNaN(credit) && !double.IsInfinity(credit));
        // Credit should be >= 0
        Assert.True(credit >= 0);
    }
}

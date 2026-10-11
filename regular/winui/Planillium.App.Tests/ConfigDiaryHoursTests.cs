using System.Text.Json.Nodes;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// The diary logging window (2026-08-04 report: "I changed the day start to 08:00 but the app
/// still counts my absence from 06:00"). It used to be a pair of hardcoded 06:00/20:00
/// constants inside ActivityTracker, unreachable from Settings and unrelated to working hours.
/// It is now, by definition, the working day — the same hours, not a second setting that
/// happens to agree (user's call, same day, after a few hours as its own `diary_hours` block).
///
/// These tests exist to keep it that way: the one thing that would quietly resurrect the
/// original bug is the two windows drifting apart again.
///
/// Shares the collection's single config.json (see TestRootFixture) — each test writes the
/// whole of every block it depends on, so it can't inherit a leftover value from another.
/// </summary>
[Collection("TestRoot")]
public sealed class ConfigDiaryHoursTests
{
    private static void SetWorkingHours(string start, string end) =>
        ConfigService.Mutate(cfg =>
            cfg["working_hours"] = new JsonObject { ["start"] = start, ["end"] = end });

    /// <summary>The actual fix for the report: move the working day and the diary moves with
    /// it, with nothing else to configure.</summary>
    [Theory]
    [InlineData("08:00", "18:00", 8, 18)]
    [InlineData("06:00", "20:00", 6, 20)]
    [InlineData("09:30", "17:45", 9, 17)]
    public void DiaryWindowIsTheWorkingDay(string start, string end, int expectStartHour, int expectEndHour)
    {
        SetWorkingHours(start, end);

        Assert.Equal(ConfigService.WorkStartTime(), ConfigService.DiaryStartTime());
        Assert.Equal(ConfigService.WorkEndTime(), ConfigService.DiaryEndTime());
        Assert.Equal(expectStartHour, ConfigService.DiaryStartTime().Hours);
        Assert.Equal(expectEndHour, ConfigService.DiaryEndTime().Hours);
    }

    /// <summary>A `diary_hours` block left behind from the few hours that setting existed must
    /// be inert, not a silent override — otherwise a config.json written in that window would
    /// keep tracking its own hours forever with no UI anywhere admitting it.</summary>
    [Fact]
    public void StrayDiaryHoursBlockIsIgnored()
    {
        ConfigService.Mutate(cfg =>
        {
            cfg["working_hours"] = new JsonObject { ["start"] = "08:00", ["end"] = "18:00" };
            cfg["diary_hours"] = new JsonObject { ["start"] = "06:00", ["end"] = "22:00" };
        });
        try
        {
            Assert.Equal(new TimeSpan(8, 0, 0), ConfigService.DiaryStartTime());
            Assert.Equal(new TimeSpan(18, 0, 0), ConfigService.DiaryEndTime());
        }
        finally
        {
            ConfigService.Mutate(cfg => cfg.Remove("diary_hours"));
        }
    }

    /// <summary>Missing/garbage working hours degrade to the documented 08:00-20:00 defaults
    /// rather than to 00:00-00:00, which would be a zero-length window — i.e. no tracking at
    /// all, all day, with nothing on screen to say why.</summary>
    [Theory]
    [InlineData("not a time")]
    [InlineData("")]
    [InlineData("25:99")]
    public void UnparseableWorkingHoursDegradeToDefaultsNotZero(string bad)
    {
        SetWorkingHours(bad, bad);

        Assert.Equal(new TimeSpan(8, 0, 0), ConfigService.DiaryStartTime());
        Assert.Equal(new TimeSpan(20, 0, 0), ConfigService.DiaryEndTime());
        Assert.True(ConfigService.DiaryStartTime() < ConfigService.DiaryEndTime());
    }
}

/// <summary>
/// The scoring rules that were compile-time consts until 2026-08-04 — the only numbers in the
/// score formula that weren't editable, while every rate beside them in the same formula
/// already came from config.json's "scoring" block. Absent keys must reproduce the old
/// constants exactly, which is what makes this a pure lift rather than a retune.
/// </summary>
[Collection("TestRoot")]
public sealed class ScoreServiceConfigurableRulesTests
{
    private static void SetScoring(Action<JsonObject> edit) =>
        ConfigService.Mutate(cfg =>
        {
            if (cfg["scoring"] is not JsonObject scoring) cfg["scoring"] = scoring = new JsonObject();
            edit(scoring);
        });

    [Fact]
    public void DefaultsMatchTheConstantsTheyReplaced()
    {
        SetScoring(s =>
        {
            s.Remove("daily_floor");
            s.Remove("overdue_accrual_cap_days");
            s.Remove("replan_flat_fee");
            s.Remove("great_day_threshold");
        });

        Assert.Equal(-10, ScoreService.DailyFloor);
        Assert.Equal(3, ScoreService.OverdueAccrualCapDays);
        Assert.Equal(-10, ScoreService.ReplanFlatFee);
        Assert.Equal(20, ScoreService.GreatDayThreshold);
    }

    [Fact]
    public void ConfiguredValuesAreUsed()
    {
        SetScoring(s =>
        {
            s["daily_floor"] = -25;
            s["overdue_accrual_cap_days"] = 5;
            s["replan_flat_fee"] = -3;
            s["great_day_threshold"] = 40;
        });
        // finally, not a trailing cleanup: every class here shares one config.json (see
        // TestRootFixture), so a failed assert that skipped the reset would leave a -25 floor
        // behind and fail unrelated scoring tests afterwards, burying the real failure.
        try
        {
            Assert.Equal(-25, ScoreService.DailyFloor);
            Assert.Equal(5, ScoreService.OverdueAccrualCapDays);
            Assert.Equal(-3, ScoreService.ReplanFlatFee);
            Assert.Equal(40, ScoreService.GreatDayThreshold);
        }
        finally
        {
            SetScoring(s =>
            {
                s.Remove("daily_floor");
                s.Remove("overdue_accrual_cap_days");
                s.Remove("replan_flat_fee");
                s.Remove("great_day_threshold");
            });
        }
    }

    /// <summary>The table is the single source of every default (2026-08-04) — Settings
    /// prefills from it and the score formula resolves through it, so a key whose table entry
    /// disagreed with what the formula actually falls back to would show one number and apply
    /// another. Asserts they're the same object of truth, not two copies that happen to match.</summary>
    [Fact]
    public void EveryRuleResolvesToItsTableDefaultWhenAbsent()
    {
        ConfigService.Mutate(cfg => cfg.Remove("scoring"));

        foreach (var rule in ScoringRules.All)
        {
            Assert.Equal(rule.Default, ConfigService.ScoringRate(rule.Key));
            Assert.Equal(rule.Default, ScoringRules.DefaultFor(rule.Key));
        }
    }

    /// <summary>Every rule's own default has to sit inside the range Settings will let you type,
    /// or the page would open showing a value it then refuses to save back.</summary>
    [Fact]
    public void EveryDefaultIsInsideItsOwnSettingsRange()
    {
        foreach (var rule in ScoringRules.All)
        {
            Assert.True(rule.Min <= rule.Default, $"{rule.Key}: default {rule.Default} below min {rule.Min}");
            Assert.True(rule.Default <= rule.Max, $"{rule.Key}: default {rule.Default} above max {rule.Max}");
            Assert.False(string.IsNullOrWhiteSpace(rule.Label), $"{rule.Key}: no label");
        }
    }

    /// <summary>Duplicate keys would silently make one entry unreachable — the dictionary build
    /// in ScoringRules would throw, but only when something first touched it at runtime.</summary>
    [Fact]
    public void RuleKeysAreUnique()
    {
        Assert.Equal(ScoringRules.All.Count, ScoringRules.All.Select(r => r.Key).Distinct().Count());
    }

    /// <summary>An unknown key is a typo, not a zero-scoring rule — see DefaultFor.</summary>
    [Fact]
    public void UnknownKeyThrowsRatherThanScoringZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScoringRules.DefaultFor("no_such_rule"));
    }

    /// <summary>A negative accrual cap would run the "how many days is this task still
    /// costing you" window backwards, so it's clamped rather than trusted.</summary>
    [Fact]
    public void NegativeAccrualCapIsClampedToZero()
    {
        SetScoring(s => s["overdue_accrual_cap_days"] = -4);
        try
        {
            Assert.Equal(0, ScoreService.OverdueAccrualCapDays);
        }
        finally
        {
            SetScoring(s => s.Remove("overdue_accrual_cap_days"));
        }
    }

    /// <summary>DailyFloor still floors a day's score after the move — the property is read
    /// through the same DayScoreBreakdown path the real formula uses, not just directly.</summary>
    [Fact]
    public void ConfiguredDailyFloorStillFloorsADayScore()
    {
        SetScoring(s => s["daily_floor"] = -4);
        try
        {
            var breakdown = new DayScoreBreakdown(
                TaskPoints: 0, MultiTaskBonus: 0, OnPlanPoints: 0,
                OffPlanPoints: -50, MissedPoints: -20, StreakBonus: 0);

            Assert.Equal(-70, breakdown.RawTotal);
            Assert.Equal(-4, breakdown.FlooredTotal);
        }
        finally
        {
            SetScoring(s => s.Remove("daily_floor"));
        }
    }
}

using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>G9b stage 7: after the diary hours only activity a confident learned rule marks as
/// on-plan work is recorded (acceptance F1, F2, F3).</summary>
[Collection("TestRoot")]
public sealed class LearnedRuleAfterHoursTests
{
    private static IReadOnlySet<string> S(params string[] w) => new HashSet<string>(w);

    private static (ActivityTracker Tracker, string App) Make(int examples, int total)
    {
        var app = "AppZ" + Guid.NewGuid().ToString("N")[..10];
        var tracker = new ActivityTracker(ConfigService.Root);
        var rule = new LearnedRule(1, app, new[] { "quotation" }, DiaryCategory.OnPlan);
        var history = new List<HistoryRow> { new(app, S("quotation"), DiaryCategory.OnPlan, examples) };
        if (total > examples) history.Add(new HistoryRow(app, S("quotation"), DiaryCategory.OffPlan, total - examples));
        tracker.SetRulesForTest(new RuleBook(new[] { rule }, history));
        return (tracker, app);
    }

    // A past day (not today): rows written here must not leak into other tests' today totals.
    private static DateTime At(int h, int m = 0) => DateTime.Today.AddDays(-45).AddHours(h).AddMinutes(m);

    private static List<(string Window, string Category, string? RuleState, string Start, string End)> Rows(string app)
    {
        using var db = new Database();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT window, category, rule_state, start_time, end_time FROM time_diary WHERE window LIKE $w ORDER BY id";
        cmd.Parameters.AddWithValue("$w", "%" + app + "%");
        using var r = cmd.ExecuteReader();
        var rows = new List<(string, string, string?, string, string)>();
        while (r.Read())
            rows.Add((r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3), r.GetString(4)));
        return rows;
    }

    [Fact]
    public void Confident_rule_work_after_hours_is_recorded_when_the_window_changes()
    {
        var (t, app) = Make(30, 30);
        var title = $"Quotation_Acme - {app}";
        t.SimulateAfterHoursPoll(At(22), title, 0);
        t.SimulateAfterHoursPoll(At(22, 10), title, 0);                 // same window, still open
        Assert.Empty(Rows(app));
        t.SimulateAfterHoursPoll(At(22, 20), "Something else", 0);      // switched: the work is closed
        var row = Assert.Single(Rows(app));
        Assert.Equal(DiaryCategory.OnPlan, row.Category);
        Assert.Equal(RuleStates.Auto, row.RuleState);
        Assert.Equal("22:00", row.Start);
        Assert.Equal("22:20", row.End);
    }

    [Fact]
    public void Rule_that_only_suggests_records_nothing_after_hours()
    {
        var (t, app) = Make(9, 9);
        var title = $"Quotation_Acme - {app}";
        t.SimulateAfterHoursPoll(At(22), title, 0);
        t.SimulateAfterHoursPoll(At(22, 30), "Something else", 0);
        Assert.Empty(Rows(app));
    }

    [Fact]
    public void Unmatched_activity_after_hours_records_nothing()
    {
        var (t, app) = Make(30, 30);
        t.SimulateAfterHoursPoll(At(22), $"Budget - {app}", 0);          // no 'quotation' word
        t.SimulateAfterHoursPoll(At(22, 30), "Something else", 0);
        Assert.Empty(Rows(app));
    }

    [Fact]
    public void Going_idle_after_hours_closes_work_at_the_idle_start_and_logs_no_idle()
    {
        var (t, app) = Make(30, 30);
        var title = $"Quotation_Acme - {app}";
        t.SimulateAfterHoursPoll(At(22), title, 0);
        t.SimulateAfterHoursPoll(At(22, 30), title, 600 + 300);          // idle for 15 min at 22:30 -> idle began 22:15
        var row = Assert.Single(Rows(app));
        Assert.Equal("22:15", row.End);
        t.SimulateAfterHoursPoll(At(23), title, 1800);                   // still idle: nothing new
        Assert.Single(Rows(app));
    }
}

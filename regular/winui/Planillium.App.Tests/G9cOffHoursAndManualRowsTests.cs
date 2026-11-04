using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>G9c acceptance H1-H7 (tag rules, off-hours paid work) and M2/M3/M5 (rows added by hand).</summary>
[Collection("TestRoot")]
public sealed class G9cOffHoursAndManualRowsTests
{
    private static IReadOnlySet<string> S(params string[] w) => new HashSet<string>(w);
    private static string Tag => DiaryTag.StudioShoo;

    private static (ActivityTracker Tracker, string App) MakeTagRule(string category, int examples, int total)
    {
        var app = "AppT" + Guid.NewGuid().ToString("N")[..10];
        var tracker = new ActivityTracker(ConfigService.Root);
        var rule = new LearnedRule(1, app, new[] { "quotation" }, category, Tag);
        var history = new List<HistoryRow> { new(app, S("quotation"), category, total) };
        var tagHistory = new List<HistoryRow> { new(app, S("quotation"), category, examples, Tag) };
        if (total > examples) tagHistory.Add(new HistoryRow(app, S("quotation"), category, total - examples, null));
        tracker.SetRulesForTest(new RuleBook(new[] { rule }, history, tagHistory));
        return (tracker, app);
    }

    private static DateTime At(int daysAgo, int h, int m = 0) => DateTime.Today.AddDays(-daysAgo).AddHours(h).AddMinutes(m);

    private static List<(string Category, string? Tag, string? RuleState, int Dur)> Rows(string app)
    {
        using var db = new Database();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT category, tag, rule_state, duration_min FROM time_diary WHERE window LIKE $w ORDER BY id";
        cmd.Parameters.AddWithValue("$w", "%" + app + "%");
        using var r = cmd.ExecuteReader();
        var rows = new List<(string, string?, string?, int)>();
        while (r.Read())
            rows.Add((r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3)));
        return rows;
    }

    private static int PaidMinutes(DateTime day)
    {
        // Other tests rewrite paid_tags in the shared config: pin the shipped default for these checks.
        PaidTags.Save(new[] { new PaidTag(Tag, "Studioshoo", 1600) });
        using var db = new Database();
        using var svc = new IncomeService(db);
        var d = DateOnly.FromDateTime(day);
        return svc.PaidEarnings(d, d).Where(e => DiaryTag.Same(e.Tag.Value, Tag)).Sum(e => e.Minutes);
    }

    // ── H4 ────────────────────────────────────────────────────────────────────

    [Fact]
    public void H4_confident_on_plan_tag_rule_after_hours_is_recorded_with_its_tag_and_counts_as_paid_work()
    {
        var (t, app) = MakeTagRule(DiaryCategory.OnPlan, 30, 30);
        var title = $"Quotation_Acme - {app}";
        t.SimulateAfterHoursPoll(At(46, 22), title, 0);
        t.SimulateAfterHoursPoll(At(46, 22, 20), "Something else", 0);
        var row = Assert.Single(Rows(app));
        Assert.Equal(DiaryCategory.OnPlan, row.Category);
        Assert.Equal(Tag, row.Tag);
        Assert.Equal(RuleStates.Auto, row.RuleState);
        Assert.Equal(20, PaidMinutes(At(46, 0)));
    }

    // ── H5 ────────────────────────────────────────────────────────────────────

    [Fact]
    public void H5_confident_neutral_tag_rule_after_hours_records_nothing()
    {
        var (t, app) = MakeTagRule(DiaryCategory.Neutral, 30, 30);
        t.SimulateAfterHoursPoll(At(47, 22), $"Quotation_Acme - {app}", 0);
        t.SimulateAfterHoursPoll(At(47, 22, 30), "Something else", 0);
        Assert.Empty(Rows(app));
        Assert.Equal(0, PaidMinutes(At(47, 0)));
    }

    // ── H6 ────────────────────────────────────────────────────────────────────

    [Fact]
    public void H6_tag_rule_below_the_bar_records_nothing_after_hours()
    {
        var (t, app) = MakeTagRule(DiaryCategory.OnPlan, 9, 9);
        t.SimulateAfterHoursPoll(At(48, 22), $"Quotation_Acme - {app}", 0);
        t.SimulateAfterHoursPoll(At(48, 22, 30), "Something else", 0);
        Assert.Empty(Rows(app));
    }

    // ── H3 / H1 evidence ──────────────────────────────────────────────────────

    [Fact]
    public void H3_tag_rule_evidence_counts_only_rows_with_the_same_category_and_tag()
    {
        var rule = new LearnedRule(1, "AppE", new[] { "quotation" }, DiaryCategory.OnPlan, Tag);
        var history = new List<HistoryRow>
        {
            new("AppE", S("quotation"), DiaryCategory.OnPlan, 12, Tag),
            new("AppE", S("quotation"), DiaryCategory.OnPlan, 8, null),         // right category, no tag
            new("AppE", S("quotation"), DiaryCategory.OffPlan, 2, Tag),
        };
        var ev = RuleMatcher.Evidence(rule, history);
        Assert.Equal(12, ev.Examples);
        Assert.Equal(22, ev.Total);
        // A category-only rule ignores the tag, as before.
        var plain = new LearnedRule(2, "AppE", new[] { "quotation" }, DiaryCategory.OnPlan);
        Assert.Equal(20, RuleMatcher.Evidence(plain, history).Examples);
    }

    [Fact]
    public void H1_proposal_for_a_tag_carries_the_tag_and_judges_precision_by_it()
    {
        var marked = new List<IReadOnlySet<string>> { S("quotation") };
        var good = new List<HistoryRow> { new("AppP", S("quotation"), DiaryCategory.OnPlan, 40, Tag) };
        var p = RuleProposer.Propose("AppP", DiaryCategory.OnPlan, marked, good, Tag);
        Assert.NotNull(p);
        Assert.Equal(Tag, p!.Tag);
        // The same words are mostly untagged in history: a tag rule must not look precise.
        var mixed = new List<HistoryRow>
        {
            new("AppP", S("quotation"), DiaryCategory.OnPlan, 3, Tag),
            new("AppP", S("quotation"), DiaryCategory.OnPlan, 40, null),
        };
        var q = RuleProposer.Propose("AppP", DiaryCategory.OnPlan, marked, mixed, Tag);
        Assert.True(q == null || !q.Evidence.Confident);
    }

    [Fact]
    public void H1_a_tag_rule_beats_an_older_category_only_rule_on_the_same_words()
    {
        var plain = new LearnedRule(1, "AppM", new[] { "quotation" }, DiaryCategory.OnPlan);
        var tagged = new LearnedRule(2, "AppM", new[] { "quotation" }, DiaryCategory.OnPlan, Tag);
        Assert.Equal(2, RuleMatcher.Match(new[] { plain, tagged }, "AppM", "Quotation - AppM")!.Id);
        Assert.Equal(2, RuleMatcher.Match(new[] { tagged, plain }, "AppM", "Quotation - AppM")!.Id);
    }

    // ── H2 storage ────────────────────────────────────────────────────────────

    [Fact]
    public void H2_rule_tag_is_stored_loaded_and_distinguishes_otherwise_equal_rules()
    {
        var app = "AppS" + Guid.NewGuid().ToString("N")[..10];
        using var db = new Database();
        var plain = LearnedRuleStore.AddRule(db, app, new[] { "budget" }, DiaryCategory.OnPlan);
        var tagged = LearnedRuleStore.AddRule(db, app, new[] { "budget" }, DiaryCategory.OnPlan, Tag);
        Assert.NotEqual(plain, tagged);
        Assert.Equal(tagged, LearnedRuleStore.AddRule(db, app, new[] { "budget" }, DiaryCategory.OnPlan, Tag.ToUpperInvariant()));
        var loaded = LearnedRuleStore.LoadRules(db).Where(r => r.App == app).ToList();
        Assert.Equal(2, loaded.Count);
        Assert.Null(loaded.Single(r => r.Id == plain).Tag);
        Assert.Equal(Tag, loaded.Single(r => r.Id == tagged).Tag);
    }

    // ── H7 ────────────────────────────────────────────────────────────────────

    [Fact]
    public void H7_paid_minutes_are_priced_the_same_at_any_time_of_day()
    {
        var day = At(49, 0);
        var date = day.ToIsoDate();
        using (var db = new Database())
        {
            db.InsertDiaryEntry(date, "14:00", "14:30", 30, DiaryCategory.OnPlan, "H7 day " + date, null, Tag);
            db.InsertDiaryEntry(date, "22:00", "22:30", 30, DiaryCategory.OnPlan, "H7 night " + date, null, Tag);
        }
        Assert.Equal(60, PaidMinutes(day));
        using var db2 = new Database();
        using var svc = new IncomeService(db2);
        var d = DateOnly.FromDateTime(day);
        var eur = svc.PaidEarnings(d, d).Single(e => DiaryTag.Same(e.Tag.Value, Tag)).Eur;
        Assert.Equal(60 / 60.0 * new PaidTag(Tag, "Studioshoo", 1600).HourlyEur, eur, 6);
    }

    // ── M2 ────────────────────────────────────────────────────────────────────

    [Fact]
    public void M2_validation_rejects_bad_times_future_and_missing_category()
    {
        var now = new DateTime(2026, 11, 4, 12, 0, 0);
        var today = new DateOnly(2026, 11, 4);
        Assert.Null(DataChangeDoor.ValidateEntry(today, new(9, 0), new(10, 0), DiaryCategory.OnPlan, now));
        Assert.NotNull(DataChangeDoor.ValidateEntry(today, new(10, 0), new(10, 0), DiaryCategory.OnPlan, now));
        Assert.NotNull(DataChangeDoor.ValidateEntry(today, new(11, 0), new(10, 0), DiaryCategory.OnPlan, now));
        Assert.NotNull(DataChangeDoor.ValidateEntry(today, new(11, 0), new(13, 0), DiaryCategory.OnPlan, now));
        Assert.NotNull(DataChangeDoor.ValidateEntry(today.AddDays(1), new(9, 0), new(10, 0), DiaryCategory.OnPlan, now));
        Assert.NotNull(DataChangeDoor.ValidateEntry(today, new(9, 0), new(10, 0), "", now));
    }

    // ── M1 / M3 / M4 / M5 ─────────────────────────────────────────────────────

    private static List<(long Id, string Start, string End, string Category, string? Tag, string? State, string? Desc)> DayRows(DateOnly d)
    {
        using var db = new Database();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id, start_time, end_time, category, tag, rule_state, description FROM time_diary WHERE date=$d ORDER BY start_time";
        cmd.Parameters.AddWithValue("$d", d.ToIsoDate());
        using var r = cmd.ExecuteReader();
        var rows = new List<(long, string, string, string, string?, string?, string?)>();
        while (r.Read())
            rows.Add((r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return rows;
    }

    [Fact]
    public void M1_M3_M4_added_row_is_manual_counts_as_paid_work_and_undo_removes_it()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(-50));
        var res = DataChangeDoor.AddEntry(d, new(21, 0), new(22, 30), DiaryCategory.OnPlan, Tag, "late invoice");
        var row = Assert.Single(DayRows(d));
        Assert.Equal(("21:00", "22:30", DiaryCategory.OnPlan, Tag, RuleStates.Manual, "late invoice"),
            (row.Start, row.End, row.Category, row.Tag, row.State, row.Desc));
        Assert.Equal(90, PaidMinutes(d.ToDateTime(TimeOnly.MinValue)));
        Assert.True(File.Exists(res.BackupFile));

        var undo = DataChangeDoor.Undo(res.ChangeId);
        Assert.Equal(1, undo.Restored);
        Assert.Empty(DayRows(d));
    }

    [Fact]
    public void M3_undo_skips_an_added_row_that_was_changed_since()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(-51));
        var res = DataChangeDoor.AddEntry(d, new(20, 0), new(21, 0), DiaryCategory.OnPlan, null, null);
        var id = Assert.Single(DayRows(d)).Id;
        DataChangeDoor.Apply("test re-mark", new[] { id }, DataChangeDoor.Field.DiaryCategory, DiaryCategory.OffPlan);
        var undo = DataChangeDoor.Undo(res.ChangeId);
        Assert.Equal(0, undo.Restored);
        Assert.Equal(1, undo.Skipped);
        Assert.Single(DayRows(d));
    }

    [Fact]
    public void M5_any_overlap_is_refused_naming_the_clashing_row_and_touching_ends_are_fine()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(-52));
        DataChangeDoor.AddEntry(d, new(9, 0), new(10, 0), DiaryCategory.OnPlan, null, null);

        foreach (var (s, e) in new[] { (new TimeOnly(9, 30), new TimeOnly(10, 30)),   // overlaps the end
                                       (new TimeOnly(8, 0), new TimeOnly(9, 30)),     // overlaps the start
                                       (new TimeOnly(9, 15), new TimeOnly(9, 45)),    // inside
                                       (new TimeOnly(8, 0), new TimeOnly(11, 0)) })   // around
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DataChangeDoor.AddEntry(d, s, e, DiaryCategory.Neutral, null, null));
            Assert.Contains("09:00–10:00", ex.Message);
        }
        Assert.Single(DayRows(d));                                                    // nothing was saved

        DataChangeDoor.AddEntry(d, new(10, 0), new(11, 0), DiaryCategory.Neutral, null, null);   // touches the end
        DataChangeDoor.AddEntry(d, new(8, 0), new(9, 0), DiaryCategory.Neutral, null, null);     // touches the start
        Assert.Equal(3, DayRows(d).Count);
    }

    [Fact]
    public void M5_a_tracked_row_also_blocks_a_manual_one()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(-53));
        using (var db = new Database())
            db.InsertDiaryEntry(d.ToIsoDate(), "13:00", "14:00", 60, DiaryCategory.OnPlan, "M5 tracked window", null);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataChangeDoor.AddEntry(d, new(13, 30), new(14, 30), DiaryCategory.OnPlan, null, null));
        Assert.Contains("M5 tracked window", ex.Message);
    }

    [Fact]
    public void M2_the_door_refuses_a_future_entry_and_writes_nothing()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        Assert.Throws<InvalidOperationException>(() =>
            DataChangeDoor.AddEntry(d, new(9, 0), new(10, 0), DiaryCategory.OnPlan, null, null));
        Assert.Empty(DayRows(d));
    }

    [Fact]
    public void Backups_made_in_the_same_second_keep_the_newest_one()
    {
        // Found by the M1 test: "<stamp>-2.db" sorted BEFORE "<stamp>.db", so the newest backup of a
        // second was the first one pruned once more than five existed.
        string last = "";
        for (var i = 0; i < 8; i++) last = DataChangeDoor.MakeBackup();
        Assert.True(File.Exists(last));
        Assert.Equal(last, DataChangeDoor.ListBackups().First());
    }

    // ── undo of tag rules ─────────────────────────────────────────────────────

    [Fact]
    public void Tag_rule_survives_delete_and_undo_with_its_tag()
    {
        var app = "AppU" + Guid.NewGuid().ToString("N")[..10];
        long id;
        using (var db = new Database())
            id = LearnedRuleStore.AddRule(db, app, new[] { "budget" }, DiaryCategory.OnPlan, Tag);
        var del = DataChangeDoor.DeleteRule(id);
        using (var db = new Database())
            Assert.DoesNotContain(LearnedRuleStore.LoadRules(db), r => r.App == app);
        DataChangeDoor.Undo(del);
        using (var db = new Database())
            Assert.Equal(Tag, LearnedRuleStore.LoadRules(db).Single(r => r.App == app).Tag);
    }
}

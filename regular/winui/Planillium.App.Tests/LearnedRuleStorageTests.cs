using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>G9b stage 2: storage, permanent counts across pruning, rule_state through the
/// data-change door, rule add/delete Undo (acceptance A7, A9, C3, E2, E3).</summary>
[Collection("TestRoot")]
public sealed class LearnedRuleStorageTests
{
    private static string NewApp() => "AppZ" + Guid.NewGuid().ToString("N");

    private static long AddRow(Database db, string date, string window, string category, string? ruleState = null)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description, tag, rule_state) " +
            "VALUES ($d, '09:00', '10:00', 60, $c, $w, NULL, NULL, $rs); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$w", window);
        cmd.Parameters.AddWithValue("$rs", (object?)ruleState ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static string RuleStateOf(Database db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT rule_state FROM time_diary WHERE id=$i";
        cmd.Parameters.AddWithValue("$i", id);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? "<null>" : (string)v;
    }

    private static string CategoryOf(Database db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT category FROM time_diary WHERE id=$i";
        cmd.Parameters.AddWithValue("$i", id);
        return (string)cmd.ExecuteScalar()!;
    }

    [Fact]
    public void Schema_has_new_tables_and_columns()
    {
        using var db = new Database();
        foreach (var (table, col) in new[] { ("time_diary", "rule_state"), ("change_log_rows", "old_rule_state"),
                     ("learned_rules", "words"), ("rule_word_counts", "word_key"), ("change_log_rule_ops", "op") })
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var r = cmd.ExecuteReader();
            var names = new List<string>();
            while (r.Read()) names.Add(r.GetString(1));
            Assert.Contains(col, names);
        }
    }

    [Fact]
    public void Rule_add_dedupes_and_normalises_words()
    {
        var app = NewApp();
        using var db = new Database();
        var id = LearnedRuleStore.AddRule(db, app, new[] { "Quotations", "ACME" }, DiaryCategory.OnPlan);
        Assert.True(id > 0);
        Assert.Equal(id, LearnedRuleStore.AddRule(db, app, new[] { "acme", "quotation" }, DiaryCategory.OnPlan));
        var rule = LearnedRuleStore.GetRule(db, id)!;
        Assert.Equal(new[] { "acme", "quotation" }, rule.Words.ToArray());
        Assert.Equal(0, LearnedRuleStore.AddRule(db, app, new[] { "a", "12" }, DiaryCategory.OnPlan)); // no usable word
    }

    [Fact]
    public void Prune_tallies_old_rows_into_permanent_counts_exactly_once()
    {
        var app = NewApp();
        var old = DateTime.Today.AddDays(-200).ToString("yyyy-MM-dd");
        var recent = DateTime.Today.ToString("yyyy-MM-dd");
        using var db = new Database();
        for (var i = 0; i < 3; i++) AddRow(db, old, $"Quotation {i} - {app}", DiaryCategory.OnPlan);
        AddRow(db, old, $"Holiday - {app}", DiaryCategory.OffPlan);
        AddRow(db, recent, "Quotation recent - " + app, DiaryCategory.OnPlan);
        var rule = new LearnedRule(1, app, new[] { "quotation" }, DiaryCategory.OnPlan);

        var before = RuleMatcher.Evidence(rule, LearnedRuleStore.LoadHistory(db));
        Assert.Equal(new RuleEvidence(4, 4), before);

        db.PruneAndRollupDiary();
        var after = RuleMatcher.Evidence(rule, LearnedRuleStore.LoadHistory(db));
        Assert.Equal(before, after);                      // 3 now in counts, 1 still live: nothing lost or doubled

        db.PruneAndRollupDiary();                         // a second prune must not count anything again
        Assert.Equal(before, RuleMatcher.Evidence(rule, LearnedRuleStore.LoadHistory(db)));
    }

    [Fact]
    public void Idle_rows_never_enter_history_or_counts()
    {
        var app = NewApp();
        var old = DateTime.Today.AddDays(-200).ToString("yyyy-MM-dd");
        using var db = new Database();
        AddRow(db, old, $"Lunch - {app}", DiaryCategory.Idle);
        db.PruneAndRollupDiary();
        Assert.DoesNotContain(LearnedRuleStore.LoadHistory(db), h => h.App == app);
    }

    [Fact]
    public void Door_category_apply_marks_manual_and_undo_restores_previous_state()
    {
        var app = NewApp();
        var today = DateTime.Today.AddDays(-45).ToString("yyyy-MM-dd");   // not today: door writes re-score the day, which would leak into other tests
        using var db = new Database();
        var a = AddRow(db, today, $"Quotation a - {app}", DiaryCategory.OffPlan, RuleStates.Suggested);
        var b = AddRow(db, today, $"Quotation b - {app}", DiaryCategory.Neutral);
        var res = DataChangeDoor.Apply("test", new[] { a, b }, DataChangeDoor.Field.DiaryCategory, DiaryCategory.OnPlan);
        Assert.Equal(2, res.Changed);
        Assert.Equal(RuleStates.Manual, RuleStateOf(db, a));
        Assert.Equal(RuleStates.Manual, RuleStateOf(db, b));

        var undo = DataChangeDoor.Undo(res.ChangeId);
        Assert.Equal(2, undo.Restored);
        Assert.Equal(0, undo.Skipped);
        Assert.Equal(RuleStates.Suggested, RuleStateOf(db, a));
        Assert.Equal("<null>", RuleStateOf(db, b));
        Assert.Equal(DiaryCategory.OffPlan, CategoryOf(db, a));
    }

    [Fact]
    public void Door_tag_apply_leaves_rule_state_alone()
    {
        var app = NewApp();
        var today = DateTime.Today.AddDays(-45).ToString("yyyy-MM-dd");   // not today: door writes re-score the day, which would leak into other tests
        using var db = new Database();
        var a = AddRow(db, today, $"Quotation a - {app}", DiaryCategory.OnPlan, RuleStates.Auto);
        var res = DataChangeDoor.Apply("tag", new[] { a }, DataChangeDoor.Field.DiaryTag, DiaryTag.Procrastination);
        Assert.Equal(RuleStates.Auto, RuleStateOf(db, a));
        Assert.Equal(1, DataChangeDoor.Undo(res.ChangeId).Restored);
        Assert.Equal(RuleStates.Auto, RuleStateOf(db, a));
    }

    [Fact]
    public void Manual_edit_of_category_marks_manual()
    {
        var app = NewApp();
        var today = DateTime.Today.AddDays(-45).ToString("yyyy-MM-dd");   // not today: door writes re-score the day, which would leak into other tests
        using var db = new Database();
        var a = AddRow(db, today, $"Quotation a - {app}", DiaryCategory.OnPlan, RuleStates.Auto);
        db.UpdateDiaryEntry(a, "09:00", "10:00", 60, DiaryCategory.OffPlan, "x", null);
        Assert.Equal(RuleStates.Manual, RuleStateOf(db, a));
    }

    [Fact]
    public void Undo_of_a_change_removes_the_rules_it_created()
    {
        var app = NewApp();
        var today = DateTime.Today.AddDays(-45).ToString("yyyy-MM-dd");   // not today: door writes re-score the day, which would leak into other tests
        using var db = new Database();
        var a = AddRow(db, today, $"Quotation a - {app}", DiaryCategory.Neutral);
        var res = DataChangeDoor.Apply("mark", new[] { a }, DataChangeDoor.Field.DiaryCategory, DiaryCategory.OnPlan);
        var rid = LearnedRuleStore.AddRule(db, app, new[] { "quotation" }, DiaryCategory.OnPlan);
        DataChangeDoor.RecordRulesAdded(res.ChangeId, new[] { rid });

        var undo = DataChangeDoor.Undo(res.ChangeId);
        Assert.Equal(1, undo.RulesRemoved);
        Assert.Null(LearnedRuleStore.GetRule(db, rid));
    }

    [Fact]
    public void Deleting_a_rule_goes_through_the_door_and_undo_brings_it_back_rows_untouched()
    {
        var app = NewApp();
        var today = DateTime.Today.AddDays(-45).ToString("yyyy-MM-dd");   // not today: door writes re-score the day, which would leak into other tests
        using var db = new Database();
        var row = AddRow(db, today, $"Quotation a - {app}", DiaryCategory.OnPlan, RuleStates.Auto);
        var rid = LearnedRuleStore.AddRule(db, app, new[] { "quotation" }, DiaryCategory.OnPlan);

        var changeId = DataChangeDoor.DeleteRule(rid);
        Assert.True(changeId > 0);
        Assert.Null(LearnedRuleStore.GetRule(db, rid));
        Assert.Equal(RuleStates.Auto, RuleStateOf(db, row));          // E2: acted-on rows keep their values
        Assert.Contains(DataChangeDoor.History(), c => c.Id == changeId && c.Field == DataChangeDoor.Field.LearnedRules);

        var undo = DataChangeDoor.Undo(changeId);
        Assert.Equal(1, undo.RulesRestored);
        Assert.Contains(LearnedRuleStore.LoadRules(db), r => r.App == app && r.Words.SequenceEqual(new[] { "quotation" }));
    }

    [Fact]
    public void Clear_activity_history_wipes_rules_and_counts()
    {
        var app = NewApp();
        var old = DateTime.Today.AddDays(-200).ToString("yyyy-MM-dd");
        using var db = new Database();
        AddRow(db, old, $"Quotation - {app}", DiaryCategory.OnPlan);
        db.PruneAndRollupDiary();
        LearnedRuleStore.AddRule(db, app, new[] { "quotation" }, DiaryCategory.OnPlan);
        Assert.Contains(LearnedRuleStore.LoadHistory(db), h => h.App == app);

        db.ClearActivityHistory();
        Assert.Empty(LearnedRuleStore.LoadRules(db));
        Assert.Empty(LearnedRuleStore.LoadHistory(db));
    }
}

using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>2026-11-12: "Apply rules to history" - the current learned rules run over old diary rows.
/// Manual, idle and day-off rows are never touched; Undo puts category, rule_state and tag back.</summary>
[Collection("TestRoot")]
public sealed class RulesOnHistoryTests
{
    private static long AddRow(Database db, string window, string cat, string? ruleState = null, string? tag = null, string start = "09:00")
    {
        using var c = db.CreateCommand();
        c.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description, tag, rule_state) " +
            "VALUES ('2026-09-01', $s, $e, 5, $cat, $w, NULL, $tag, $rs); SELECT last_insert_rowid();";
        c.Parameters.AddWithValue("$s", start);
        c.Parameters.AddWithValue("$e", start.Replace(":00", ":05"));
        c.Parameters.AddWithValue("$cat", cat);
        c.Parameters.AddWithValue("$w", window);
        c.Parameters.AddWithValue("$tag", (object?)tag ?? DBNull.Value);
        c.Parameters.AddWithValue("$rs", (object?)ruleState ?? DBNull.Value);
        return Convert.ToInt64(c.ExecuteScalar());
    }

    private static (string Cat, string? State, string? Tag) Get(Database db, long id)
    {
        using var c = db.CreateCommand();
        c.CommandText = "SELECT category, rule_state, tag FROM time_diary WHERE id=$i";
        c.Parameters.AddWithValue("$i", id);
        using var r = c.ExecuteReader();
        r.Read();
        return (r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
    }

    [Fact]
    public void Rules_set_matching_rows_leave_manual_idle_and_unmatched_alone_and_Undo_restores()
    {
        long hit, hitTagged, manual, idle, other;
        using (var db = new Database())
        {
            DataChangeDoor.AddRuleByHand("Excel", new[] { "zyqquote" }, DiaryCategory.OnPlan, null);
            DataChangeDoor.AddRuleByHand("Word", new[] { "zyqinvoice" }, DiaryCategory.Neutral, "studioshoo");
            hit = AddRow(db, "Zyqquote_Acme.xlsx - Excel", DiaryCategory.Neutral, start: "09:00");
            hitTagged = AddRow(db, "Zyqinvoice 12.docx - Word", DiaryCategory.OffPlan, start: "09:10");
            manual = AddRow(db, "Zyqquote_Beta.xlsx - Excel", DiaryCategory.OffPlan, RuleStates.Manual, start: "09:20");
            idle = AddRow(db, "idle", DiaryCategory.Idle, start: "09:30");
            other = AddRow(db, "Holiday.xlsx - Excel", DiaryCategory.Neutral, start: "09:40");
        }

        var preview = DataChangeDoor.PreviewRulesOnHistory();
        Assert.True(preview.Changed >= 2);
        Assert.True(preview.NoRule >= 1);

        var result = DataChangeDoor.ApplyRulesToHistory();
        Assert.True(result.Changed >= 2);
        using (var db = new Database())
        {
            Assert.Equal((DiaryCategory.OnPlan, RuleStates.Auto, (string?)null), Get(db, hit));
            Assert.Equal((DiaryCategory.Neutral, RuleStates.Auto, "studioshoo"), Get(db, hitTagged));
            Assert.Equal((DiaryCategory.OffPlan, RuleStates.Manual, (string?)null), Get(db, manual));
            Assert.Equal((DiaryCategory.Idle, (string?)null, (string?)null), Get(db, idle));
            Assert.Equal((DiaryCategory.Neutral, (string?)null, (string?)null), Get(db, other));
        }
        // a second pass has nothing left to do
        Assert.Equal(0, DataChangeDoor.PreviewRulesOnHistory().Changed);

        var undo = DataChangeDoor.Undo(result.ChangeId);
        Assert.True(undo.Restored >= 2);
        using (var db = new Database())
        {
            Assert.Equal((DiaryCategory.Neutral, (string?)null, (string?)null), Get(db, hit));
            Assert.Equal((DiaryCategory.OffPlan, (string?)null, (string?)null), Get(db, hitTagged));
        }
    }
}

using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>2026-11-05 batch (context/acceptance/2026-11-05-diary-rules-batch.md): A2/A3 rule edit, A5 more than
/// two rule words, A6 dialog windows take the previous app, A7 idle answers remember their category,
/// A8 suggestions follow recent use.</summary>
[Collection("TestRoot")]
public sealed class RulesBatch20261105Tests
{
    private static IReadOnlySet<string> W(params string[] w) => new HashSet<string>(w);

    // ── A5 (OR since 2026-11-11: words are added only to cover marked rows the first missed) ──
    [Fact]
    public void Proposer_adds_further_OR_words_to_cover_marked_rows()
    {
        var marked = new[] { W("aaa"), W("bbb"), W("ccc") };
        var history = marked.Select(m => new HistoryRow("App", m, "on_plan", 5)).ToList();
        var p = RuleProposer.Propose("App", "on_plan", marked, history);
        Assert.NotNull(p);
        Assert.Equal(new[] { "aaa", "bbb", "ccc" }, p!.Words.ToArray());
    }

    [Fact]
    public void Proposer_stays_at_one_word_when_it_is_already_pure()
    {
        var marked = new[] { W("a", "b") };
        var history = new List<HistoryRow> { new("App", W("a", "b"), "on_plan", 5) };
        Assert.Single(RuleProposer.Propose("App", "on_plan", marked, history)!.Words);
    }

    // ── A6 ────────────────────────────────────────────────────────────────────
    [Fact]
    public void Dialog_window_takes_the_app_opened_just_before()
    {
        var d = new DateOnly(2026, 11, 5);
        var map = AppNames.InheritedFilterGroups(new (long, DateOnly, string, string)[]
        {
            (1, d, "09:00", "Report - Word"),
            (2, d, "09:05", "Save As"),                 // no dash: a dialog
            (3, d, "09:10", "Inbox - Chrome"),
            (4, d, "09:11", "Are you sure?"),
        });
        Assert.Equal("Word", map[2]);
        Assert.Equal("Chrome", map[4]);
        Assert.Equal("Word", map[1]);
    }

    [Fact]
    public void Dialog_with_nothing_before_it_and_idle_rows_stay_as_they_were()
    {
        var d = new DateOnly(2026, 11, 5);
        var map = AppNames.InheritedFilterGroups(new (long, DateOnly, string, string)[]
        {
            (1, d, "08:00", "Save As"),
            (2, d, "08:05", "Report - Word"),
            (3, d, "08:10", "idle"),
            (4, d, "08:20", "Open"),
        });
        Assert.Equal(AppNames.OtherGroup, map[1]);
        Assert.Equal(AppNames.OtherGroup, map[3]);      // idle is not an app, keeps its own value
        Assert.Equal("Word", map[4]);                   // idle did not become the "previous app"
    }

    // ── A7 / A8 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Idle_answer_category_is_remembered_from_the_last_time()
    {
        var desc = "idle-learn-" + Guid.NewGuid();
        var tracker = new ActivityTracker(ConfigService.Root);
        var t = DateTime.Today.AddHours(7);
        tracker.LogIdleAnswer(t, 5, desc, DiaryCategory.OffPlan);
        tracker.LogIdleAnswer(t.AddMinutes(30), 5, desc, DiaryCategory.OnPlan);   // changed its mind: latest wins
        using var db = new Database();
        Assert.Equal(DiaryCategory.OnPlan, db.IdleAnswerCategories()[desc.ToUpperInvariant()]);
    }

    [Fact]
    public void Suggestions_put_recent_descriptions_with_the_same_category_first()
    {
        var recent = "sugg-recent-" + Guid.NewGuid();
        var tracker = new ActivityTracker(ConfigService.Root);
        tracker.LogIdleAnswer(DateTime.Today.AddHours(6), 5, recent, DiaryCategory.Neutral);
        using var db = new Database();
        var list = db.MostFrequentDescriptions(topN: 8, category: DiaryCategory.Neutral);
        Assert.Contains(recent, list);
        Assert.True(list.Count <= 8);
    }

    // ── A2 / A3 ───────────────────────────────────────────────────────────────
    [Fact]
    public void EditRule_changes_words_category_and_undo_restores_it()
    {
        var app = "AppE" + Guid.NewGuid().ToString("N")[..10];
        long id;
        using (var db = new Database())
            id = LearnedRuleStore.AddRule(db, app, new[] { "budget" }, DiaryCategory.OnPlan);
        var change = DataChangeDoor.EditRule(id, new[] { "budget", "quarter", "forecast" }, DiaryCategory.OffPlan, null);
        using (var db = new Database())
        {
            var r = LearnedRuleStore.LoadRules(db).Single(x => x.App == app);
            Assert.Equal(3, r.Words.Count);
            Assert.Equal(DiaryCategory.OffPlan, r.Category);
        }
        DataChangeDoor.Undo(change);
        using (var db = new Database())
        {
            var r = LearnedRuleStore.LoadRules(db).Single(x => x.App == app);
            Assert.Equal(new[] { "budget" }, r.Words);
            Assert.Equal(DiaryCategory.OnPlan, r.Category);
        }
    }

    [Fact]
    public void EditRule_refuses_no_words_no_change_and_duplicates()
    {
        var app = "AppR" + Guid.NewGuid().ToString("N")[..10];
        long a, b;
        using (var db = new Database())
        {
            a = LearnedRuleStore.AddRule(db, app, new[] { "alpha" }, DiaryCategory.OnPlan);
            b = LearnedRuleStore.AddRule(db, app, new[] { "beta" }, DiaryCategory.OnPlan);
        }
        // (empty words are an app-only rule since 2026-11-12, no longer refused)
        Assert.Throws<ArgumentException>(() => DataChangeDoor.EditRule(a, new[] { "and" }, DiaryCategory.OnPlan, null));
        var solo = "AppS" + Guid.NewGuid().ToString("N")[..10];
        long c;
        using (var dbs = new Database()) c = LearnedRuleStore.AddRule(dbs, solo, new[] { "alpha" }, DiaryCategory.OnPlan);
        Assert.Throws<ArgumentException>(() => DataChangeDoor.EditRule(c, new[] { "alpha" }, DiaryCategory.OnPlan, null));
        // same app + category + tag as another rule: the words merge into one rule (user 2026-11-11)
        Assert.True(DataChangeDoor.EditRule(b, new[] { "beta" }, DiaryCategory.OnPlan, null) > 0);
        using var db2 = new Database();
        var left = LearnedRuleStore.LoadRules(db2).Where(x => x.App == app).ToList();
        Assert.Single(left);
        Assert.Equal(new[] { "alpha", "beta" }, left[0].Words.OrderBy(w => w).ToArray());
    }
}

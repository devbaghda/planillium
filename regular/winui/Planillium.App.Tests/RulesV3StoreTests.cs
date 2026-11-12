using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>2026-11-11 rules v3 (context/acceptance/2026-11-11-rules-or-words-one-line-per-app.md):
/// A (add by hand), W5/A3 (merge), E2/J3 (junk refused), L3 (group edit/delete undo).</summary>
[Collection("TestRoot")]
public sealed class RulesV3StoreTests
{
    private static string NewApp() => "AppV3" + Guid.NewGuid().ToString("N")[..10];
    private static List<LearnedRule> Rules(string app) { using var db = new Database(); return LearnedRuleStore.LoadRules(db).Where(r => r.App == app).ToList(); }

    [Fact]
    public void Add_by_hand_creates_a_rule_and_undo_removes_it()
    {
        var app = NewApp();
        var change = DataChangeDoor.AddRuleByHand(app, new[] { "Contract", "Budget" }, DiaryCategory.OnPlan, "studioshoo");
        var r = Assert.Single(Rules(app));
        Assert.Equal(new[] { "budget", "contract" }, r.Words.OrderBy(w => w).ToArray());
        Assert.Equal("studioshoo", r.Tag);
        DataChangeDoor.Undo(change);
        Assert.Empty(Rules(app));
    }

    [Fact]
    public void Add_to_same_app_category_tag_merges_and_undo_restores_the_old_rule()
    {
        var app = NewApp();
        DataChangeDoor.AddRuleByHand(app, new[] { "alpha" }, DiaryCategory.OnPlan, null);
        var change = DataChangeDoor.AddRuleByHand(app, new[] { "beta" }, DiaryCategory.OnPlan, null);
        Assert.Equal(new[] { "alpha", "beta" }, Assert.Single(Rules(app)).Words.OrderBy(w => w).ToArray());
        DataChangeDoor.Undo(change);
        Assert.Equal(new[] { "alpha" }, Assert.Single(Rules(app)).Words.ToArray());
    }

    [Fact]
    public void Same_words_again_changes_nothing()
    {
        var app = NewApp();
        DataChangeDoor.AddRuleByHand(app, new[] { "alpha" }, DiaryCategory.OnPlan, null);
        Assert.Throws<ArgumentException>(() => DataChangeDoor.AddRuleByHand(app, new[] { "alpha" }, DiaryCategory.OnPlan, null));
    }

    [Theory]
    [InlineData("and")] [InlineData("google")] [InlineData("2026")]
    public void Junk_words_are_refused_and_nothing_is_written(string word)
    {
        var app = NewApp();
        Assert.Throws<ArgumentException>(() => DataChangeDoor.AddRuleByHand(app, new[] { word }, DiaryCategory.OnPlan, null));
        Assert.Empty(Rules(app));
    }

    // ── app-only rules (user 2026-11-12: "AnyDesk in any case is studioshoo activity") ──

    [Fact]
    public void App_only_rule_can_be_added_matches_every_title_acts_alone_and_undo_removes_it()
    {
        var app = NewApp();
        var change = DataChangeDoor.AddRuleByHand(app, Array.Empty<string>(), DiaryCategory.Neutral, "studioshoo");
        var r = Assert.Single(Rules(app));
        Assert.Empty(r.Words);
        Assert.NotNull(RuleMatcher.Match(new[] { r }, app, "anything at all - 2 windows"));
        Assert.NotNull(RuleMatcher.Match(new[] { r }, app, null));
        Assert.True(RuleMatcher.Evidence(r, Array.Empty<HistoryRow>()).Confident);   // acts with no history
        DataChangeDoor.Undo(change);
        Assert.Empty(Rules(app));
    }

    [Fact]
    public void A_rule_with_a_word_hit_beats_the_app_only_rule_which_is_the_fallback()
    {
        var appOnly = new LearnedRule(1, "X", Array.Empty<string>(), DiaryCategory.OffPlan);
        var word = new LearnedRule(2, "X", new[] { "budget" }, DiaryCategory.OnPlan);
        Assert.Equal(2, RuleMatcher.Match(new[] { appOnly, word }, "X", "Budget plan")!.Id);
        Assert.Equal(1, RuleMatcher.Match(new[] { appOnly, word }, "X", "something else")!.Id);
        Assert.Null(RuleMatcher.Match(new[] { appOnly }, "Y", "something else"));
    }

    [Fact]
    public void App_only_rule_swallows_the_groups_word_rules_but_a_word_rule_never_merges_into_it()
    {
        var app = NewApp();
        DataChangeDoor.AddRuleByHand(app, new[] { "alpha" }, DiaryCategory.Neutral, "studioshoo");
        DataChangeDoor.AddRuleByHand(app, Array.Empty<string>(), DiaryCategory.Neutral, "studioshoo");
        Assert.Empty(Assert.Single(Rules(app)).Words);
        DataChangeDoor.AddRuleByHand(app, new[] { "beta" }, DiaryCategory.Neutral, "studioshoo");
        Assert.Equal(2, Rules(app).Count);
        DataChangeDoor.AddRuleByHand(app, Array.Empty<string>(), DiaryCategory.Neutral, "studioshoo");   // swallows beta again
        Assert.Empty(Assert.Single(Rules(app)).Words);
    }

    [Fact]
    public void Deleting_an_app_only_rule_and_undoing_it_brings_it_back()
    {
        var app = NewApp();
        DataChangeDoor.AddRuleByHand(app, Array.Empty<string>(), DiaryCategory.OnPlan, null);
        var del = DataChangeDoor.DeleteRules(Rules(app).Select(r => r.Id).ToList());
        Assert.Empty(Rules(app));
        DataChangeDoor.Undo(del);
        Assert.Empty(Assert.Single(Rules(app)).Words);
    }

    [Fact]
    public void No_app_is_refused() =>
        Assert.Throws<ArgumentException>(() => DataChangeDoor.AddRuleByHand("  ", new[] { "alpha" }, DiaryCategory.OnPlan, null));

    [Fact]
    public void Group_edit_replaces_all_stored_rules_of_the_sub_row_and_undo_brings_them_back()
    {
        var app = NewApp();
        long a, b;
        using (var db = new Database())
        {
            a = LearnedRuleStore.AddRule(db, app, new[] { "alpha", "gamma" }, DiaryCategory.OnPlan);
            b = LearnedRuleStore.AddRule(db, app, new[] { "beta" }, DiaryCategory.OnPlan);
        }
        var change = DataChangeDoor.SaveRule(null, new[] { a, b }, new[] { "alpha", "beta", "delta" }, DiaryCategory.OnPlan, null);
        Assert.Equal(new[] { "alpha", "beta", "delta" }, Assert.Single(Rules(app)).Words.OrderBy(w => w).ToArray());
        DataChangeDoor.Undo(change);
        var back = Rules(app);
        Assert.Equal(2, back.Count);
        Assert.Contains(back, r => r.Words.SequenceEqual(new[] { "beta" }));
    }

    [Fact]
    public void Group_delete_is_one_undoable_change()
    {
        var app = NewApp();
        long a, b;
        using (var db = new Database())
        {
            a = LearnedRuleStore.AddRule(db, app, new[] { "alpha" }, DiaryCategory.OnPlan);
            b = LearnedRuleStore.AddRule(db, app, new[] { "beta" }, DiaryCategory.OnPlan);
        }
        var change = DataChangeDoor.DeleteRules(new[] { a, b });
        Assert.Empty(Rules(app));
        DataChangeDoor.Undo(change);
        Assert.Equal(2, Rules(app).Count);
    }
}

/// <summary>2026-11-12 (user): a rule word also fires on its word forms (акт: акты, актов).</summary>
public sealed class WordFormMatchTests
{
    private static LearnedRule R(params string[] w) => new(1, "Word", w, DiaryCategory.Neutral, "studioshoo");

    [Theory]
    [InlineData("Акт сверки")] [InlineData("Акты за октябрь")] [InlineData("Договор и актов")]
    public void Rule_word_fires_on_its_word_forms(string title) =>
        Assert.NotNull(RuleMatcher.Match(new[] { R("акт") }, "Word", title));

    [Fact]
    public void Rule_word_does_not_fire_when_the_title_word_only_ends_with_it() =>
        Assert.Null(RuleMatcher.Match(new[] { R("акт") }, "Word", "Фактура"));

    [Fact]
    public void Evidence_counts_word_forms()
    {
        var h = new[] { new HistoryRow("Word", new HashSet<string> { "актов" }, DiaryCategory.Neutral, 3, "studioshoo") };
        Assert.Equal(3, RuleMatcher.Evidence(R("акт"), h).Total);
    }
}

/// <summary>Word forms work the same in every script: the rule word is a root, the title word may continue it.</summary>
public sealed class WordFormScriptsTests
{
    private static LearnedRule R(string w) => new(1, "Word", new[] { w }, DiaryCategory.Neutral, "studioshoo");

    [Theory]
    [InlineData("ակտ", "Ակտեր 2026")] [InlineData("ակտ", "ակտերի ցուցակ")]               // Armenian
    [InlineData("договор", "Договорами и приложения")] [InlineData("договор", "Договора.docx")] // Russian
    [InlineData("invoice", "Invoices - May")] [InlineData("report", "Reporting dashboard")]      // English
    public void A_root_fires_on_longer_forms(string root, string title) =>
        Assert.NotNull(RuleMatcher.Match(new[] { R(NormalizeRoot(root)) }, "Word", title));

    [Theory]
    [InlineData("ակտ", "Փակտ")] [InlineData("договор", "Субдоговор")] [InlineData("invoice", "Preinvoice")]
    public void A_root_never_fires_inside_or_at_the_end_of_another_word(string root, string title) =>
        Assert.Null(RuleMatcher.Match(new[] { R(NormalizeRoot(root)) }, "Word", title));

    private static string NormalizeRoot(string w) => TitleWords.NormalizeWord(w);
}

/// <summary>Roots that change near the end (Russian/Armenian) still match; English stays strict.</summary>
public sealed class WordRootChangeTests
{
    private static LearnedRule R(string w) => new(1, "Word", new[] { TitleWords.NormalizeWord(w) }, DiaryCategory.Neutral);

    [Theory]
    [InlineData("рынок", "Рынка анализ")] [InlineData("рынок", "Рыночный отчёт")]
    [InlineData("акты", "Акт сверки")] [InlineData("актов", "Акт сверки")] [InlineData("отчёт", "Отчета за май")]
    [InlineData("ակտեր", "Ակտ 12")]
    public void A_changed_ending_still_matches(string rule, string title) =>
        Assert.NotNull(RuleMatcher.Match(new[] { R(rule) }, "Word", title));

    [Theory]
    [InlineData("рынок", "Рыба")] [InlineData("report", "Repository")] [InlineData("акт", "Фактура")]
    public void Unrelated_words_do_not_match(string rule, string title) =>
        Assert.Null(RuleMatcher.Match(new[] { R(rule) }, "Word", title));
}

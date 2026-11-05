using System.Text.Json;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>G9b stage 4: the classifier's use of learned rules (acceptance B1-B5, C1/C2/C5).</summary>
public class LearnedRuleClassifierTests
{
    private static ActivityClassifier Make(string json = "{}") =>
        new(JsonDocument.Parse(json).RootElement);

    private static IReadOnlySet<string> S(params string[] w) => new HashSet<string>(w);

    private static RuleBook Book(IEnumerable<LearnedRule> rules, params HistoryRow[] history) =>
        new(rules, history);

    [Fact]
    public void No_rules_means_keyword_lists_and_no_rule_state()
    {
        var c = Make("""{"activity_rules":{"on_plan":["excel"]}}""");
        var (cat, state) = c.ClassifyDetailed("Budget - Excel");
        Assert.Equal(DiaryCategory.OnPlan, cat);
        Assert.Null(state);
    }

    [Fact]
    public void Confident_rule_decides_alone_and_marks_auto()
    {
        var c = Make("""{"activity_rules":{"off_plan":["excel"]}}""");
        var rule = new LearnedRule(1, "Excel", new[] { "quotation" }, DiaryCategory.OnPlan);
        c.SetRules(Book(new[] { rule }, new HistoryRow("Excel", S("quotation"), DiaryCategory.OnPlan, 30)));
        var (cat, state) = c.ClassifyDetailed("Quotation_Acme.xlsx - Excel");
        Assert.Equal(DiaryCategory.OnPlan, cat);          // beats the off-plan keyword
        Assert.Equal(RuleStates.Auto, state);
    }

    [Fact]
    public void Saved_rule_with_few_examples_still_acts_at_once_and_beats_keywords()
    {
        var c = Make("""{"activity_rules":{"off_plan":["excel"]}}""");
        var rule = new LearnedRule(1, "Excel", new[] { "quotation" }, DiaryCategory.OnPlan);
        c.SetRules(Book(new[] { rule }, new HistoryRow("Excel", S("quotation"), DiaryCategory.OnPlan, 1)));
        var (cat, state) = c.ClassifyDetailed("Quotation - Excel");
        Assert.Equal(DiaryCategory.OnPlan, cat);
        Assert.Equal(RuleStates.Auto, state);
    }

    [Fact]
    public void Saved_rule_acts_even_with_no_history_at_all()
    {
        var c = Make();
        var rule = new LearnedRule(1, "Excel", new[] { "quotation" }, DiaryCategory.OnPlan);
        c.SetRules(Book(new[] { rule }));
        Assert.Equal((DiaryCategory.OnPlan, RuleStates.Auto), c.ClassifyDetailed("Quotation - Excel"));
    }

    [Fact]
    public void Most_specific_rule_wins_and_acts()
    {
        var c = Make();
        var general = new LearnedRule(1, "Excel", new[] { "quotation" }, DiaryCategory.OnPlan);
        var specific = new LearnedRule(2, "Excel", new[] { "acme", "quotation" }, DiaryCategory.OffPlan);
        c.SetRules(Book(new[] { general, specific },
            new HistoryRow("Excel", S("quotation"), DiaryCategory.OnPlan, 100),
            new HistoryRow("Excel", S("acme", "quotation"), DiaryCategory.OffPlan, 3)));
        Assert.Equal((DiaryCategory.OffPlan, RuleStates.Auto), c.ClassifyDetailed("Acme quotation - Excel"));
        Assert.Equal((DiaryCategory.OnPlan, RuleStates.Auto), c.ClassifyDetailed("Other quotation - Excel"));
    }

    [Fact]
    public void Other_app_or_missing_word_is_untouched()
    {
        var c = Make();
        var rule = new LearnedRule(1, "Excel", new[] { "quotation" }, DiaryCategory.OnPlan);
        c.SetRules(Book(new[] { rule }, new HistoryRow("Excel", S("quotation"), DiaryCategory.OnPlan, 30)));
        Assert.Null(c.ClassifyDetailed("Quotation - Word").RuleState);
        Assert.Null(c.ClassifyDetailed("Budget - Excel").RuleState);
    }
}

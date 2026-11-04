namespace Planillium.App.Tests;

using Planillium.App.Services;

/// <summary>G9b stage 1: normaliser, Wilson bar, matcher specificity, proposal finder
/// (acceptance A2/A3/A5/A6/A7, B1-B3, C1/C2/C4, G2).</summary>
public class LearnedRulesEngineTests
{
    private static HashSet<string> W(string title, string app = "Excel") => TitleWords.Normalize(title, app);
    private static IReadOnlySet<string> S(params string[] w) => new HashSet<string>(w);
    private static LearnedRule R(long id, string app, string cat, params string[] w) => new(id, app, w, cat);

    // ---- A2/A3 normaliser ----
    [Theory]
    [InlineData("Quotation_Acme.xlsx - Excel")]
    [InlineData("Quotations 2026 - Excel")]
    [InlineData("QUOTATION beta")]
    [InlineData("Quotation 2026-11-04.xlsx")]
    public void Quotation_variants_all_yield_quotation(string title) =>
        Assert.Contains("quotation", W(title));

    [Fact]
    public void App_name_numbers_dates_extensions_are_never_words()
    {
        var w = W("Budget_v2 2026-11-04 report.xlsx - Excel");
        Assert.DoesNotContain("excel", w);
        Assert.DoesNotContain("xlsx", w);
        Assert.DoesNotContain("2026", w);
        Assert.Equal(new[] { "budget", "report" }, w.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Accents_and_plural_fold()
    {
        Assert.Equal(TitleWords.Normalize("Café", ""), TitleWords.Normalize("cafe", ""));
        Assert.Equal("company", TitleWords.Stem("companies"));
        Assert.Equal("box", TitleWords.Stem("boxes"));
        Assert.Equal("class", TitleWords.Stem("class"));
    }

    // ---- B1 whole word ----
    [Fact]
    public void Whole_word_only()
    {
        var rules = new[] { R(1, "Excel", "on_plan", "quote") };
        Assert.Null(RuleMatcher.Match(rules, "Excel", "quotefree.xlsx"));
        Assert.NotNull(RuleMatcher.Match(rules, "Excel", "Quote.xlsx"));
    }

    // ---- B2 app and word ----
    [Fact]
    public void Needs_both_app_and_word()
    {
        var rules = new[] { R(1, "Excel", "on_plan", "quotation") };
        Assert.Null(RuleMatcher.Match(rules, "Word", "quotation.docx"));
        Assert.Null(RuleMatcher.Match(rules, "Excel", "budget.xlsx"));
        Assert.Equal(1, RuleMatcher.Match(rules, "excel", "Quotation.xlsx")!.Id);
    }

    // ---- B3 specificity ----
    [Fact]
    public void More_words_beats_fewer_equal_goes_to_older()
    {
        var one = R(5, "Excel", "neutral", "quotation");
        var two = R(9, "Excel", "on_plan", "acme", "quotation");
        Assert.Equal(9, RuleMatcher.Match(new[] { one, two }, "Excel", "Acme quotation")!.Id);
        Assert.Equal(5, RuleMatcher.Match(new[] { one, two }, "Excel", "Other quotation")!.Id);
        var a = R(3, "Excel", "on_plan", "alpha"); var b = R(2, "Excel", "off_plan", "beta");
        Assert.Equal(2, RuleMatcher.Match(new[] { a, b }, "Excel", "alpha beta")!.Id);
    }

    [Fact]
    public void Rule_without_words_never_matches()
    {
        Assert.Null(RuleMatcher.Match(new[] { R(1, "Excel", "on_plan") }, "Excel", "anything"));
    }

    // ---- C Wilson bar ----
    [Fact]
    public void Nine_examples_never_confident_even_if_perfect() =>
        Assert.False(Wilson.IsConfident(9, 9));

    [Fact]
    public void Bar_needs_10_examples_and_lower_bound_85()
    {
        Assert.False(Wilson.IsConfident(10, 10));   // lower bound ~0.72
        Assert.False(Wilson.IsConfident(21, 21));   // lower bound 0.845, just under
        Assert.True(Wilson.IsConfident(22, 22));    // 0.851, first perfect run that clears the bar
        Assert.True(Wilson.IsConfident(30, 30));
        Assert.False(Wilson.IsConfident(30, 40));   // 75% agreement
    }

    [Fact]
    public void Wilson_known_value()
    {
        var (lo, hi) = Wilson.Range(8, 10);
        Assert.InRange(lo, 0.490, 0.495);
        Assert.InRange(hi, 0.942, 0.946);
        Assert.Equal((0.0, 0.0), Wilson.Range(0, 0));
    }

    // ---- A7 / C4 evidence counted directly ----
    [Fact]
    public void Evidence_counts_all_history_with_counts_rows()
    {
        var hist = new List<HistoryRow>
        {
            new("Excel", S("quotation", "acme"), "on_plan", 3),
            new("Excel", S("quotation"), "on_plan", 2),
            new("Excel", S("quotation"), "off_plan", 1),   // contradicting mark lowers range (C4)
            new("Excel", S("budget"), "on_plan", 7),
            new("Word", S("quotation"), "on_plan", 4),
        };
        var ev = RuleMatcher.Evidence(R(1, "Excel", "on_plan", "quotation"), hist);
        Assert.Equal(6, ev.Total);
        Assert.Equal(5, ev.Examples);
        var before = Wilson.Range(5, 5).Lower;
        Assert.True(ev.Range.Lower < before);
    }

    // ---- A2/A5/A6 proposals ----
    [Fact]
    public void Proposes_shared_word_rare_elsewhere()
    {
        var marked = new[] { W("Quotation_Acme.xlsx"), W("Quotations 2026"), W("quotation beta") };
        var hist = marked.Select(m => new HistoryRow("Excel", m, "on_plan")).ToList();
        hist.Add(new HistoryRow("Excel", W("Holiday budget"), "off_plan", 5));
        var p = RuleProposer.Propose("Excel", "on_plan", marked, hist)!;
        Assert.Equal(new[] { "quotation" }, p.Words);
        Assert.Equal(3, p.Evidence.Examples);
        Assert.Equal(3, p.Evidence.Total);
    }

    [Fact]
    public void Prefers_word_that_is_rare_in_other_rows_of_the_app()
    {
        // "report" and "acme" both shared; "report" also appears in many off-plan rows.
        var marked = new[] { W("Acme report"), W("Acme report q2"), W("Acme report q3") };
        var hist = marked.Select(m => new HistoryRow("Excel", m, "on_plan")).ToList();
        hist.Add(new HistoryRow("Excel", W("Holiday report"), "off_plan", 10));
        var p = RuleProposer.Propose("Excel", "on_plan", marked, hist)!;
        Assert.Contains("acme", p.Words);
    }

    [Fact]
    public void No_shared_word_gives_no_proposal_never_empty_rule()
    {
        var marked = new[] { W("alpha one"), W("beta two"), W("gamma three"), W("delta four") };
        var hist = marked.Select(m => new HistoryRow("Excel", m, "on_plan")).ToList();
        Assert.Null(RuleProposer.Propose("Excel", "on_plan", marked, hist));
    }

    [Fact]
    public void Idle_or_nothing_marked_gives_no_proposal()
    {
        var marked = new[] { W("quotation") };
        var hist = new[] { new HistoryRow("Excel", marked[0], "idle") };
        Assert.Null(RuleProposer.Propose("Excel", "idle", marked, hist));
        Assert.Null(RuleProposer.Propose("Excel", "on_plan", Array.Empty<IReadOnlySet<string>>(), hist));
    }

    [Fact]
    public void Adds_second_word_when_first_is_not_pure()
    {
        var marked = new[] { W("acme quotation"), W("acme quotation v2"), W("acme quotation final") };
        var hist = marked.Select(m => new HistoryRow("Excel", m, "on_plan")).ToList();
        hist.Add(new HistoryRow("Excel", W("holiday quotation"), "off_plan", 6));
        hist.Add(new HistoryRow("Excel", W("acme holiday"), "off_plan", 6));
        var p = RuleProposer.Propose("Excel", "on_plan", marked, hist)!;
        Assert.Equal(new[] { "acme", "quotation" }, p.Words.OrderBy(x => x).ToArray());
    }
}

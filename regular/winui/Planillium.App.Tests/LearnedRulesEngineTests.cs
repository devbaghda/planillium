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
    public void Whole_word_start_only()
    {
        // 2026-11-12: a rule word fires on word forms (title word STARTS with it), never on a word that merely contains it
        var rules = new[] { R(1, "Excel", "on_plan", "quote") };
        Assert.Null(RuleMatcher.Match(rules, "Excel", "freequote.xlsx"));
        Assert.NotNull(RuleMatcher.Match(rules, "Excel", "quotes.xlsx"));
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
    public void Rule_without_words_is_an_app_only_rule_matching_its_own_app_only()
    {
        Assert.NotNull(RuleMatcher.Match(new[] { R(1, "Excel", "on_plan") }, "Excel", "anything"));
        Assert.Null(RuleMatcher.Match(new[] { R(1, "Excel", "on_plan") }, "Word", "anything"));
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
    public void Unshared_words_are_ORed_into_one_rule_and_junk_only_gives_none()
    {
        var marked = new[] { W("alpha one"), W("beta two"), W("gamma three"), W("delta four") };
        var hist = marked.Select(m => new HistoryRow("Excel", m, "on_plan")).ToList();
        var p = RuleProposer.Propose("Excel", "on_plan", marked, hist)!;
        Assert.InRange(p.Words.Count, 1, RuleProposer.MaxWords);
        Assert.All(marked, m => Assert.Contains(p.Words, w => m.Contains(w)));   // every marked row is covered
        // only filler / app-name words left: no proposal, never an empty rule
        var junk = new[] { S("and"), S("google") };
        Assert.Null(RuleProposer.Propose("Chrome", "on_plan", junk, junk.Select(m => new HistoryRow("Chrome", m, "on_plan")).ToList()));
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
    public void Inflected_forms_marked_together_become_one_word_that_covers_them_all()
    {
        var marked = new[] { S("contract"), S("contracts"), S("contracted") };
        var hist = marked.Select(m => new HistoryRow("Word", m, "on_plan")).ToList();
        var p = RuleProposer.Propose("Word", "on_plan", marked, hist)!;
        Assert.Equal(new[] { "contract" }, p.Words.ToArray());   // "contract" already covers contracts/contracted
    }

    [Fact]
    public void A_further_word_is_refused_when_it_makes_the_rule_impure()
    {
        var marked = new[] { S("acme", "plan"), S("zeta", "plan") };
        var hist = marked.Select(m => new HistoryRow("Excel", m, "on_plan")).ToList();
        hist.Add(new HistoryRow("Excel", S("zeta"), "off_plan", 20));   // "zeta" alone is mostly off-plan
        var p = RuleProposer.Propose("Excel", "on_plan", marked, hist)!;
        Assert.DoesNotContain("zeta", p.Words);
    }

    // ---- 2026-11-11 OR matching ----
    [Fact]
    public void Any_one_word_matches_and_more_hits_win()
    {
        var either = R(4, "Excel", "on_plan", "contract", "contracts");
        Assert.Equal(4, RuleMatcher.Match(new[] { either }, "Excel", "contracts_2 - Excel")!.Id);
        Assert.Null(RuleMatcher.Match(new[] { either }, "Excel", "report - Excel"));
        var two = R(9, "Excel", "off_plan", "acme", "report");
        var one = R(2, "Excel", "on_plan", "report");
        Assert.Equal(9, RuleMatcher.Match(new[] { one, two }, "Excel", "acme report")!.Id);       // 2 hits beat 1
        Assert.Equal(2, RuleMatcher.Match(new[] { one, two }, "Excel", "report only")!.Id);       // 1 hit each: older
        var tagged = new LearnedRule(7, "Excel", new[] { "report" }, "neutral", "studioshoo");
        Assert.Equal(7, RuleMatcher.Match(new[] { R(8, "Excel", "on_plan", "report"), tagged }, "Excel", "report")!.Id);   // tie: tag
    }

    [Fact]
    public void Evidence_counts_rows_containing_any_rule_word()
    {
        var hist = new List<HistoryRow>
        {
            new("Excel", S("contract"), "on_plan", 2), new("Excel", S("contracts"), "on_plan", 3),
            new("Excel", S("contracts"), "off_plan", 1), new("Excel", S("budget"), "on_plan", 9),
        };
        var ev = RuleMatcher.Evidence(R(1, "Excel", "on_plan", "contract", "contracts"), hist);
        Assert.Equal((5, 6), (ev.Examples, ev.Total));
    }

    [Theory]
    [InlineData("and")] [InlineData("The")] [InlineData("для")] [InlineData("ab")] [InlineData("google")]
    [InlineData("Chrome")] [InlineData("2026")] [InlineData("docx")] [InlineData("excel")]
    public void Junk_words_are_refused_when_typed(string word)
    {
        var r = TitleWords.ParseTyped(word, "Word");
        Assert.Empty(r.Words);
        Assert.NotEmpty(r.Refused);
    }

    [Fact]
    public void Typed_words_split_on_comma_and_keep_good_ones()
    {
        var r = TitleWords.ParseTyped("Contract, budget ,\nакт; and", "Word");
        Assert.Equal(new[] { "contract", "budget", "акт" }, r.Words.ToArray());
        Assert.Equal(new[] { "and" }, r.Refused.ToArray());
    }
}

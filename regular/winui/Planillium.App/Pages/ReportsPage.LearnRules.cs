using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// G9b (2026-11-04): after a Bulk Mark of ticked rows the app proposes one combination rule per app
// ("Excel + quotation = on plan"): the user confirms, edits the words, or skips. Nothing is saved
// without the confirm. Saved rules are recorded with the mark's change, so its Undo removes them.
public sealed partial class ReportsPage
{
    private sealed class RuleOffer
    {
        public required RuleProposal Proposal { get; init; }
        public required CheckBox Include { get; init; }
        public required TextBox Words { get; init; }
    }

    private static string RangeText(RuleEvidence ev)
    {
        if (ev.Total == 0) return "no examples yet";
        var (lo, hi) = ev.Range;
        return $"{FormatCount(ev.Examples)} of {FormatCount(ev.Total)} matching rows agree " +
               $"(range {Math.Round(lo * 100).ToString("0", CultureInfo.CurrentCulture)}–" +
               $"{Math.Round(hi * 100).ToString("0", CultureInfo.CurrentCulture)}%)";
    }

    /// <summary>The proposals for a category mark of these rows, one per app, using the whole history
    /// (live Diary rows plus the permanent counts). Pure read; empty when nothing usable.</summary>
    private static (List<RuleProposal> Proposals, List<HistoryRow> History) BuildRuleProposals(
        string category, IReadOnlyList<ReportData.DiaryEntry> rows, string? tag = null)
    {
        using var db = new Database();
        // G9c: a tag rule is measured against the tag-aware history, and takes the category the
        // ticked rows of its app mostly have (so an on-plan paid app gives an on-plan paid rule).
        var history = tag == null ? LearnedRuleStore.LoadHistory(db) : LearnedRuleTagHistory.Load(db);
        var existing = LearnedRuleStore.LoadRules(db);
        var proposals = new List<RuleProposal>();
        var byApp = rows
            .Where(r => !string.Equals(r.Window, DiaryCategory.Idle, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => AppNames.Group(r.Window))
            .Where(g => g.Key is { Length: > 0 } && g.Key != "—");
        foreach (var g in byApp)
        {
            var marked = g.Select(r => (IReadOnlySet<string>)TitleWords.Normalize(r.Window, g.Key))
                .Where(w => w.Count > 0).ToList();
            var cat = tag == null
                ? category
                : g.Where(r => !string.IsNullOrWhiteSpace(r.Cat) && r.Cat != DiaryCategory.Idle)
                    .GroupBy(r => r.Cat).OrderByDescending(x => x.Count()).Select(x => x.Key).FirstOrDefault();
            if (cat == null) continue;
            var p = RuleProposer.Propose(g.Key, cat, marked, history, tag);
            if (p == null) continue;
            var key = LearnedRuleStore.JoinWords(p.Words);
            if (existing.Any(e => string.Equals(e.App, p.App, StringComparison.OrdinalIgnoreCase)
                                  && LearnedRuleStore.JoinWords(e.Words) == key && e.Category == p.Category
                                  && (e.Tag ?? "") == (p.Tag ?? ""))) continue;
            proposals.Add(p);
        }
        return (proposals, history);
    }

    /// <summary>Shows the proposals and saves the confirmed ones. Returns the saved rule ids (empty
    /// when skipped, nothing to propose, or a failure — which is logged and reported to the user).</summary>
    private async Task<List<long>> OfferLearnedRulesAsync(long changeId, string category, string categoryLabel,
        List<ReportData.DiaryEntry> rows, string? tag = null)
    {
        var saved = new List<long>();
        List<RuleProposal> proposals;
        List<HistoryRow> history;
        try
        {
            (proposals, history) = await Task.Run(() => BuildRuleProposals(category, rows, tag));
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.LearnRules.Propose", ex);
            return saved;
        }
        if (proposals.Count == 0) return saved;

        var offers = new List<RuleOffer>();
        var panel = new StackPanel { Spacing = 14, MinWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = "Teach Planillium a rule so this is marked automatically next time. A rule is an app plus " +
                   "words from the window title; it acts by itself only after at least 10 examples with a " +
                   "safe margin (85%), and until then it only suggests in the Diary.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        foreach (var p in proposals)
        {
            var pCategory = p.Category;
            var include = new CheckBox
            {
                IsChecked = true,
                Content = tag == null
                    ? $"{p.App}  →  {categoryLabel}"
                    : $"{p.App}  →  {categoryLabel} ({MarkValueLabel(DataChangeDoor.Field.DiaryCategory, pCategory)})",
            };
            AutomationProperties.SetName(include, $"Save rule for {p.App}");
            var words = new TextBox { Text = string.Join(" ", p.Words), Header = "Title words (all must appear)" };
            AutomationProperties.SetName(words, $"Words for {p.App}");
            var evidence = new TextBlock
            {
                Text = RangeText(p.Evidence), FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            AutomationProperties.SetName(evidence, $"Evidence for {p.App}");
            var app = p.App;
            words.TextChanged += (_, _) =>
            {
                var ws = words.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(TitleWords.NormalizeWord).Where(w => w.Length > 0).Distinct().ToList();
                evidence.Text = ws.Count == 0
                    ? "Type at least one word."
                    : RangeText(RuleMatcher.Evidence(new LearnedRule(0, app, ws, pCategory, tag), history));
            };
            var card = new StackPanel { Spacing = 6 };
            card.Children.Add(include);
            card.Children.Add(words);
            card.Children.Add(evidence);
            panel.Children.Add(card);
            offers.Add(new RuleOffer { Proposal = p, Include = include, Words = words });
        }

        var dialog = Dialogs.DialogControls.Build(XamlRoot,
            proposals.Count == 1 ? "Learn this rule?" : "Learn these rules?",
            new ScrollViewer { Content = panel, MaxHeight = 420 },
            primaryButtonText: "Save rules", closeButtonText: "Skip");
        if (await Dialogs.DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return saved;

        var chosen = offers.Where(o => o.Include.IsChecked == true)
            .Select(o => (o.Proposal.App, o.Proposal.Category, Words: o.Words.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .ToList();
        if (chosen.Count == 0) return saved;
        try
        {
            saved = await Task.Run(() =>
            {
                var ids = new List<long>();
                using var db = new Database();
                db.RunInTransaction(() =>
                {
                    foreach (var (app, ruleCategory, words) in chosen)
                    {
                        var id = LearnedRuleStore.AddRule(db, app, words, ruleCategory, tag);
                        if (id > 0) ids.Add(id);
                    }
                });
                DataChangeDoor.RecordRulesAdded(changeId, ids);
                return ids;
            });
            (App.MainWindow as MainWindow)?.ReloadTrackerRules();
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.LearnRules.Save", ex);
            ShowDiaryNotice(new(InfoBarSeverity.Error, "Couldn't save the rules",
                Log.Friendly("The marked rows are saved, the rules are not", ex), null));
        }
        return saved;
    }
}

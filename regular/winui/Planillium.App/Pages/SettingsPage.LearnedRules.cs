using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// G9b (2026-11-04): Settings ▸ Activity keywords ▸ Learned rules — a searchable list, delete only.
// Deleting leaves rows the rule already marked exactly as they are (E2) and is undoable from
// Change history (E3).
public sealed partial class SettingsPage
{
    private sealed record RuleLine(LearnedRule Rule, RuleEvidence Evidence, string Status, string Text);

    private List<RuleLine> _ruleLines = new();
    private bool _rulesBusy;

    private static string RuleStatusText(RuleEvidence ev) => ev.Confident ? "acts alone" : "suggests only";

    private static string RuleRange(RuleEvidence ev)
    {
        if (ev.Total == 0) return "no examples";
        var (lo, hi) = ev.Range;
        return $"{ev.Examples.ToString("N0", CultureInfo.CurrentCulture)} of {ev.Total.ToString("N0", CultureInfo.CurrentCulture)} agree, " +
               $"range {Math.Round(lo * 100).ToString("0", CultureInfo.CurrentCulture)}–" +
               $"{Math.Round(hi * 100).ToString("0", CultureInfo.CurrentCulture)}%";
    }

    /// <summary>Reads the rules and their evidence, then shows them filtered by the search box.</summary>
    private async void LoadLearnedRules()
    {
        try
        {
            var lines = await Task.Run(() =>
            {
                using var db = new Database();
                var book = RuleBook.Load(db);
                return book.Rules.Select(r =>
                {
                    var ev = book.EvidenceFor(r.Id);
                    var status = RuleStatusText(ev);
                    var words = string.Join(" ", r.Words);
                    return new RuleLine(r, ev, status,
                        $"{r.App} {words} {r.Category.Replace('_', ' ')} {r.Tag} {status}");
                }).OrderBy(l => l.Rule.App, StringComparer.OrdinalIgnoreCase).ThenBy(l => l.Rule.Id).ToList();
            });
            _ruleLines = lines;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.LearnedRules.Load", ex);
            LearnedRulesList.Children.Clear();
            LearnedRulesList.Children.Add(new TextBlock
            {
                Text = Log.Friendly("Couldn't read the learned rules", ex), TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        ShowLearnedRules();
    }

    private void LearnedRulesSearch_Changed(object sender, TextChangedEventArgs e) => ShowLearnedRules();

    private void ShowLearnedRules()
    {
        LearnedRulesList.Children.Clear();
        var q = (LearnedRulesSearch.Text ?? "").Trim();
        var shown = q.Length == 0
            ? _ruleLines
            : _ruleLines.Where(l => q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(t => l.Text.Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
        if (shown.Count == 0)
        {
            LearnedRulesList.Children.Add(new TextBlock
            {
                Text = _ruleLines.Count == 0
                    ? "No learned rules yet. Mark rows in the Diary with the ticks and Planillium will offer one."
                    : "No rule matches that search.",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            return;
        }
        foreach (var line in shown) LearnedRulesList.Children.Add(RuleRow(line));
    }

    private Border RuleRow(RuleLine line)
    {
        var r = line.Rule;
        var words = string.Join(" ", r.Words);
        var title = new TextBlock
        {
            Text = $"{r.App} + {words}  →  {r.Category.Replace('_', '-')}" + (r.Tag != null ? $" + {r.Tag}" : ""),
            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
        };
        var detail = new TextBlock
        {
            Text = $"{line.Status} · {RuleRange(line.Evidence)}",
            FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(title);
        text.Children.Add(detail);

        var delete = new Button { Content = "Delete", VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(delete, $"Delete rule: {r.App} + {words}");
        delete.Click += async (_, _) => await DeleteRuleAsync(line, delete);

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(delete, 1);
        grid.Children.Add(text);
        grid.Children.Add(delete);
        return new Border
        {
            Child = grid,
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        };
    }

    private async Task DeleteRuleAsync(RuleLine line, Button trigger)
    {
        if (_rulesBusy) return;
        var r = line.Rule;
        var words = string.Join(" ", r.Words);
        var confirm = Dialogs.DialogControls.Build(XamlRoot, "Delete this rule?",
            $"\"{r.App} + {words}\" → {r.Category.Replace('_', '-')}{(r.Tag != null ? " + " + r.Tag : "")}. Rows it already marked keep their values; " +
            "from now on the keyword lists decide for this app and these words. A backup is saved first, " +
            "and you can undo this from Change history.",
            primaryButtonText: "Delete", closeButtonText: "Cancel");
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        _rulesBusy = true;
        trigger.IsEnabled = false;
        try
        {
            var changeId = await Task.Run(() => DataChangeDoor.DeleteRule(r.Id));
            (App.MainWindow as MainWindow)?.ReloadTrackerRules();
            SaveStatus.Text = changeId > 0 ? "Rule deleted. Undo is in Change history." : "That rule was already gone.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.LearnedRules.Delete", ex);
            SaveStatus.Text = Log.Friendly("Couldn't delete that rule", ex);
        }
        finally
        {
            _rulesBusy = false;
            LoadLearnedRules();
        }
    }
}

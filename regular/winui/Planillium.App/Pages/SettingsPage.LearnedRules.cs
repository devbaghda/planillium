using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// G9b (2026-11-04): Settings ▸ Learned rules — a searchable list.
// 2026-11-11 (user): words are an OR list separated by commas; one line per app with a sub-row per
// category + tag; rules can be added by hand. Deleting leaves rows the rule already marked exactly as
// they are (E2) and every change is undoable from Change history (E3).
public sealed partial class SettingsPage
{
    /// <summary>One sub-row: every stored rule of an app with the same category + tag, shown as one rule
    /// whose words are the union of theirs. <see cref="Rule"/> is that merged view (Id of the first).</summary>
    private sealed record RuleLine(LearnedRule Rule, IReadOnlyList<long> Ids, RuleEvidence Evidence, string Status, string Text);

    private List<RuleLine> _ruleLines = new();
    private bool _rulesBusy;

    private static string RuleStatusText(RuleEvidence ev) => "acts at once";

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
                return book.Rules
                    .GroupBy(r => (App: r.App.ToLowerInvariant(), r.Category, Tag: (r.Tag ?? "").ToLowerInvariant(), AppOnly: r.Words.Count == 0))
                    .Select(g =>
                    {
                        var first = g.OrderBy(r => r.Id).First();
                        var words = g.SelectMany(r => r.Words).Distinct().OrderBy(w => w, StringComparer.Ordinal).ToList();
                        var merged = new LearnedRule(first.Id, first.App, words, first.Category, first.Tag);
                        var ev = book.EvidenceOf(merged);
                        var status = RuleStatusText(ev);
                        return new RuleLine(merged, g.Select(r => r.Id).ToList(), ev, status,
                            $"{first.App} {(words.Count == 0 ? "any window" : string.Join(" ", words))} {first.Category.Replace('_', ' ')} {first.Tag} {status}");
                    })
                    .OrderBy(l => l.Rule.App, StringComparer.OrdinalIgnoreCase).ThenBy(l => l.Rule.Category, StringComparer.Ordinal)
                    .ThenBy(l => l.Rule.Tag, StringComparer.OrdinalIgnoreCase).ToList();
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
        foreach (var app in shown.GroupBy(l => l.Rule.App, StringComparer.OrdinalIgnoreCase))
            LearnedRulesList.Children.Add(AppCard(app.Key, app.ToList()));
    }

    /// <summary>One card per app (user 2026-11-11: "one line for an app"), its rules listed inside.</summary>
    private Border AppCard(string app, List<RuleLine> lines)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = app, FontWeight = FontWeights.SemiBold, FontSize = 15 });
        foreach (var line in lines) stack.Children.Add(RuleRow(line));
        return new Border
        {
            Child = stack,
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        };
    }

    private Grid RuleRow(RuleLine line)
    {
        var r = line.Rule;
        var words = r.Words.Count == 0 ? "any window of this app" : string.Join(", ", r.Words);
        var title = new TextBlock
        {
            Text = $"{words}  →  {r.Category.Replace('_', '-')}" + (r.Tag != null ? $" + {r.Tag}" : ""),
            TextWrapping = TextWrapping.Wrap,
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

        var edit = new Button { Content = "Edit", VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(edit, $"Edit rule: {r.App} + {words}");
        edit.Click += async (_, _) => await EditRuleAsync(line, edit);

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(edit, 1);
        Grid.SetColumn(delete, 2);
        grid.Children.Add(text);
        grid.Children.Add(edit);
        grid.Children.Add(delete);
        return grid;
    }

    private Task EditRuleAsync(RuleLine line, Button trigger) =>
        RuleDialogAsync(line.Rule.App, line.Ids, string.Join(", ", line.Rule.Words), line.Rule.Category, line.Rule.Tag, trigger, isAdd: false);

    private async void AddRule_Click(object sender, RoutedEventArgs e)
    {
        try { await RuleDialogAsync(null, Array.Empty<long>(), "", DiaryCategory.OnPlan, null, AddRuleButton, isAdd: true); }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.LearnedRules.Add", ex);
            SaveStatus.Text = Log.Friendly("Couldn't add that rule", ex);
        }
    }

    /// <summary>Runs the current rules over the whole diary: shows how many rows would change, then applies
    /// on confirmation. Rows marked by hand, idle and day-off rows stay as they are. Backup first; Undo is
    /// in Change history.</summary>
    private async void ApplyRulesHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_rulesBusy) return;
        _rulesBusy = true;
        ApplyRulesHistoryButton.IsEnabled = false;
        try
        {
            var p = await Task.Run(DataChangeDoor.PreviewRulesOnHistory);
            string N(int n) => n.ToString("N0", CultureInfo.CurrentCulture);
            if (p.Changed == 0)
            {
                SaveStatus.Text = $"Nothing to change: the current rules already fit all {N(p.Rows)} diary rows they can reach.";
                return;
            }
            var split = string.Join(", ", p.ByCategory.Select(kv => $"{N(kv.Value)} → {kv.Key.Replace('_', '-')}"));
            var confirm = Dialogs.DialogControls.Build(XamlRoot, "Apply the rules to the history?",
                $"{N(p.Changed)} of {N(p.Rows)} diary rows would be set by a rule ({split}). " +
                $"Rows you marked by hand ({N(p.Skipped)} manual, idle or day-off rows in all) are left alone. " +
                "Day scores are recalculated. A backup is saved first, and you can undo this from Change history.",
                primaryButtonText: "Apply", closeButtonText: "Cancel");
            if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;
            var r = await Task.Run(DataChangeDoor.ApplyRulesToHistory);
            SaveStatus.Text = $"Applied the rules to {N(r.Changed)} diary rows. Undo is in Change history.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.LearnedRules.ApplyToHistory", ex);
            SaveStatus.Text = Log.Friendly("Couldn't apply the rules to the history", ex);
        }
        finally
        {
            _rulesBusy = false;
            ApplyRulesHistoryButton.IsEnabled = true;
        }
    }

    /// <summary>Apps already in a rule or seen in the Diary, for the Add rule picker.</summary>
    private static List<string> KnownApps()
    {
        var apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var db = new Database();
        foreach (var r in LearnedRuleStore.LoadRules(db)) apps.Add(r.App);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT window FROM time_diary WHERE window <> 'idle'";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var app = (rd.GetString(0) ?? "").Trim();
            if (app.Length > 0) apps.Add(app);
        }
        return apps.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Add (no ids) or edit (the ids of an app + category + tag group) a rule: words typed
    /// separated by commas, matched as an OR; category and tag. Goes through DataChangeDoor.SaveRule (backup,
    /// change history, Undo, junk words refused). A refused save shows its reason and reopens the dialog with
    /// what was typed.</summary>
    private async Task RuleDialogAsync(string? app, IReadOnlyList<long> ids, string wordsText, string category,
        string? tag, Button trigger, bool isAdd)
    {
        if (_rulesBusy) return;
        List<string> knownApps = isAdd ? await Task.Run(KnownApps) : new();
        string? error = null;
        while (true)
        {
            var wordsBox = new TextBox
            {
                Header = "Words from the window title, separated by commas (any one of them matches). Leave empty for every window of this app.",
                Text = wordsText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64,
            };
            var catBox = new ComboBox { Header = "Category", HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var (label, value) in DiaryCategory.EditableOptions)
                catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            catBox.SelectedIndex = Math.Max(0, Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == category));
            var tagBox = new ComboBox { Header = "Tag (optional)", HorizontalAlignment = HorizontalAlignment.Stretch };
            tagBox.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });
            foreach (var (label, value) in DiaryTag.Options)
                tagBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            var tagIdx = tag == null ? 0 : Array.FindIndex(DiaryTag.Options, t => string.Equals(t.Value, tag, StringComparison.OrdinalIgnoreCase)) + 1;
            tagBox.SelectedIndex = Math.Max(0, tagIdx);
            AutomationProperties.SetName(wordsBox, "Rule words");
            AutomationProperties.SetName(catBox, "Rule category");
            AutomationProperties.SetName(tagBox, "Rule tag");

            var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
            ComboBox? appBox = null;
            if (isAdd)
            {
                appBox = new ComboBox
                {
                    Header = "App (pick or type)", IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch,
                    ItemsSource = knownApps, Text = app ?? "",
                };
                AutomationProperties.SetName(appBox, "Rule app");
                panel.Children.Add(appBox);
            }
            else
                panel.Children.Add(new TextBlock { Text = $"App: {app}", FontWeight = FontWeights.SemiBold });
            panel.Children.Add(wordsBox);
            panel.Children.Add(catBox);
            panel.Children.Add(tagBox);
            if (error != null)
                panel.Children.Add(new TextBlock
                {
                    Text = error, TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                });
            var dialog = Dialogs.DialogControls.Build(XamlRoot, isAdd ? "Add rule" : "Edit rule", "", primaryButtonText: "Save", closeButtonText: "Cancel");
            dialog.Content = panel;
            if (await Dialogs.DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return;

            wordsText = wordsBox.Text ?? "";
            category = (catBox.SelectedItem as ComboBoxItem)?.Tag as string ?? category;
            tag = (tagBox.SelectedItem as ComboBoxItem)?.Tag as string;
            if (appBox != null) app = (appBox.Text ?? "").Trim();
            _rulesBusy = true;
            trigger.IsEnabled = false;
            try
            {
                var words = wordsText.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var changeId = await Task.Run(() => DataChangeDoor.SaveRule(app, ids, words, category, tag));
                (App.MainWindow as MainWindow)?.ReloadTrackerRules();
                SaveStatus.Text = changeId > 0 ? (isAdd ? "Rule added." : "Rule changed.") + " Undo is in Change history."
                                               : "That rule was already gone.";
                return;
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;       // refused: show why, keep what was typed
            }
            catch (Exception ex)
            {
                Log.Error("SettingsPage.LearnedRules.Save", ex);
                SaveStatus.Text = Log.Friendly("Couldn't save that rule", ex);
                return;
            }
            finally
            {
                _rulesBusy = false;
                trigger.IsEnabled = true;
                LoadLearnedRules();
            }
        }
    }

    private async Task DeleteRuleAsync(RuleLine line, Button trigger)
    {
        if (_rulesBusy) return;
        var r = line.Rule;
        var words = r.Words.Count == 0 ? "any window" : string.Join(", ", r.Words);
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
            var changeId = await Task.Run(() => DataChangeDoor.DeleteRules(line.Ids));
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

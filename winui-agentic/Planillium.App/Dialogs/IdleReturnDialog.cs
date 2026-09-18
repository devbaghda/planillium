using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// "Welcome back" — the softened replacement for the Python app's blocking
/// idle interrogation: one-tap chips (fixed set + your most frequent past
/// answers) for the common single-activity case, or "Split into activities"
/// for the "it was actually several things" case — the WinUI port of the
/// Python dialog's multi-row editor, minus the modal blocking. Logs through
/// ActivityTracker.LogIdleAnswer (once per segment when split), so the
/// idle-answer library still reclassifies matched text per segment.
///
/// Category and Tag are explicit fields (2026-08-07 request: "it is not giving me the field
/// to fill in also the category and the tag"), in both single mode and each split row —
/// previously Category was silently inferred from the typed text with no way to see or
/// override the guess, and Tag didn't exist here at all. A chip click now fills the
/// description box and re-runs the same guess instead of instant-submitting (it used to close
/// the dialog on the spot, which left no room to look at or change the fields this added) —
/// one extra tap for the common case, but the only way the new fields mean anything on the
/// chip path too, not just the typed-text one.
/// </summary>
public static class IdleReturnDialog
{
    private static readonly string[] FixedChips =
        { "Lunch", "Break", "Errand", "Work off-screen" };

    private const int ChipsPerRow = 4;

    // Same bug class already fixed twice elsewhere (SplitDiaryEntryDialog, AddPlanDialog):
    // ContentDialog's own template caps its rendered width at the ContentDialogMaxWidth theme
    // resource (default 548) regardless of what width the content asks for — see either of
    // those two for the full generic.xaml story. This dialog's split-mode row (duration +
    // category + tag + description + remove) measures well past that cap, which zero-arranges
    // the trailing columns instead of visibly clipping (2026-08-17 user report: "field is out
    // of boundaries"). Overriding ContentDialogMaxWidth is the actual fix; rowsScroller's own
    // horizontal scrollbar stays as the fallback for a narrower app window.
    private const double DialogWidth = 780;
    private const double DialogContentWidth = DialogWidth - 64; // 48px template padding + margin, same rule as the other two

    /// <summary>
    /// Poll-loop entry point: shows the interactive dialog directly when the
    /// window is actually on screen, otherwise raises a "welcome back" toast
    /// so the prompt still reaches the user on PC activation after an
    /// absence even while the app sits in the tray — this fires from
    /// ActivityTracker's background poll, so most of the time nobody is
    /// looking at the (hidden) main window. If the toast is never clicked or
    /// answered, nothing is lost: HandleIdleReturn already logged the gap as
    /// "unaccounted time" the moment it was detected (2026-07-27 — see its own
    /// comment), and answering here just replaces that placeholder with the
    /// real description instead of leaving the stretch unlabelled.
    /// </summary>
    public static Task Trigger(MainWindow window, int idleMinutes, DateTime idleStart)
    {
        // No once-per-day throttle here, unlike Kickoff/Review — a real
        // idle-return can legitimately happen several times in one day
        // (lunch, an errand, an afternoon break), and each is a distinct
        // event worth its own prompt. The Tag below still stops them from
        // stacking up in Action Center: a second toast before the first is
        // acted on replaces it rather than piling beside it, but it never
        // suppresses a genuinely new return.
        return PromptRouter.ShowOrToast(window, () => ShowAsync(window, idleMinutes, idleStart),
            () => false, () => { },
            "Welcome back.", "Quick check-in — click to log where you were.",
            ToastArgs.IdleReturn,
            (ToastArgs.Action, ToastArgs.IdleReturn),
            (ToastArgs.Mins, idleMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            (ToastArgs.Start, idleStart.ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <param name="leadIn">Optional context line shown above the usual
    /// question — used when this dialog is standing in for something other
    /// than a plain return-from-idle (e.g. the evening review's gap sweep),
    /// so it doesn't read as an unrelated, unexplained interruption.</param>
    public static async Task ShowAsync(MainWindow window, int idleMinutes, DateTime idleStart,
        string? leadIn = null)
    {
        if (window.Tracker is not { } tracker) return;

        List<string> frequent;
        try
        {
            using var db = new Database();
            frequent = db.MostFrequentIdleAnswers();
        }
        catch (Exception ex)
        {
            Log.Error("IdleReturnDialog.MostFrequentIdleAnswers", ex);
            frequent = new List<string>();
        }
        var chips = FixedChips
            .Concat(frequent.Where(f => !FixedChips.Contains(f, StringComparer.OrdinalIgnoreCase)))
            .Take(8)
            .ToArray();

        var root = new StackPanel { Spacing = 12, MinWidth = DialogContentWidth };
        if (leadIn is { Length: > 0 })
            root.Children.Add(new TextBlock
            {
                Text = leadIn,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        var idleEnd = idleStart.AddMinutes(idleMinutes);
        root.Children.Add(new TextBlock
        {
            Text = $"You were away {idleMinutes} min ({idleStart.ToIsoTimeOfDay()}–{idleEnd.ToIsoTimeOfDay()}). What was it, roughly?",
            TextWrapping = TextWrapping.Wrap,
        });

        // ── single-answer mode (default): chips + free text + category/tag ─
        var singleRoot = new StackPanel { Spacing = 12 };
        var input = new TextBox
        {
            PlaceholderText = "…or type it (matches your idle-answer library)",
        };
        AutomationProperties.SetName(input, "What you were doing");

        ContentDialog dialog = null!;

        var chipRows = new StackPanel { Spacing = 6 };
        for (var i = 0; i < chips.Length; i += ChipsPerRow)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var chip in chips.Skip(i).Take(ChipsPerRow))
            {
                var b = new Button { Content = chip };
                // Fills the field rather than submitting on the spot (was an instant
                // dialog.Hide() before 2026-08-07) — a one-tap submit left no chance to look
                // at or adjust the Category/Tag fields below, which would make them dead
                // weight on the chip path. Setting Text here re-runs the same classifier
                // guess via input.TextChanged below, same as typing it by hand.
                b.Click += (_, _) => input.Text = chip;
                row.Children.Add(b);
            }
            chipRows.Children.Add(row);
        }
        singleRoot.Children.Add(chipRows);
        singleRoot.Children.Add(input);

        var catBox = new ComboBox { Header = "Category", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (label, value) in DiaryCategory.EditableOptions)
            catBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        catBox.SelectedIndex = Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == DiaryCategory.Idle);

        // A second, independent axis (2026-08-06 elsewhere in this app) — "(none)" first
        // since most idle answers won't carry one, same convention EditDiaryEntryDialog uses.
        var tagBox = new ComboBox { Header = "Tag (optional)", HorizontalAlignment = HorizontalAlignment.Stretch };
        tagBox.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });
        foreach (var (label, value) in DiaryTag.Options)
            tagBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        tagBox.SelectedIndex = 0;

        var catTagRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        catTagRow.Children.Add(catBox);
        catTagRow.Children.Add(tagBox);
        singleRoot.Children.Add(catTagRow);

        // Category starts as ClassifyIdleText's guess and keeps re-guessing as the text
        // changes — until the user picks something themselves, at which point it stops
        // overriding their choice. programmaticCatChange distinguishes "we just set this from
        // the guess" from "the user just picked something" so the first doesn't get mistaken
        // for the second and immediately disable the guessing it was performing.
        var catTouched = false;
        var programmaticCatChange = false;
        void AutoClassifyCat(string text)
        {
            if (catTouched) return;
            var guess = tracker.ClassifyIdleText(text);
            var idx = Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == guess);
            if (idx < 0) return;
            programmaticCatChange = true;
            catBox.SelectedIndex = idx;
            programmaticCatChange = false;
        }
        catBox.SelectionChanged += (_, _) => { if (!programmaticCatChange) catTouched = true; };
        input.TextChanged += (_, _) => AutoClassifyCat(input.Text);

        var splitLink = new HyperlinkButton
        {
            Content = "It was actually several things — split it",
            Padding = new Thickness(0),
        };
        singleRoot.Children.Add(splitLink);
        root.Children.Add(singleRoot);

        // ── split mode: hidden until "split it" is clicked ───────────────
        var splitRoot = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
        root.Children.Add(splitRoot);

        // Horizontally scrollable, not just a plain panel — adding Category+Tag columns
        // (2026-08-07) means a row with duration+category+tag+description+remove can exceed
        // ContentDialog's own width cap, which zero-arranges trailing Auto columns instead of
        // visibly clipping (the exact bug already root-caused for ReportsPage.Diary.cs's row
        // list and SplitDiaryEntryDialog's own rows — see either's comments for the full
        // story). Applying that same proven fix here pre-emptively.
        var rowsPanel = new StackPanel { Spacing = 6 };
        var rowsScroller = new ScrollViewer
        {
            // Visible, not Auto — this project already learned that lesson once
            // (ReportsPage.Diary.cs's diaryScroller, 2026-07-28: an Auto scrollbar's
            // hover-only indicator went unnoticed and read as broken/missing content).
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rowsPanel,
        };
        splitRoot.Children.Add(rowsScroller);

        var addRowBtn = new Button { Content = "+ Add activity", Padding = new Thickness(0) };
        var remainingText = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(addRowBtn);
        toolbar.Children.Add(remainingText);
        splitRoot.Children.Add(toolbar);

        dialog = DialogControls.Build(window.Content.XamlRoot, "Welcome back", root,
            primaryButtonText: "Log it", closeButtonText: "Skip", defaultButton: ContentDialogButton.Primary);
        // Instance-level resource shadows the app-wide ContentDialogMaxWidth theme resource the
        // default ContentDialog style reads from — see DialogWidth's comment above for why, and
        // why root is sized to DialogContentWidth (smaller than this) rather than to this value.
        dialog.Resources["ContentDialogMaxWidth"] = DialogWidth;

        var rows = new List<(NumberBox Dur, ComboBox Cat, ComboBox Tag, AutoSuggestBox Desc, Button Remove)>();

        // Split mode had no one-click answers at all, unlike single mode's chips above —
        // reuses that same fixed+frequent `chips` list rather than a second, different set, so
        // both modes offer the same one-tap answers (2026-08-17 user report: "no frequent
        // answers one-click option"). Fills whichever row's description was last focused, since
        // — unlike single mode's one input — several rows can be mid-edit at once here (same
        // convention SplitDiaryEntryDialog's own quick-pick chips use).
        AutoSuggestBox? activeDescBox = null;
        if (chips.Length > 0)
        {
            var chipSection = new StackPanel { Spacing = 6 };
            chipSection.Children.Add(new TextBlock
            {
                Text = "Quick pick:",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            });
            for (var i = 0; i < chips.Length; i += ChipsPerRow)
            {
                var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                foreach (var chip in chips.Skip(i).Take(ChipsPerRow))
                {
                    var b = new Button { Content = chip, FontSize = 12, Padding = new Thickness(8, 3, 8, 3) };
                    b.Click += (_, _) =>
                    {
                        var target = activeDescBox ?? rows.FirstOrDefault(r => r.Desc.Text.Length == 0).Desc;
                        if (target != null) target.Text = chip;
                    };
                    chipRow.Children.Add(b);
                }
                chipSection.Children.Add(chipRow);
            }
            // Above rowsScroller, not appended after the toolbar — buried below the whole row
            // list would defeat the point of being quick (same placement SplitDiaryEntryDialog
            // uses for its own chip section).
            splitRoot.Children.Insert(0, chipSection);
        }

        void UpdateSplitState()
        {
            var used = rows.Sum(r => double.IsNaN(r.Dur.Value) ? 0 : (int)r.Dur.Value);
            var remaining = idleMinutes - used;
            var allDurOk = rows.All(r => !double.IsNaN(r.Dur.Value) && r.Dur.Value > 0);
            var allDescOk = rows.All(r => r.Desc.Text.Trim().Length > 0);

            remainingText.Text = remaining < 0
                ? $"{-remaining} min over — reduce a duration"
                : remaining > 0
                    ? $"{remaining} min unaccounted — OK to save"
                    : "All time accounted";

            foreach (var r in rows) r.Remove.IsEnabled = rows.Count > 1;

            var canSubmit = remaining >= 0 && allDurOk && allDescOk;
            dialog.IsPrimaryButtonEnabled = canSubmit;
            // DefaultButton follows the enabled state once split rows exist, not fixed at
            // Primary — WinUI's DefaultButton can still fire on Enter even while the button it
            // names is disabled (confirmed live 2026-08-07, same trap in ReviewDialog: an
            // Enter press froze a day's score through a disabled "Close the day" button). Here
            // that would mean pressing Enter mid-split, with an over-budget or blank-description
            // row, could still commit an invalid split.
            dialog.DefaultButton = canSubmit ? ContentDialogButton.Primary : ContentDialogButton.Close;
        }

        // Each row picks its own category/tag rather than re-guessing per keystroke — same
        // choice SplitDiaryEntryDialog already made for its own rows ("a recorded block isn't
        // auto-classified from text"); once split, a row is its own activity, not a live guess.
        // prefillCat/prefillTag seed a new row from whatever the single-mode fields currently
        // hold, so switching to split mode after already narrowing those down doesn't discard it.
        void AddRow(int? prefillMin, string prefillCat, string? prefillTag)
        {
            var durBox = DialogControls.MinutesBox(prefillMin);

            var rowCatBox = new ComboBox { Width = 110 };
            foreach (var (label, value) in DiaryCategory.EditableOptions)
                rowCatBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            rowCatBox.SelectedIndex = Array.FindIndex(DiaryCategory.EditableOptions, c => c.Value == prefillCat) is >= 0 and var ci ? ci : 0;
            AutomationProperties.SetName(rowCatBox, "Category");

            var rowTagBox = new ComboBox { Width = 130 };
            rowTagBox.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });
            foreach (var (label, value) in DiaryTag.Options)
                rowTagBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
            rowTagBox.SelectedIndex = prefillTag is null ? 0
                : Array.FindIndex(DiaryTag.Options, t => t.Value == prefillTag) is >= 0 and var ti ? ti + 1 : 0;
            AutomationProperties.SetName(rowTagBox, "Tag (optional)");

            // Fixed width, not Stretch — inside rowsScroller's horizontal ScrollViewer this
            // Grid is offered effectively unconstrained width, and a Star/Stretch column has
            // nothing finite to size against there, collapsing toward zero instead of staying
            // usable (same trap documented on SplitDiaryEntryDialog's own descBox).
            var descBox = new AutoSuggestBox
            {
                PlaceholderText = "e.g. lunch, walked the dog…",
                Width = 220,
            };
            DialogControls.WireFrequentSuggestions(descBox, frequent);
            AutomationProperties.SetName(descBox, "Activity description");
            // Tracks which row a quick-pick chip should fill — see activeDescBox's declaration
            // above the chip section.
            descBox.GotFocus += (_, _) => activeDescBox = descBox;
            var removeBtn = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4) };
            AutomationProperties.SetName(removeBtn, "Remove this activity");

            var row = new Grid { ColumnSpacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(durBox, 0); Grid.SetColumn(rowCatBox, 1); Grid.SetColumn(rowTagBox, 2);
            Grid.SetColumn(descBox, 3); Grid.SetColumn(removeBtn, 4);
            row.Children.Add(durBox);
            row.Children.Add(rowCatBox);
            row.Children.Add(rowTagBox);
            row.Children.Add(descBox);
            row.Children.Add(removeBtn);
            rowsPanel.Children.Add(row);

            var entry = (durBox, rowCatBox, rowTagBox, descBox, removeBtn);
            rows.Add(entry);

            durBox.ValueChanged += (_, _) => UpdateSplitState();
            descBox.TextChanged += (_, _) => UpdateSplitState();
            removeBtn.Click += (_, _) =>
            {
                rowsPanel.Children.Remove(row);
                rows.Remove(entry);
                UpdateSplitState();
            };
            UpdateSplitState();
        }

        string CurrentCategory() => ((ComboBoxItem)catBox.SelectedItem).Tag as string ?? DiaryCategory.Idle;
        string? CurrentTag() => ((ComboBoxItem)tagBox.SelectedItem).Tag as string;

        var splitMode = false;
        splitLink.Click += (_, _) =>
        {
            splitMode = true;
            singleRoot.Visibility = Visibility.Collapsed;
            splitRoot.Visibility = Visibility.Visible;
            if (rows.Count == 0) AddRow(idleMinutes, CurrentCategory(), CurrentTag());
            UpdateSplitState();
        };
        addRowBtn.Click += (_, _) => AddRow(null, CurrentCategory(), CurrentTag());

        var result = await DialogGate.ShowAsync(dialog);

        try
        {
            if (result != ContentDialogResult.Primary)
            {
                tracker.LogIdleAnswer(idleStart, idleMinutes, DiaryCategory.IdlePlaceholder, DiaryCategory.Idle);
                return;
            }

            if (splitMode)
            {
                // Re-validate before writing, independent of the dialog's last-known enabled
                // state — never trust IsPrimaryButtonEnabled alone for a write this
                // consequential (same reasoning as ReviewDialog's/SpendDialog's/
                // SplitDiaryEntryDialog's own post-await re-checks). An over-budget or
                // blank-description split falls back to the same placeholder path as Skip,
                // rather than silently committing incomplete rows.
                var used = rows.Sum(r => double.IsNaN(r.Dur.Value) ? 0 : (int)r.Dur.Value);
                var valid = used <= idleMinutes && rows.All(r => !double.IsNaN(r.Dur.Value) && r.Dur.Value > 0 &&
                    r.Desc.Text.Trim().Length > 0);
                if (!valid)
                {
                    tracker.LogIdleAnswer(idleStart, idleMinutes, DiaryCategory.IdlePlaceholder, DiaryCategory.Idle);
                    return;
                }
                tracker.LogIdleAnswers(BuildSegments(idleStart, rows));
                return;
            }

            var text = input.Text.Trim();
            tracker.LogIdleAnswer(idleStart, idleMinutes, text.Length > 0 ? text : DiaryCategory.IdlePlaceholder,
                CurrentCategory(), CurrentTag());
        }
        catch (Exception ex)
        {
            // No page is guaranteed to be on screen here (this runs from a
            // background poll's idle-return event) — toast is the same
            // fallback the timed prompts already use to reach the user
            // outside of an open dialog (2026-07-14 round-6 audit finding #5).
            Log.Error("IdleReturnDialog.LogIdleAnswer(s)", ex);
            Services.ToastNotifier.Show("Couldn't save that",
                "The idle-time answer didn't save — the database was likely briefly busy.", tag: null);
        }
    }

    /// <summary>Turns the split-mode rows into back-to-back diary segments starting at
    /// idleStart — the one piece of ShowAsync that's pure data transformation rather
    /// than UI wiring or closure-shared dialog state, pulled out on its own so it can be
    /// read (and eventually tested) independently of the dialog around it (audit finding
    /// #5). The rest of ShowAsync stays one method deliberately — its closures (rows,
    /// dialog, splitMode) share too much mutable state specific to this one widget's two UI
    /// modes to split further without adding more complexity than it removes, the same
    /// reasoning ReportsPage.Diary.BuildDiarySection's own doc comment already gives for
    /// staying unsplit.</summary>
    private static List<(DateTime Start, int Minutes, string Description, string Category, string? Tag)> BuildSegments(
        DateTime idleStart, List<(NumberBox Dur, ComboBox Cat, ComboBox Tag, AutoSuggestBox Desc, Button Remove)> rows)
    {
        var t = idleStart;
        var segments = new List<(DateTime, int, string, string, string?)>();
        foreach (var (dur, cat, tag, desc, _) in rows)
        {
            var mins = (int)dur.Value;
            var catValue = ((ComboBoxItem)cat.SelectedItem).Tag as string ?? DiaryCategory.Idle;
            var tagValue = ((ComboBoxItem)tag.SelectedItem).Tag as string;
            segments.Add((t, mins, desc.Text.Trim(), catValue, tagValue));
            t = t.AddMinutes(mins);
        }
        return segments;
    }
}

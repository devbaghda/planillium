using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;
using Windows.ApplicationModel.DataTransfer;

using Microsoft.UI.Xaml.Automation;
namespace MentorOverseer.App.Dialogs;

/// <summary>
/// "Add plan" wizard — same flow as the Python app: pick a mode, fill the
/// fields, copy the generated prompt into claude.ai, paste the JSON reply
/// back, import. No API key needed.
/// </summary>
public static class AddPlanDialog
{
    private const int MaxPlans = 2;

    private record Mode(string Label, string Template, string Field1Label,
        string Field1Hint, string Field2Hint, string Field3Hint, bool Reformat);

    private static readonly Mode[] Modes =
    {
        new("🎯 Learn a skill", PlanTemplates.Skill, "Skill you want to learn",
            "e.g. 'Power BI DAX', 'public speaking', 'Spanish conversation'",
            "e.g. 'senior data analyst', 'professional speech coach', 'native Spanish tutor'",
            "e.g. 'business intelligence', 'executive communication', 'language teaching'", false),
        new("📌 Achieve a goal", PlanTemplates.Goal, "Goal you want to achieve",
            "e.g. 'move to the Netherlands', 'buy a reliable car', 'become more productive'",
            "e.g. 'relocation consultant', 'car-buying advisor', 'productivity coach'",
            "e.g. 'EU relocation & visas', 'used-car markets', 'personal productivity systems'", false),
        new("📝 Format my own plan", PlanTemplates.Reformat, "Title for this plan",
            "e.g. 'Q3 fitness block', 'Dutch A2 in 90 days'", "", "", true),
    };

    public static async Task<bool> ShowAsync(Page host)
    {
        if (PlanStore.LoadActivePlans().Count >= MaxPlans)
        {
            await Message(host, $"Maximum {MaxPlans} active plans",
                "Archive a completed plan first — Plans page → Archive.");
            return false;
        }

        var panel = new StackPanel { Spacing = 10, MinWidth = 520 };

        var modeBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var m in Modes) modeBox.Items.Add(m.Label);
        modeBox.SelectedIndex = 0;
        AutomationProperties.SetName(modeBox, "Mode");

        var field1Label = new TextBlock { Text = Modes[0].Field1Label, FontSize = 12 };
        var subject = new TextBox { PlaceholderText = Modes[0].Field1Hint };
        // field1Label's text changes with the selected mode (below), so this
        // is a live label association rather than a static Header — a
        // screen reader announces whatever field1Label currently says
        // (2026-07-09 audit finding #15: this box previously had no
        // accessible name at all, only an adjacent, unassociated TextBlock).
        AutomationProperties.SetLabeledBy(subject, field1Label);
        var role = new TextBox { PlaceholderText = Modes[0].Field2Hint, Header = "Claude's role" };
        var area = new TextBox { PlaceholderText = Modes[0].Field3Hint, Header = "Area of expertise" };
        var ownPlan = new TextBox
        {
            AcceptsReturn = true, Height = 120, TextWrapping = TextWrapping.Wrap,
            Header = "Your own plan text",
            PlaceholderText = "Paste your own plan text here — or load it from a file below…",
            Visibility = Visibility.Collapsed,
        };
        var loadBtn = new Button
        {
            Content = "Load from file (.docx / .txt / .md / .json)…",
            Visibility = Visibility.Collapsed,
        };

        var prompt = new TextBox
        {
            AcceptsReturn = true, IsReadOnly = true, Height = 110,
            TextWrapping = TextWrapping.Wrap,
            Header = "Generated prompt",
            PlaceholderText = "Generated prompt appears here — copy it into claude.ai",
        };
        var genRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var genBtn = new Button { Content = "Generate prompt" };
        var copyBtn = new Button { Content = "Copy", IsEnabled = false };
        genRow.Children.Add(genBtn);
        genRow.Children.Add(copyBtn);

        var reply = new TextBox
        {
            AcceptsReturn = true, Height = 110, TextWrapping = TextWrapping.Wrap,
            Header = "Claude's reply",
            PlaceholderText = "…then paste Claude's whole reply (with the ```json block) here",
        };
        var error = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed,
        };

        modeBox.SelectionChanged += (_, _) =>
        {
            var m = Modes[modeBox.SelectedIndex];
            field1Label.Text = m.Field1Label;
            subject.PlaceholderText = m.Field1Hint;
            role.Visibility = m.Reformat ? Visibility.Collapsed : Visibility.Visible;
            area.Visibility = m.Reformat ? Visibility.Collapsed : Visibility.Visible;
            ownPlan.Visibility = m.Reformat ? Visibility.Visible : Visibility.Collapsed;
            loadBtn.Visibility = m.Reformat ? Visibility.Visible : Visibility.Collapsed;
        };

        loadBtn.Click += async (_, _) => await LoadFromFileAsync(reply, ownPlan, error);

        genBtn.Click += (_, _) =>
        {
            var m = Modes[modeBox.SelectedIndex];
            if (subject.Text.Trim().Length == 0) { Show(error, $"{m.Field1Label} is required."); return; }
            // Reformat mode's own plan-text box was never checked before being substituted
            // into the template — an empty box silently produced a prompt with a blank
            // {user_plan} slot and no error (audit finding #18).
            if (m.Reformat && ownPlan.Text.Trim().Length == 0)
            {
                Show(error, "Paste or load your plan text first.");
                return;
            }
            var text = m.Template.Replace("{subject}", subject.Text.Trim());
            text = m.Reformat
                ? text.Replace("{user_plan}", ownPlan.Text.Trim())
                : text.Replace("{claude_role}", role.Text.Trim().Length > 0 ? role.Text.Trim() : "seasoned mentor")
                      .Replace("{area_of_interest}", area.Text.Trim().Length > 0 ? area.Text.Trim() : subject.Text.Trim());
            prompt.Text = text;
            copyBtn.IsEnabled = true;
            // A freshly generated prompt is a new, uncopied prompt — reset the confirmation
            // label so it doesn't keep reading "Copied ✓" for content that was never
            // actually copied (audit finding #19).
            copyBtn.Content = "Copy";
            error.Visibility = Visibility.Collapsed;
        };

        copyBtn.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(prompt.Text);
            Clipboard.SetContent(dp);
            copyBtn.Content = "Copied ✓";
        };

        panel.Children.Add(modeBox);
        panel.Children.Add(field1Label);
        panel.Children.Add(subject);
        panel.Children.Add(role);
        panel.Children.Add(area);
        panel.Children.Add(ownPlan);
        panel.Children.Add(loadBtn);
        panel.Children.Add(genRow);
        panel.Children.Add(prompt);
        panel.Children.Add(reply);
        panel.Children.Add(error);

        var dialog = new ContentDialog
        {
            Title = "Add Plan",
            Content = new ScrollViewer { Content = panel, MaxHeight = 560 },
            PrimaryButtonText = "Import plan",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = host.XamlRoot,
        };

        // Keep the dialog open on failed import: cancel the close, show the error.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var problem = TryImport(reply.Text);
            if (problem != null)
            {
                Show(error, problem);
                args.Cancel = true;
            }
        };

        return await DialogGate.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>The "Load from file" button's handler, pulled out of ShowAsync's field
    /// wiring so it reads as its own step rather than one more closure sharing that
    /// method's local state (round-5 audit finding #29). Picks a file, routes .json
    /// straight to the reply box (no Claude round-trip needed) and everything else into
    /// the "own plan text" box, via ExtractDocxText for .docx specifically.</summary>
    private static async Task LoadFromFileAsync(TextBox reply, TextBox ownPlan, TextBlock error)
    {
        // Hoisted above the try so the catch block can still name which file
        // failed without holding a reference to file.Path itself (see the
        // catch block's own comment).
        string? pickedFileName = null;
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            foreach (var ext in new[] { ".docx", ".txt", ".md", ".json" })
                picker.FileTypeFilter.Add(ext);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            pickedFileName = Path.GetFileName(file.Path);

            var ext2 = Path.GetExtension(file.Path).ToLowerInvariant();
            if (ext2 == ".json")
            {
                // Already-structured plan: goes straight into the import box.
                reply.Text = await File.ReadAllTextAsync(file.Path);
                Show(error, "JSON loaded — press 'Import plan' to add it directly " +
                            "(no Claude round-trip needed).");
                return;
            }
            ownPlan.Text = ext2 == ".docx"
                ? ExtractDocxText(file.Path)
                : await File.ReadAllTextAsync(file.Path);
            error.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            // Not Log.Error(ctx, ex) — a file-not-found/IO exception's own
            // Message bakes in the full picked path, which can carry a
            // personally-named plan file plus the Windows username the path
            // starts with. The error TextBlock (visible only to the user, on
            // their own screen) still shows the full message; only what
            // lands in the log file — which has no retention/redaction of
            // its own — is narrowed (2026-07-14 round-6 audit finding #22).
            Log.Warn("AddPlanDialog.LoadFile",
                $"{ex.GetType().Name} reading '{pickedFileName ?? "(no file picked)"}'");
            Show(error, "Couldn't read that file: " + ex.Message);
        }
    }

    /// <summary>Plain text from a .docx (zip of XML) — port of main.py's
    /// _extract_docx_text: paragraph tags become newlines, all other tags drop.</summary>
    private static string ExtractDocxText(string path)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        var entry = zip.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("Not a .docx file (no word/document.xml inside).");
        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd();
        xml = xml.Replace("</w:p>", "\n");
        var text = Regex.Replace(xml, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
        return string.Join("\n", lines).Trim();
    }

    private static void Show(TextBlock error, string message)
    {
        error.Text = message;
        error.Visibility = Visibility.Visible;
    }

    /// <summary>Returns an error message, or null on success (plan file written).</summary>
    private static string? TryImport(string replyText)
    {
        if (replyText.Trim().Length == 0)
            return "Paste Claude's reply first.";

        var raw = replyText.Trim();
        var fence = Regex.Match(raw, "```(?:json)?\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline);
        var json = fence.Success ? fence.Groups[1].Value
                 : raw.StartsWith('{') ? raw : null;
        if (json == null)
            return "Couldn't find valid JSON — paste the whole reply including the ```json block.";

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException e) { return "That JSON doesn't parse: " + e.Message; }

        using (doc)
        {
            var root = doc.RootElement;
            foreach (var field in new[] { "id", "name", "phases" })
                if (!root.TryGetProperty(field, out _))
                    return $"Plan is missing the required field '{field}'.";

            var id = root.GetProperty("id").GetString() ?? "";
            // The id becomes a filename — never let pasted JSON smuggle path
            // separators or reserved characters into plans/active. Routed
            // through PlanStore.IsValidPlanId (not a separate copy of the
            // pattern) so import can never accept an id — e.g. one with an
            // underscore — that PlanStore.PlanFilePath would later reject;
            // that exact mismatch used to let an underscore-bearing id
            // import cleanly and then crash the first time it was used
            // (2026-07-14 round-6 audit finding #1).
            if (!PlanStore.IsValidPlanId(id))
                return "Plan 'id' must be a short kebab-case slug (lowercase a-z, 0-9, '-').";
            if (PlanStore.LoadActivePlans().Any(p => p.Id == id))
                return $"A plan with id '{id}' is already active.";

            // Fill defaults the Python importer would have asked for.
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            var output = new Dictionary<string, object?>();
            foreach (var (k, v) in dict) output[k] = v;
            if (!dict.ContainsKey("start_date"))
                output["start_date"] = DateTime.Today.ToIsoDate();
            if (!dict.ContainsKey("color"))
                output["color"] = "#bf5af2";

            // Uncaught here, this would throw straight out of the dialog's
            // synchronous PrimaryButtonClick handler instead of showing
            // inline like every other problem in this method does
            // (2026-07-14 round-6 audit finding #8).
            try
            {
                Directory.CreateDirectory(AppPaths.ActivePlansDir);
                JsonFileIO.WriteAllTextAtomic(
                    Path.Combine(AppPaths.ActivePlansDir, $"{id}.json"),
                    JsonSerializer.Serialize(output, JsonFileIO.Indented));
            }
            catch (Exception ex)
            {
                Log.Error("AddPlanDialog.TryImport", ex);
                return "Couldn't write the plan file — check the log for details and try again.";
            }
            return null;
        }
    }

    private static async Task Message(Page host, string title, string body)
    {
        var msg = new ContentDialog
        {
            Title = title,
            Content = body,
            CloseButtonText = "OK",
            XamlRoot = host.XamlRoot,
        };
        await DialogGate.ShowAsync(msg);
    }
}
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;
using Windows.ApplicationModel.DataTransfer;

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

        var field1Label = new TextBlock { Text = Modes[0].Field1Label, FontSize = 12 };
        var subject = new TextBox { PlaceholderText = Modes[0].Field1Hint };
        var role = new TextBox { PlaceholderText = Modes[0].Field2Hint, Header = "Claude's role" };
        var area = new TextBox { PlaceholderText = Modes[0].Field3Hint, Header = "Area of expertise" };
        var ownPlan = new TextBox
        {
            AcceptsReturn = true, Height = 120, TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Paste your own plan text here…", Visibility = Visibility.Collapsed,
        };

        var prompt = new TextBox
        {
            AcceptsReturn = true, IsReadOnly = true, Height = 110,
            TextWrapping = TextWrapping.Wrap,
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
        };

        genBtn.Click += (_, _) =>
        {
            var m = Modes[modeBox.SelectedIndex];
            if (subject.Text.Trim().Length == 0) { Show(error, $"{m.Field1Label} is required."); return; }
            var text = m.Template.Replace("{subject}", subject.Text.Trim());
            text = m.Reformat
                ? text.Replace("{user_plan}", ownPlan.Text.Trim())
                : text.Replace("{claude_role}", role.Text.Trim().Length > 0 ? role.Text.Trim() : "seasoned mentor")
                      .Replace("{area_of_interest}", area.Text.Trim().Length > 0 ? area.Text.Trim() : subject.Text.Trim());
            prompt.Text = text;
            copyBtn.IsEnabled = true;
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

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
            if (PlanStore.LoadActivePlans().Any(p => p.Id == id))
                return $"A plan with id '{id}' is already active.";

            // Fill defaults the Python importer would have asked for.
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            var output = new Dictionary<string, object?>();
            foreach (var (k, v) in dict) output[k] = v;
            if (!dict.ContainsKey("start_date"))
                output["start_date"] = DateTime.Today.ToString("yyyy-MM-dd");
            if (!dict.ContainsKey("color"))
                output["color"] = "#bf5af2";

            Directory.CreateDirectory(AppPaths.ActivePlansDir);
            File.WriteAllText(
                Path.Combine(AppPaths.ActivePlansDir, $"{id}.json"),
                JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            return null;
        }
    }

    private static async Task Message(Page host, string title, string body)
    {
        await new ContentDialog
        {
            Title = title,
            Content = body,
            CloseButtonText = "OK",
            XamlRoot = host.XamlRoot,
        }.ShowAsync();
    }
}

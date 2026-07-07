using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _initialising = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var theme = StateService.Load().Theme;
            ThemeChoice.SelectedIndex = theme switch
            {
                "light" => 1, "dark" => 2, _ => 0,
            };
            OpacitySlider.Value = StateService.Load().Opacity;
            StartupToggle.IsOn = StartupService.IsEnabled;
            RefreshTickTickStatus();
            LoadRules();
            _initialising = false;

            var win = App.MainWindow as MainWindow;
            TrackerInfo.Text = win?.Tracker is { Running: true }
                ? "This app is tracking your activity (60s polls, diary 06:00–20:00)."
                : "Tracking is off in this app — the Python app was already running " +
                  "and only one tracker may write the diary at a time.";
            DataInfo.Text = $"Mentor Overseer v{AppVersion.Current}\nShared data folder: " + AppPaths.Root;
        };
    }

    // ── rules & timing (writes the shared config.json) ───────────────────

    private void LoadRules()
    {
        var cfg = ConfigService.Root;
        static string Words(System.Text.Json.JsonElement root, string section, string key)
        {
            if (!root.TryGetProperty(section, out var s) || !s.TryGetProperty(key, out var arr))
                return "";
            return string.Join("\n", arr.EnumerateArray()
                .Select(v => v.GetString() ?? "").Where(v => v.Length > 0));
        }
        static string Time(System.Text.Json.JsonElement root, string key, string fallback) =>
            root.TryGetProperty("working_hours", out var wh) &&
            wh.TryGetProperty(key, out var v) && v.GetString() is { Length: > 0 } s ? s : fallback;
        static int Num(System.Text.Json.JsonElement root, string key, int fallback) =>
            root.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : fallback;

        WorkStart.Text = Time(cfg, "start", "08:00");
        WorkEnd.Text = Time(cfg, "end", "20:00");
        EodTimeBox.Text = cfg.TryGetProperty("end_of_day_summary_time", out var eod)
            ? eod.GetString() ?? "20:00" : "20:00";
        GraceMin.Value = Num(cfg, "reminder_grace_minutes", 15);
        RepeatMin.Value = Num(cfg, "reminder_interval_minutes", 5);
        IdleMin.Value = Num(cfg, "idle_threshold_minutes", 10);
        RulesOn.Text = Words(cfg, "activity_rules", "on_plan");
        RulesOff.Text = Words(cfg, "activity_rules", "off_plan");
        RulesNeutral.Text = Words(cfg, "activity_rules", "neutral");
        IdleOn.Text = Words(cfg, "idle_activity_rules", "on_plan");
        IdleOff.Text = Words(cfg, "idle_activity_rules", "off_plan");
        IdleNeutral.Text = Words(cfg, "idle_activity_rules", "neutral");
    }

    private void SaveRules_Click(object sender, RoutedEventArgs e)
    {
        // Validate the three times before anything is written.
        foreach (var (box, label) in new[]
                 { (WorkStart, "Work start"), (WorkEnd, "Work end"), (EodTimeBox, "Day review at") })
        {
            if (!TimeOnly.TryParseExact(box.Text.Trim(), "HH:mm", out _))
            {
                SaveStatus.Text = $"{label} must be HH:MM (e.g. 08:00).";
                return;
            }
        }

        static System.Text.Json.Nodes.JsonArray Lines(TextBox box) =>
            new(box.Text.Split('\n', '\r')
                .Select(l => l.Trim()).Where(l => l.Length > 0)
                .Select(l => (System.Text.Json.Nodes.JsonNode)l).ToArray());

        try
        {
            ConfigService.Mutate(cfg =>
            {
                cfg["working_hours"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["start"] = WorkStart.Text.Trim(),
                    ["end"] = WorkEnd.Text.Trim(),
                };
                cfg["end_of_day_summary_time"] = EodTimeBox.Text.Trim();
                cfg["reminder_grace_minutes"] = (int)GraceMin.Value;
                cfg["reminder_interval_minutes"] = (int)RepeatMin.Value;
                cfg["idle_threshold_minutes"] = (int)IdleMin.Value;
                cfg["activity_rules"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["on_plan"] = Lines(RulesOn),
                    ["off_plan"] = Lines(RulesOff),
                    ["neutral"] = Lines(RulesNeutral),
                };
                cfg["idle_activity_rules"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["on_plan"] = Lines(IdleOn),
                    ["off_plan"] = Lines(IdleOff),
                    ["neutral"] = Lines(IdleNeutral),
                };
            });
            // The tracker reads config once at construction — restart it so
            // keyword/threshold changes apply now, not at the next app start.
            (App.MainWindow as MainWindow)?.RestartTracker();
            SaveStatus.Text = "Saved — tracker restarted with the new rules.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.SaveRules", ex);
            SaveStatus.Text = "Couldn't save: " + ex.Message;
        }
    }

    private void RefreshTickTickStatus()
    {
        TickTickStatus.Text = TickTickService.IsAuthorized
            ? "Connected — personal tasks due today appear on the Today page."
            : "Not connected.";
        TickTickConnectBtn.Content = TickTickService.IsAuthorized
            ? "Reconnect TickTick…" : "Connect TickTick…";
    }

    private void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        StartupService.SetEnabled(StartupToggle.IsOn);
    }

    private void Opacity_Changed(object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_initialising) return;
        var value = (int)e.NewValue;
        (App.MainWindow as MainWindow)?.ApplyOpacity(value);

        var state = StateService.Load();
        state.Opacity = value;
        StateService.Save(state);
    }

    private async void TickTickConnect_Click(object sender, RoutedEventArgs e)
    {
        await Dialogs.TickTickConnectDialog.ShowAsync(XamlRoot);
        RefreshTickTickStatus();
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initialising) return;
        var tag = (ThemeChoice.SelectedItem as RadioButton)?.Tag as string ?? "default";

        var state = StateService.Load();
        state.Theme = tag;
        StateService.Save(state);

        if ((App.MainWindow as MainWindow)?.Content is FrameworkElement root)
            root.RequestedTheme = tag switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
    }
}

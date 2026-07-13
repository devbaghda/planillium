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
                : "Tracking isn't running — check data/mentor-winui.log for why it failed to start.";
            DataInfo.Text = $"{AppInfo.DisplayName} v{AppVersion.Current}\nShared data folder: " + AppPaths.Root;
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
        RetentionDays.Value = Num(cfg, "diary_retention_days", Database.DiaryRetentionDays);
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
                // User-configurable retention (2026-07-09 audit finding
                // #34) — Database.DiaryRetentionDays remains the fallback
                // default, read via ConfigService.DiaryRetentionDays().
                cfg["diary_retention_days"] = (int)RetentionDays.Value;
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

    private void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = DataExport.ExportAll();
            SaveStatus.Text = "Exported to " + path;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ExportAll", ex);
            SaveStatus.Text = "Export failed: " + ex.Message;
        }
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        int diaryRows, rollupDays;
        try
        {
            using var db = new Database();
            (diaryRows, rollupDays) = db.CountActivityHistory();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearHistory.Count", ex);
            SaveStatus.Text = "Couldn't read activity history: " + ex.Message;
            return;
        }
        if (diaryRows == 0 && rollupDays == 0)
        {
            SaveStatus.Text = "No activity history to clear.";
            return;
        }

        // This only clears the database — any report/data export the user
        // already saved to disk (data/report.html, report.csv,
        // full-export.json) is a second, untouched copy of some of the same
        // information. Rather than silently delete files the user may have
        // deliberately kept, name the ones that currently exist so "cleared"
        // doesn't quietly mean "except for that export from last week."
        var exportNames = new[] { "report.html", "report.csv", "full-export.json" }
            .Where(f => File.Exists(Path.Combine(AppPaths.Root, "data", f)))
            .ToList();
        var exportWarning = exportNames.Count > 0
            ? $"\n\nNote: {string.Join(", ", exportNames)} in your data folder still holds a copy " +
              "of some of this — delete those separately if you want them gone too."
            : "";

        var confirm = new ContentDialog
        {
            Title = "Clear activity history?",
            Content = $"Deletes {diaryRows} diary session(s) and {rollupDays} day(s) of " +
                      "rolled-up totals — the tracked window-activity record. Your plans, " +
                      "task completions, notes, and score are not affected. This cannot be undone." +
                      exportWarning,
            PrimaryButtonText = "Clear history",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        // VACUUM rewrites the whole database file, not just the deleted
        // rows, so its cost scales with total file size — on months of
        // history this can take a perceptible moment. Running it inline on
        // the click handler used to freeze the window for that whole time
        // (2026-07-09 audit finding #8); moved off the UI thread, with the
        // button disabled and a status message so a slow clear still reads
        // as "working," not "stuck."
        ClearHistoryBtn.IsEnabled = false;
        SaveStatus.Text = "Clearing…";
        try
        {
            await Task.Run(() =>
            {
                using var db = new Database();
                db.ClearActivityHistory();
            });
            SaveStatus.Text = "Activity history cleared.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearHistory", ex);
            SaveStatus.Text = "Couldn't clear activity history: " + ex.Message;
        }
        finally
        {
            ClearHistoryBtn.IsEnabled = true;
        }
    }

    /// <summary>
    /// Reflections (the evening review's one-line answers) previously had
    /// no delete path anywhere in the app — a deliberately separate action
    /// from "Clear activity history" above, since reflections are the
    /// user's own reflective text, not tracked window-activity data
    /// (2026-07-09 audit finding #12).
    /// </summary>
    private async void ClearReflections_Click(object sender, RoutedEventArgs e)
    {
        int count;
        try
        {
            using var db = new Database();
            using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
            count = score.CountReflections();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearReflections.Count", ex);
            SaveStatus.Text = "Couldn't read reflections: " + ex.Message;
            return;
        }
        if (count == 0)
        {
            SaveStatus.Text = "No reflections to clear.";
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Clear my reflections?",
            Content = $"Deletes {count} evening-review reflection(s). Your plans, task " +
                      "completions, activity diary, and score are not affected. This cannot be undone.",
            PrimaryButtonText = "Clear reflections",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        ClearReflectionsBtn.IsEnabled = false;
        SaveStatus.Text = "Clearing…";
        try
        {
            await Task.Run(() =>
            {
                using var db = new Database();
                using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
                score.ClearReflections();
            });
            SaveStatus.Text = "Reflections cleared.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearReflections", ex);
            SaveStatus.Text = "Couldn't clear reflections: " + ex.Message;
        }
        finally
        {
            ClearReflectionsBtn.IsEnabled = true;
        }
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initialising) return;
        var tag = (ThemeChoice.SelectedItem as RadioButton)?.Tag as string ?? "default";

        var state = StateService.Load();
        state.Theme = tag;
        StateService.Save(state);

        if ((App.MainWindow as MainWindow)?.Content is FrameworkElement root)
        {
            root.RequestedTheme = tag switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            // MainWindow's ActualThemeChanged subscription re-applies
            // ThemeSync automatically, but calling it directly here too
            // means the C#-built UI already on screen (this page included)
            // updates immediately rather than waiting for that event to
            // propagate through the dispatcher.
            ThemeSync.Apply(root.ActualTheme);
        }
    }
}

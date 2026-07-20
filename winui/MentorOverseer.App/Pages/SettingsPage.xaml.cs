using System.Globalization;
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
            YourName.Text = ConfigService.UserName;
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
        static double NumD(System.Text.Json.JsonElement root, string key, double fallback) =>
            root.TryGetProperty(key, out var v) && v.TryGetDouble(out var n) ? n : fallback;

        WorkStart.Text = Time(cfg, "start", "08:00");
        WorkEnd.Text = Time(cfg, "end", "20:00");
        EodTimeBox.Text = cfg.TryGetProperty("end_of_day_summary_time", out var eod)
            ? eod.GetString() ?? "20:00" : "20:00";
        GraceMin.Value = Num(cfg, "reminder_grace_minutes", 15);
        RepeatMin.Value = Num(cfg, "reminder_interval_minutes", 5);
        IdleMin.Value = Num(cfg, "idle_threshold_minutes", 10);
        RetentionDays.Value = Num(cfg, "diary_retention_days", Database.DiaryRetentionDays);
        LateDayReminderHours.Value = NumD(cfg, "late_day_task_reminder_hours", 2.0);
        RulesOn.Text = Words(cfg, "activity_rules", DiaryCategory.OnPlan);
        RulesOff.Text = Words(cfg, "activity_rules", DiaryCategory.OffPlan);
        RulesNeutral.Text = Words(cfg, "activity_rules", DiaryCategory.Neutral);
        IdleOn.Text = Words(cfg, "idle_activity_rules", DiaryCategory.OnPlan);
        IdleOff.Text = Words(cfg, "idle_activity_rules", DiaryCategory.OffPlan);
        IdleNeutral.Text = Words(cfg, "idle_activity_rules", DiaryCategory.Neutral);
    }

    /// <summary>Fires on LostFocus for every hours/reminders/keyword TextBox and
    /// ValueChanged for every NumberBox in that section — the whole group used to only
    /// save via an explicit "Save settings" button, inconsistent with Theme/Opacity/
    /// "start with Windows" above it (which already autosave), and with nothing warning
    /// if you navigated away with unsaved edits (audit finding #10). Saving the whole
    /// group together on any one field's change mirrors exactly what the old button did
    /// in one Mutate call — this just removes the extra click.</summary>
    private void Rules_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        SaveRules();
    }

    private void Rules_ValueChanged(NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
    {
        if (_initialising) return;
        SaveRules();
    }

    private void SaveRules()
    {
        // Validate the three times before anything is written.
        foreach (var (box, label) in new[]
                 { (WorkStart, "Work start"), (WorkEnd, "Work end"), (EodTimeBox, "Day review at") })
        {
            if (!TimeOnly.TryParseExact(box.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
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
                cfg["late_day_task_reminder_hours"] = LateDayReminderHours.Value;
                cfg["activity_rules"] = new System.Text.Json.Nodes.JsonObject
                {
                    [DiaryCategory.OnPlan] = Lines(RulesOn),
                    [DiaryCategory.OffPlan] = Lines(RulesOff),
                    [DiaryCategory.Neutral] = Lines(RulesNeutral),
                };
                cfg["idle_activity_rules"] = new System.Text.Json.Nodes.JsonObject
                {
                    [DiaryCategory.OnPlan] = Lines(IdleOn),
                    [DiaryCategory.OffPlan] = Lines(IdleOff),
                    [DiaryCategory.Neutral] = Lines(IdleNeutral),
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
            SaveStatus.Text = Log.Friendly("Couldn't save your settings", ex);
        }
    }

    private void RefreshTickTickStatus()
    {
        TickTickStatus.Text = TickTickService.IsAuthorized
            ? "Connected — personal tasks due today appear on the Today page."
            : "Not connected.";
        TickTickConnectBtn.Content = TickTickService.IsAuthorized
            ? "Reconnect TickTick…" : "Connect TickTick…";
        // Only worth offering once there's something to disconnect — before the first
        // connect, or after a Disconnect/Clear-all-data already removed the tokens,
        // there's nothing left for it to do (2026-07-18 audit finding R10-02).
        TickTickDisconnectBtn.Visibility = TickTickService.IsAuthorized
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        StartupService.SetEnabled(StartupToggle.IsOn);
    }

    /// <summary>Autosaves like Theme/Opacity/"start with Windows" above it — previously
    /// this could only be set once, at first run, via NameSetupDialog, with no way to see
    /// or change it afterward short of opening config.json directly (2026-07-18 audit
    /// finding R10-13). An empty name is valid (mentor-voice copy already falls back to a
    /// neutral greeting) — nothing here forces a value.</summary>
    private void YourName_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        try
        {
            ConfigService.Mutate(cfg => cfg["user_name"] = YourName.Text.Trim());
            // Every other autosaving field on this page confirms on success — this one
            // didn't, so typing a name and tabbing away gave no sign it actually saved
            // (2026-07-18 audit finding R11-09).
            SaveStatus.Text = "Saved.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.YourName", ex);
            SaveStatus.Text = Log.Friendly("Couldn't save your name", ex);
        }
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

    /// <summary>Removes the stored client secret and tokens, and the saved client ID —
    /// previously the only way to fully undo a "Connect TickTick" was to open Windows
    /// Credential Manager by hand and find the right entries (2026-07-18 audit finding
    /// R10-02).</summary>
    private async void TickTickDisconnect_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Disconnect TickTick?",
            Content = "Removes the saved client ID, secret, and access tokens from this " +
                      "PC. Your TickTick account and tasks themselves aren't affected — " +
                      "you can reconnect any time.",
            PrimaryButtonText = "Disconnect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        // Unlike every other mutating button on this page, this one had no try/catch —
        // a failed write partway through (Disconnect deletes 3 credentials, then rewrites
        // config.json) could silently leave the screen still saying "Connected" with no
        // error shown (2026-07-18 audit finding R11-01, found independently by two audit
        // passes). RefreshTickTickStatus in `finally` keeps the screen honest either way.
        try
        {
            TickTickAuth.Disconnect();
            SaveStatus.Text = "TickTick disconnected.";
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.TickTickDisconnect", ex);
            SaveStatus.Text = Log.Friendly("Couldn't fully disconnect TickTick", ex);
        }
        finally
        {
            RefreshTickTickStatus();
        }
    }

    private async void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Off the UI thread — this opens the DB and reads every user table, which will
            // only grow with time and used to run directly inside the button click handler
            // (audit finding #12).
            var path = await Task.Run(() => DataExport.ExportAll());
            SaveStatus.Text = "Exported to " + path;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ExportAll", ex);
            SaveStatus.Text = Log.Friendly("Couldn't export your data", ex);
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
            SaveStatus.Text = Log.Friendly("Couldn't read your activity history", ex);
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
        // deliberately kept, name the ones that currently exist and offer
        // an opt-in checkbox to remove them in the same action instead of
        // just warning and leaving it to a manual trip to File Explorer.
        var exportNames = ExportFiles.All
            .Where(f => File.Exists(Path.Combine(AppPaths.Root, "data", f)))
            .ToList();

        var confirmPanel = new StackPanel { Spacing = 10 };
        confirmPanel.Children.Add(new TextBlock
        {
            Text = $"Deletes {diaryRows} diary session(s) and {rollupDays} day(s) of " +
                   "rolled-up totals — the tracked window-activity record. Your plans, " +
                   "task completions, notes, and score are not affected. This cannot be undone.",
            TextWrapping = TextWrapping.Wrap,
        });
        var deleteExportsBox = new CheckBox
        {
            Content = $"Also delete {string.Join(", ", exportNames)} from your data folder",
            Visibility = Visibility.Collapsed,
        };
        if (exportNames.Count > 0)
        {
            confirmPanel.Children.Add(new TextBlock
            {
                Text = $"{string.Join(", ", exportNames)} in your data folder still holds a copy " +
                       "of some of this.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            deleteExportsBox.Visibility = Visibility.Visible;
            confirmPanel.Children.Add(deleteExportsBox);
        }

        var confirm = new ContentDialog
        {
            Title = "Clear activity history?",
            Content = confirmPanel,
            PrimaryButtonText = "Clear history",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;
        var deleteExports = deleteExportsBox.IsChecked == true;

        // VACUUM rewrites the whole database file, not just the deleted
        // rows, so its cost scales with total file size — on months of
        // history this can take a perceptible moment. Running it inline on
        // the click handler used to freeze the window for that whole time
        // (2026-07-09 audit finding #8); moved off the UI thread, with the
        // button disabled and a status message so a slow clear still reads
        // as "working," not "stuck."
        await RunClearActionAsync(ClearHistoryBtn, () =>
        {
            using var db = new Database();
            db.ClearActivityHistory();
            if (deleteExports) ThrowIfExportFilesRemain(DeleteExportFiles(exportNames));
        }, "Activity history cleared.", "your activity history", "SettingsPage.ClearHistory");
    }

    /// <summary>Deletes each named file from the data folder, best-effort — logs and
    /// continues past a locked file instead of leaving the rest untried. Returns the
    /// names that could NOT be removed, so the caller can decide whether to report that
    /// (2026-07-18 audit finding, found during round-8's own re-audit: both "clear" actions
    /// used to swallow a delete failure into the log only, so a file left open elsewhere —
    /// report.csv in Excel, say — silently survived a "cleared" action with no error shown).</summary>
    private static List<string> DeleteExportFiles(IEnumerable<string> names)
    {
        var failed = new List<string>();
        foreach (var f in names)
        {
            try { File.Delete(Path.Combine(AppPaths.Root, "data", f)); }
            catch (Exception ex)
            {
                Log.Error($"SettingsPage.DeleteExportFile({f})", ex);
                failed.Add(f);
            }
        }
        return failed;
    }

    /// <summary>Distinguishes "the clear itself failed" from "the clear succeeded, but a
    /// leftover export file couldn't be removed" — RunClearActionAsync's catch checks for
    /// this type specifically so the two cases get different headlines instead of both
    /// being flattened into a generic "Couldn't clear X" that would contradict this
    /// exception's own already-correct message (2026-07-18 audit finding R10-01: wrapping
    /// this in Log.Friendly's "Couldn't clear..." prefix produced a message that said the
    /// clear failed in its first sentence and succeeded in its second).</summary>
    private sealed class ExportCleanupException(string message) : Exception(message);

    /// <summary>Throws so a partial file-delete failure surfaces through
    /// RunClearActionAsync's existing error path instead of being silently absorbed —
    /// the database-level clear this runs alongside already succeeded by this point, so
    /// the message is explicit that it's the leftover file(s), not the data itself.</summary>
    private static void ThrowIfExportFilesRemain(List<string> failed)
    {
        if (failed.Count > 0)
            throw new ExportCleanupException(
                $"Your data was cleared, but {string.Join(", ", failed)} couldn't be removed — close it elsewhere and try again.");
    }

    /// <summary>Shared busy-state sequence for the two "clear my data"
    /// buttons below — confirm dialog already shown by the caller; this
    /// covers disable-button → "Clearing…" → run off the UI thread →
    /// status message → re-enable, so the two buttons can't drift apart on
    /// this shared shape while still owning their own confirm copy/dbAction.
    /// <paramref name="whatFailed"/> keeps the failure message specific
    /// ("Couldn't clear reflections" vs "Couldn't clear activity history")
    /// instead of a single generic one that reads the same for both.</summary>
    private async Task RunClearActionAsync(Button trigger, Action dbAction, string successMessage,
        string whatFailed, string logTag)
    {
        trigger.IsEnabled = false;
        SaveStatus.Text = "Clearing…";
        try
        {
            await Task.Run(dbAction);
            SaveStatus.Text = successMessage;
        }
        catch (ExportCleanupException ex)
        {
            // Already a complete, correct, human-readable message — the clear itself
            // succeeded, only a leftover export file didn't. Log.Friendly's "Couldn't
            // clear..." prefix would contradict it (R10-01), so show it as-is.
            Log.Warn(logTag, ex.Message);
            SaveStatus.Text = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error(logTag, ex);
            SaveStatus.Text = Log.Friendly($"Couldn't clear {whatFailed}", ex);
        }
        finally
        {
            trigger.IsEnabled = true;
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
            SaveStatus.Text = Log.Friendly("Couldn't read your reflections", ex);
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

        await RunClearActionAsync(ClearReflectionsBtn, () =>
        {
            using var db = new Database();
            using var score = new ScoreService(PlanStore.LoadActivePlans(), db);
            score.ClearReflections();
        }, "Reflections cleared.", "your reflections", "SettingsPage.ClearReflections");
    }

    /// <summary>
    /// Wipes every remaining data table this app keeps (task completions, reschedules/
    /// day-offs, task notes, score history, reflections, TickTick sync links, and the
    /// activity diary) plus the debug log, in one action — before this, only activity
    /// history and reflections had any delete path at all (audit finding #6); the debug
    /// log had none either (audit finding #25). Plan definitions themselves
    /// (plans/active/*.json) are never touched here — archiving/deleting a plan is its
    /// own separate action on the Plans page. Also deletes any report.html/report.csv/
    /// full-export.json sitting in the data folder — unconditionally, unlike the smaller
    /// "Clear activity history" button's opt-in checkbox, since this action is already the
    /// most destructive one in the app and its own confirmation already says "this cannot
    /// be undone" (2026-07-18 audit finding R8-05: this used to only clear the database,
    /// leaving those export files as an untouched second copy of the same data).
    /// </summary>
    private async void ClearAllData_Click(object sender, RoutedEventArgs e)
    {
        int rowCount;
        try
        {
            using var db = new Database();
            rowCount = db.CountAllData();
        }
        catch (Exception ex)
        {
            Log.Error("SettingsPage.ClearAllData.Count", ex);
            SaveStatus.Text = Log.Friendly("Couldn't read your data", ex);
            return;
        }
        if (rowCount == 0)
        {
            SaveStatus.Text = "No data to clear.";
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Clear all my data?",
            Content = $"Deletes {rowCount} row(s) across every data table this app keeps — " +
                      "task completions, reschedules and day-offs, task notes, score history, " +
                      "reflections, TickTick sync links, and the activity diary — plus the debug " +
                      "log, your saved TickTick connection (client ID, secret, and tokens), and " +
                      "any report.html/report.csv/full-export.json you've exported to the data " +
                      "folder. Your plan definitions and the other settings on this page " +
                      "(including your name) are not touched — use the Plans page to archive " +
                      "or remove a plan. This cannot be undone.",
            PrimaryButtonText = "Clear everything",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await Dialogs.DialogGate.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        await RunClearActionAsync(ClearAllDataBtn, () =>
        {
            using var db = new Database();
            db.ClearAllData();
            Log.Clear();
            // Previously only the ticktick_sync database table (plan-task ↔ TickTick-task
            // ID links) was cleared here — the actual credentials in Windows Credential
            // Manager survived every "clear all my data" run with no way to remove them
            // short of opening Credential Manager by hand (2026-07-18 audit finding R10-02).
            TickTickAuth.Disconnect();
            ThrowIfExportFilesRemain(DeleteExportFiles(ExportFiles.All));
        }, "All data cleared.", "your data", "SettingsPage.ClearAllData");
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

using Microsoft.Win32;

namespace MentorOverseer.App.Services;

/// <summary>
/// Start-with-Windows via the HKCU Run key. Value name is distinct from the
/// retired Python app's names — and Enable() sweeps those legacy names so
/// only ever one Planillium starts at boot.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.StartupRegistryValue;
    private static readonly string[] LegacyNames = { "Mentor-Overseer", "NetherlandsMentor", AppInfo.LegacyStartupRegistryValue };

    private static string Command =>
        $"\"{Environment.ProcessPath}\" --minimized";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception ex)
            {
                Log.Error("StartupService.IsEnabled", ex);
                return false;
            }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
            {
                // Unlike every other failure path in this file, this used to return
                // silently — the toggle would appear to save with no error and nothing
                // in the log explaining why it didn't actually take (round-5 audit
                // finding #35). Narrow in practice (a policy-restricted HKCU), but worth
                // a trace if it ever happens.
                Log.Warn("StartupService.SetEnabled", $"couldn't open '{RunKey}' as writable");
                return;
            }
            foreach (var legacy in LegacyNames)
                key.DeleteValue(legacy, throwOnMissingValue: false);
            if (enabled) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Error("StartupService.SetEnabled", ex);
        }
    }
}

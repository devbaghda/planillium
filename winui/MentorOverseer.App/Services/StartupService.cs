using Microsoft.Win32;

namespace MentorOverseer.App.Services;

/// <summary>
/// Start-with-Windows via the HKCU Run key. Value name is distinct from the
/// retired Python app's names — and Enable() sweeps those legacy names so
/// only ever one Mentor Overseer starts at boot.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MentorOverseer";
    private static readonly string[] LegacyNames = { "Mentor-Overseer", "NetherlandsMentor" };

    private static string Command =>
        $"\"{Environment.ProcessPath}\" --minimized";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
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

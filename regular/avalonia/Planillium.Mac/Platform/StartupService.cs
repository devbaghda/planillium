namespace Planillium.App.Services;

/// <summary>Mac version: start at login via a LaunchAgent plist in ~/Library/LaunchAgents.</summary>
public static class StartupService
{
    private const string Label = "com.planillium.app";
    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", Label + ".plist");

    public static bool IsEnabled => !AppPaths.IsScratch && File.Exists(PlistPath);

    public static void SetEnabled(bool enabled)
    {
        if (AppPaths.IsScratch)
        {
            Log.Info("StartupService.SetEnabled: scratch data folder - the real start-at-login entry was not touched");
            return;
        }
        try
        {
            if (!enabled) { if (File.Exists(PlistPath)) File.Delete(PlistPath); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
            File.WriteAllText(PlistPath,
$@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0""><dict>
<key>Label</key><string>{Label}</string>
<key>ProgramArguments</key><array><string>{Environment.ProcessPath}</string><string>--minimized</string></array>
<key>RunAtLoad</key><true/>
</dict></plist>
");
        }
        catch (Exception ex) { Log.Error("StartupService.SetEnabled", ex); }
    }
}

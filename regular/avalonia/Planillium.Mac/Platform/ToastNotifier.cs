using System.Diagnostics;

namespace Planillium.App.Services;

public static class ToastArgs
{
    public const string Action = "action";
    public const string Kickoff = "kickoff";
    public const string IdleReturn = "idlereturn";
    public const string Review = "review";
    public const string Mins = "mins";
    public const string Start = "start";
}

/// <summary>Mac version: a macOS notification via osascript. Clicking it does not carry the action
/// arguments back (macOS limitation of this method); the in-app notification recap still holds them.</summary>
public static class ToastNotifier
{
    public static void Show(string title, string message, string? tag, params (string Key, string Value)[] args)
    {
        try
        {
            var dict = new Dictionary<string, string>();
            foreach (var (k, v) in args) dict[k] = v;
            static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var psi = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add($"display notification \"{Esc(message)}\" with title \"{Esc(title)}\"");
            Process.Start(psi);
            NotificationCenter.Record(title, message, dict);
        }
        catch (Exception ex) { Log.Error("ToastNotifier.Show", ex); }
    }
}

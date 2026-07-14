using System.Globalization;

namespace MentorOverseer.App.Services;

/// <summary>
/// Minimal file logger — the WinUI counterpart of the Python app's
/// data/mentor.log. Every catch block routes here so failures leave a trace
/// instead of vanishing (audit findings #1/#2).
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    private static string PathOf
    {
        get
        {
            if (_path != null) return _path;
            try
            {
                var dir = Path.Combine(AppPaths.Root, "data");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "mentor-winui.log");
            }
            catch
            {
                // Data root unresolvable — fall back next to the exe so the
                // failure that *caused* that is still recorded somewhere.
                _path = Path.Combine(AppContext.BaseDirectory, "mentor-winui.log");
            }
            return _path;
        }
    }

    public static void Error(string context, Exception ex) =>
        Write("ERROR", $"{context}: {ex}");

    public static void Warn(string context, string message) =>
        Write("WARN", $"{context}: {message}");

    public static void Info(string message) => Write("INFO", message);

    // Uncapped before this — the log file just grew forever for as long as the app
    // was installed, and sat outside every other privacy tool (Settings' Export/Clear
    // buttons never touched it) — round-5 audit finding #13.
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now.ToIsoTimestamp()} " +
                       $"[{level}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                RotateIfTooBig();
                File.AppendAllText(PathOf, line);
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    /// <summary>Trims the file back to its newer half once it crosses MaxSizeBytes —
    /// a debug trail, not data worth preserving across the cap, so discarding the
    /// oldest half in place is enough; no archived .old file to also grow forever.</summary>
    private static void RotateIfTooBig()
    {
        if (!File.Exists(PathOf)) return;
        if (new FileInfo(PathOf).Length <= MaxSizeBytes) return;
        var lines = File.ReadAllLines(PathOf);
        File.WriteAllLines(PathOf, lines.Skip(lines.Length / 2));
    }
}

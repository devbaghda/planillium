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

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} " +
                       $"[{level}] {message}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(PathOf, line);
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}

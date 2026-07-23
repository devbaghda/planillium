using System.Globalization;

namespace Planillium.App.Services;

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

    /// <summary>Turns a caught exception into UI copy that leads with what to do, not just
    /// the raw exception text — the technical detail stays (in parentheses), since for this
    /// app's actual audience (the developer, running it for themselves) it's genuinely useful
    /// for self-diagnosis, not noise to hide (2026-07-18 audit finding R9-02). One shared
    /// helper instead of every page re-typing its own "{what}: {ex.Message}" — the same
    /// "don't duplicate the string" reasoning as R9-01 in the same audit round.</summary>
    /// <param name="action">Overrides the default "try again, or check the log" clause for
    /// cases with a more specific fix to name (e.g. "close the other app using it") —
    /// added so call sites with real custom action text don't have to skip this helper
    /// entirely just because the generic default doesn't fit (2026-07-18 audit finding
    /// R10-12).</param>
    public static string Friendly(string what, Exception ex,
        string action = "try again, or check data/mentor-winui.log for details") =>
        $"{what} — {action}. ({ex.Message})";

    /// <summary>Deletes the log file outright — this was the one piece of the app's data
    /// that sat outside every "clear my data" control (audit finding #25); folded into
    /// Settings' "Clear all my data" action rather than a standalone button, since this
    /// log has already been confirmed to hold nothing more sensitive than timestamps and
    /// exception text (never window titles, task text, or reflection content).</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            try { File.Delete(PathOf); }
            catch (Exception ex)
            {
                // Best-effort — a locked/undeletable log file shouldn't block the rest of
                // "clear all my data" from completing.
                try { Write("WARN", $"Log.Clear: {ex.GetType().Name}: {ex.Message}"); }
                catch { /* logging must never take the app down */ }
            }
        }
    }

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
    /// oldest half in place is enough; no archived .old file to also grow forever.
    /// Seeks to the halfway byte offset and streams the rest straight through instead
    /// of reading the whole file into a line array and rejoining it — this runs under
    /// Write's lock and can be invoked from the UI thread, so turning an O(file) string
    /// operation into an O(seek) byte copy avoids a rare but real stall (audit finding
    /// #11).</summary>
    private static void RotateIfTooBig()
    {
        if (!File.Exists(PathOf)) return;
        var info = new FileInfo(PathOf);
        if (info.Length <= MaxSizeBytes) return;

        var tmp = PathOf + ".tmp";
        using (var input = new FileStream(PathOf, FileMode.Open, FileAccess.Read))
        using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            input.Seek(info.Length / 2, SeekOrigin.Begin);
            int b;
            while ((b = input.ReadByte()) != -1 && b != '\n') { } // land on a clean line boundary
            input.CopyTo(output);
        }
        File.Move(tmp, PathOf, overwrite: true);
    }
}

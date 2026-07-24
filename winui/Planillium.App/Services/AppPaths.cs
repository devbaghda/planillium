using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>
/// Locates the data root — config.json / plans/ / data/progress.db.
/// Resolution order: MENTOR_ROOT env var (test/dev builds only — see below),
/// then walking up from the exe directory looking for a folder that has both
/// "plans" and "config.json".
/// </summary>
public static class AppPaths
{
    private static string? _root;

    public static string Root
    {
        get
        {
            if (_root != null) return _root;

            // #if DEBUG-gated like its siblings, MENTOR_PAGE (MainWindow.xaml.cs) and
            // MENTOR_INSTANCE_SUFFIX (App.xaml.cs) — a real user running the shipped
            // Release exe has no legitimate reason to redirect the app's entire data root
            // via an environment variable, and leaving it live there meant anything able to
            // set an env var before launch (a script, a modified shortcut) could point the
            // app at an arbitrary folder of its choosing (2026-07-24 audit finding #10).
            // Genuinely still needed in Debug: Planillium.App.Tests' TestRootFixture relies
            // on this exact hook to isolate the test suite's SQLite file from the real
            // data/progress.db (dotnet test builds Debug by default, so this stays live for it).
#if DEBUG
            var env = Environment.GetEnvironmentVariable("MENTOR_ROOT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
                return _root = env;
#endif

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "config.json")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "plans")))
                    return _root = dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                $"Couldn't find the {AppInfo.DisplayName} data folder (config.json + plans/)."
#if DEBUG
                + " Set the MENTOR_ROOT environment variable to point at it."
#endif
                );
        }
    }

    public static string ActivePlansDir => Path.Combine(Root, "plans", "active");
    /// <summary>Plan ideas saved for later — created when a user hits the active-plan limit
    /// but still wants to capture an idea instead of losing it (2026-07-22 request). Inert:
    /// nothing here is loaded into scoring/Today/Schedule until a plan is activated (moved
    /// to <see cref="ActivePlansDir"/> with its start_date reset).</summary>
    public static string QueuedPlansDir => Path.Combine(Root, "plans", "queued");
    public static string DbPath => Path.Combine(Root, "data", "progress.db");

    /// <summary>
    /// Opens a connection with a busy timeout set — this app's own poll
    /// thread (ActivityTracker) and UI thread each hold independent
    /// connections to the same file, so a completion toggle landing in the
    /// same instant as a 60s poll write must wait briefly instead of
    /// throwing SQLITE_BUSY immediately.
    /// </summary>
    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        // WAL lets a reader (e.g. Reports opening mid-poll) proceed
        // alongside a writer instead of blocking on it — busy_timeout alone
        // only bounds how long that wait can take, it doesn't avoid it.
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=2000;";
        cmd.ExecuteNonQuery();
        return conn;
    }
}

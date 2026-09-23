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
            //
            // ALSO gated on PLANILLIUM_TESTS (defined unconditionally — both Debug and
            // Release — by Planillium.App.Tests.csproj, which source-links this exact file
            // rather than referencing the compiled Planillium.App.dll): this file used to be
            // #if DEBUG only, on the assumption that `dotnet test` always builds Debug. It
            // doesn't when invoked with `-c Release`, and this file gets recompiled from
            // source as part of that build — DEBUG is then undefined, the block below never
            // exists in the compiled test binary, and TestRootFixture's env var has nothing
            // to attach to. AppPaths.Root then falls straight through to the walk-up-from-
            // exe-directory branch, which finds THIS REPO'S OWN config.json + plans/ and
            // silently points the "isolated" test run at the real data/progress.db.
            //
            // Confirmed real (2026-08-13): a `dotnet test -c Release` run left dozens of
            // fake plan_id rows in the real task_overrides/task_completions/plan_days_off
            // tables, and — because score_ledger's daily_score row is keyed by (reason,
            // date) with no plan_id at all — any test that called CreditDayScoreIfMissing/
            // RecalculateDayScore for DateTime.Today (several do) could silently overwrite
            // or delete the real ledger row for whatever the real calendar date happened to
            // be at test-run time. That's indistinguishable from a legitimate recompute after
            // the fact, so the historical extent of this is not something a query can recover
            // — see CONTEXT.md for what was and wasn't cleaned up.
#if DEBUG || PLANILLIUM_TESTS
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

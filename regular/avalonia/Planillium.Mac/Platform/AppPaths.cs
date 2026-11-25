using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>
/// Mac version of AppPaths. Data folder resolution:
///   1. MENTOR_ROOT, if set. It must exist; a mistyped path refuses to start and never falls back
///      (same rule as Windows, G14). A MENTOR_ROOT run is a "scratch" run: it never reads the real
///      Keychain secrets and never calls the real AI / TickTick services.
///   2. Otherwise ~/Library/Application Support/Planillium, if it holds config.json + plans/.
/// </summary>
public static class AppPaths
{
    private static string? _root;

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Planillium");

    public static string Root
    {
        get
        {
            if (_root != null) return _root;
            if (MissingScratchFolder is { } missing)
                throw new DirectoryNotFoundException($"The data folder set by MENTOR_ROOT does not exist: {missing}");
            if (ScratchRoot() is { } scratch) return _root = scratch;
            var def = DefaultFolder;
            if (File.Exists(Path.Combine(def, "config.json")) && Directory.Exists(Path.Combine(def, "plans")))
                return _root = def;
            throw new DirectoryNotFoundException(
                $"Couldn't find the {AppInfo.DisplayName} data folder. Expected config.json and plans/ in {def}, or set MENTOR_ROOT.");
        }
    }

    public static bool IsScratch => ScratchRoot() != null;
    public static bool ScratchRequested => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MENTOR_ROOT"));

    public static string? MissingScratchFolder
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("MENTOR_ROOT");
            return !string.IsNullOrEmpty(env) && !Directory.Exists(env) ? env : null;
        }
    }

    private static string? ScratchRoot()
    {
        var env = Environment.GetEnvironmentVariable("MENTOR_ROOT");
        return !string.IsNullOrEmpty(env) && Directory.Exists(env) ? env : null;
    }

    public static string ActivePlansDir => Path.Combine(Root, "plans", "active");
    public static string QueuedPlansDir => Path.Combine(Root, "plans", "queued");
    public static string DbPath => Path.Combine(Root, "data", "progress.db");

    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=2000;";
        cmd.ExecuteNonQuery();
        return conn;
    }
}

using System.Text.Json;

namespace Planillium.App.Services;

/// <summary>
/// Two small utilities shared by every plain-JSON file this app writes
/// directly (plan files, config.json) — not the SQLite database, which has
/// its own transaction wrapper (Database.RunInTransaction).
/// </summary>
internal static class JsonFileIO
{
    /// <summary>A JsonSerializerOptions built from scratch (rather than copied
    /// from .Default) has no TypeInfoResolver and throws on ToJsonString as
    /// soon as the tree holds a node added via the generic JsonArray/JsonObject
    /// .Add&lt;T&gt;() — this workaround, and its explanatory comment, used to be
    /// duplicated across PlanStore and ConfigService (2026-07-14 round-6 audit
    /// finding #20).</summary>
    public static readonly JsonSerializerOptions Indented =
        new(JsonSerializerOptions.Default) { WriteIndented = true };

    /// <summary>Write-to-temp-then-replace so a crash or kill mid-write can
    /// never leave the real file half-written, unreadable on the next launch
    /// — plain File.WriteAllText left that corruption window open on every
    /// plan/config save (2026-07-14 round-6 audit finding #16). File.Move's
    /// overwrite:true is effectively instant (a filesystem rename, not a
    /// copy), so the window an interruption could land in shrinks to
    /// nothing observable.</summary>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        File.Move(tmp, path, overwrite: true);
    }
}

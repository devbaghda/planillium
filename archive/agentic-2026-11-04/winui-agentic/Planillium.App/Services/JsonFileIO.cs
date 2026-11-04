using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Planillium.App.Services;

/// <summary>
/// Two small utilities shared by every plain-JSON file this app writes
/// directly (plan files, config.json) — not the SQLite database, which has
/// its own transaction wrapper (Database.RunInTransaction).
/// </summary>
internal static class JsonFileIO
{
    /// <summary>G11 (2026-10-30): the one resolver every JsonSerializerOptions in this app names
    /// explicitly. An options object without a TypeInfoResolver throws "must specify a
    /// TypeInfoResolver" when JsonNode.ToJsonString / GetTypeInfo marks it read-only, and every
    /// JsonSerializer call fails if reflection-by-default is ever switched off (trimming, AOT,
    /// JsonSerializerIsReflectionEnabledByDefault=false). Naming it makes each call independent of
    /// that switch and of which call touched a shared options object first.</summary>
    public static readonly IJsonTypeInfoResolver Resolver = new DefaultJsonTypeInfoResolver();

    /// <summary>Default settings with the explicit resolver: use instead of the parameterless
    /// JsonSerializer overloads (which fall back to JsonSerializerOptions.Default).</summary>
    public static readonly JsonSerializerOptions Plain = new() { TypeInfoResolver = Resolver };

    /// <summary>A JsonSerializerOptions built from scratch with no TypeInfoResolver throws on
    /// ToJsonString as soon as the tree holds a node added via the generic JsonArray/JsonObject
    /// .Add&lt;T&gt;() (2026-07-14 round-6 audit finding #20; was a copy of .Default, whose resolver
    /// is empty when reflection is off — now the explicit <see cref="Resolver"/>, G11).</summary>
    public static readonly JsonSerializerOptions Indented =
        new() { WriteIndented = true, TypeInfoResolver = Resolver };

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

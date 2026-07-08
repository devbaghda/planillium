using System.Text.Json;
using System.Text.Json.Nodes;

namespace MentorOverseer.App.Services;

/// <summary>
/// View of the shared config.json. Reads are cached; targeted writes go
/// through Mutate(), which round-trips the whole file as JSON so keys this
/// app doesn't know about survive untouched.
/// </summary>
public static class ConfigService
{
    private static string ConfigPath => Path.Combine(AppPaths.Root, "config.json");

    /// <summary>Load-mutate-save the config; invalidates the read cache.</summary>
    public static void Mutate(Action<JsonObject> change)
    {
        var node = File.Exists(ConfigPath)
            ? JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();
        change(node);
        File.WriteAllText(ConfigPath, node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Invalidate();
    }

    private static JsonDocument? _doc;

    public static JsonElement Root
    {
        get
        {
            if (_doc is null)
            {
                var path = Path.Combine(AppPaths.Root, "config.json");
                _doc = File.Exists(path)
                    ? JsonDocument.Parse(File.ReadAllText(path))
                    : JsonDocument.Parse("{}");
            }
            return _doc.RootElement.Clone();
        }
    }

    public static void Invalidate() { _doc?.Dispose(); _doc = null; }

    public static int ScoringRate(string key, int fallback) =>
        Root.TryGetProperty("scoring", out var s) &&
        s.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : fallback;

    /// <summary>Spend rates from config["score"] — same defaults as main.py _score_rates().</summary>
    public static (double PtsPerMin, double PtsPerUnit, string Symbol) SpendRates()
    {
        double ppm = 1.0, ppu = 10.0;
        var sym = "€";
        if (Root.TryGetProperty("score", out var s))
        {
            if (s.TryGetProperty("points_per_minute", out var a) && a.TryGetDouble(out var d1)) ppm = d1;
            if (s.TryGetProperty("points_per_currency_unit", out var b) && b.TryGetDouble(out var d2)) ppu = d2;
            if (s.TryGetProperty("currency_symbol", out var c) && c.GetString() is { Length: > 0 } cs) sym = cs;
        }
        return (ppm, ppu, sym);
    }

    /// <summary>Empty until the first-launch NameSetupDialog asks and saves it.</summary>
    public static string UserName =>
        Root.TryGetProperty("user_name", out var v) ? v.GetString() ?? "" : "";

    public static string TickTickClientId =>
        Root.TryGetProperty("ticktick", out var t) &&
        t.TryGetProperty("client_id", out var v) ? v.GetString() ?? "" : "";
}

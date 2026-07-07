using System.Text.Json;

namespace MentorOverseer.App.Services;

/// <summary>Read-only view of the shared config.json (same file the Python app owns).</summary>
public static class ConfigService
{
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

    public static string TickTickClientId =>
        Root.TryGetProperty("ticktick", out var t) &&
        t.TryGetProperty("client_id", out var v) ? v.GetString() ?? "" : "";
}

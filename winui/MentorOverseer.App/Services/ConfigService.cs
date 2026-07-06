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

    public static string TickTickClientId =>
        Root.TryGetProperty("ticktick", out var t) &&
        t.TryGetProperty("client_id", out var v) ? v.GetString() ?? "" : "";
}

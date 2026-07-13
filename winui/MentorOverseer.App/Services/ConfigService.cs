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
        // A JsonSerializerOptions built from scratch (rather than copied from
        // .Default) has no TypeInfoResolver and throws on ToJsonString as
        // soon as the tree holds a node added via the generic JsonArray/
        // JsonObject .Add<T>() (always routes through the "customized" path,
        // even for plain ints/strings) — see PlanStore.SetExcludedWeekdays.
        File.WriteAllText(ConfigPath, node.ToJsonString(new JsonSerializerOptions(JsonSerializerOptions.Default)
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

    /// <summary>Configured start of the working day ("working_hours.start"), default 08:00.</summary>
    public static TimeSpan WorkStartTime()
    {
        var start = "08:00";
        if (Root.TryGetProperty("working_hours", out var wh) &&
            wh.TryGetProperty("start", out var v) && v.GetString() is { Length: > 0 } s) start = s;
        return TimeSpan.TryParse(start, out var t) ? t : new TimeSpan(8, 0, 0);
    }

    /// <summary>How many days of detailed diary history to keep before it's
    /// rolled up (Database.DiaryRetentionDays is only the default now —
    /// this makes it user-configurable, 2026-07-09 audit finding #34).</summary>
    public static int DiaryRetentionDays() =>
        Root.TryGetProperty("diary_retention_days", out var v) && v.TryGetInt32(out var n) && n > 0
            ? n : Database.DiaryRetentionDays;

    /// <summary>Empty until the first-launch NameSetupDialog asks and saves it.</summary>
    public static string UserName =>
        Root.TryGetProperty("user_name", out var v) ? v.GetString() ?? "" : "";

    public static string TickTickClientId =>
        Root.TryGetProperty("ticktick", out var t) &&
        t.TryGetProperty("client_id", out var v) ? v.GetString() ?? "" : "";

    /// <summary>
    /// Teaches activity_rules a new keyword for the given category — the
    /// "remember this" half of manually recategorizing a diary entry, so the
    /// live tracker classifies matching windows the same way from then on
    /// (ActivityTracker.Classify does a case-insensitive substring match
    /// against these same lists). Removed from the other two categories
    /// first so one keyword never lives in two lists at once, which would
    /// make classification depend on list-check order instead of intent.
    /// </summary>
    public static void LearnActivityRule(string keyword, string category)
    {
        if (category is not ("on_plan" or "off_plan" or "neutral")) return;
        if (string.IsNullOrWhiteSpace(keyword)) return;

        Mutate(node =>
        {
            if (node["activity_rules"] is not JsonObject rules)
                node["activity_rules"] = rules = new JsonObject();

            JsonArray ArrayFor(string cat)
            {
                if (rules[cat] is JsonArray existing) return existing;
                var fresh = new JsonArray();
                rules[cat] = fresh;
                return fresh;
            }

            foreach (var cat in new[] { "on_plan", "off_plan", "neutral" })
            {
                var arr = ArrayFor(cat);
                for (var i = arr.Count - 1; i >= 0; i--)
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var s) &&
                        string.Equals(s, keyword, StringComparison.OrdinalIgnoreCase))
                        arr.RemoveAt(i);
            }
            ArrayFor(category).Add(keyword);
        });
    }
}

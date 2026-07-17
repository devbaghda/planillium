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
        var node = LoadConfigNode();
        change(node);
        // JsonFileIO.Indented already carries the TypeInfoResolver copy-from-
        // .Default workaround (see its own doc comment) — this just adds the
        // one extra option config.json specifically needs on top.
        JsonFileIO.WriteAllTextAtomic(ConfigPath, node.ToJsonString(new JsonSerializerOptions(JsonFileIO.Indented)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Invalidate();
    }

    /// <summary>Same "corrupted file degrades to defaults, doesn't crash" contract as
    /// PlanStore.LoadActivePlans — a config.json truncated by a crash mid-write from some
    /// other tool used to let a raw JsonException propagate straight out of Mutate instead
    /// (audit finding #14). Falling back to an empty object here means the next save just
    /// rewrites the file cleanly rather than losing the whole app to an unhandled parse
    /// error over one bad file.</summary>
    private static JsonObject LoadConfigNode()
    {
        if (!File.Exists(ConfigPath)) return new JsonObject();
        try
        {
            return JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            Log.Error("ConfigService.Mutate (config.json corrupted, starting fresh)", ex);
            return new JsonObject();
        }
    }

    // Guards _doc: read from both the UI thread and ActivityTracker's
    // background poll timer (e.g. ConfigService.UserName inside CheckAlert),
    // and cleared/rebuilt from the UI thread via Mutate()/Invalidate() —
    // same class of cross-thread race as ActivityTracker's _dayStateLock,
    // just for the config cache instead of tracker state.
    private static readonly object _docLock = new();
    private static JsonDocument? _doc;

    public static JsonElement Root
    {
        get
        {
            lock (_docLock)
            {
                if (_doc is null)
                {
                    var path = Path.Combine(AppPaths.Root, "config.json");
                    try
                    {
                        _doc = File.Exists(path)
                            ? JsonDocument.Parse(File.ReadAllText(path))
                            : JsonDocument.Parse("{}");
                    }
                    catch (JsonException ex)
                    {
                        // This property is read from nearly everywhere (UI thread and
                        // ActivityTracker's background poll alike) — a corrupted config.json
                        // used to throw here unguarded, which would have taken down every
                        // caller in the app instead of just degrading to defaults the way
                        // PlanStore.LoadActivePlans already does for a bad plan file
                        // (audit finding #14).
                        Log.Error("ConfigService.Root (config.json corrupted, using defaults)", ex);
                        _doc = JsonDocument.Parse("{}");
                    }
                }
                return _doc.RootElement.Clone();
            }
        }
    }

    public static void Invalidate()
    {
        lock (_docLock) { _doc?.Dispose(); _doc = null; }
    }

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
        if (category is not (DiaryCategory.OnPlan or DiaryCategory.OffPlan or DiaryCategory.Neutral)) return;
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

            foreach (var cat in new[] { DiaryCategory.OnPlan, DiaryCategory.OffPlan, DiaryCategory.Neutral })
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

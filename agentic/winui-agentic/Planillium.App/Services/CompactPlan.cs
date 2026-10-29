using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Planillium.App.Services;

/// <summary>
/// G10 compact Ask Claude exchange. Claude is asked to reply with a short, minified plan form (marker
/// <c>"cf":1</c>, tasks as arrays, briefing only inside the JSON) so it writes less and answers faster.
/// This class turns such a reply back into the standard plan JSON (a ```json fence with the long field
/// names), so every existing step after it — import, Replace remaining preview, apply — is unchanged and
/// shows exactly the same plan.
///
/// Compact form:
/// <code>
/// {"cf":1,"id":"slug","name":"Title","color":"#3b82f6","days":14,
///  "b":[["leverage 1","leverage 2","leverage 3"],"ignore","time wasters","timeline"],
///  "p":[["Phase name",[[1,"Task title","detail","mentor note","category",60,["tool"]]]]]}
/// </code>
/// A task is the array [day, title, detail, mentor note, category, minutes, tools]; only the first two
/// are required. A reply without the "cf" marker is not touched here (it is the standard form, which the
/// existing checks handle as before: a pasted or file-loaded full plan still imports). A reply WITH the
/// marker that is malformed gets a plain message and nothing is produced, so nothing is half-applied.
/// Plain C#, linked into the test project.
/// </summary>
public static class CompactPlan
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const string TaskShape = "[day, title, detail, mentor note, category, minutes, tools]";

    /// <summary>
    /// Null with <paramref name="normalized"/> set to the standard-form ```json block when the reply is a
    /// compact one; null with the text unchanged when it is not a compact reply (no JSON, bad JSON, or
    /// no "cf" marker); otherwise the plain-language problem (normalized stays the input).
    /// </summary>
    public static string? Normalize(string replyText, out string normalized)
    {
        normalized = replyText;
        var raw = replyText.Trim();
        var fence = Regex.Match(raw, "```(?:json)?\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline);
        var json = fence.Success ? fence.Groups[1].Value
                 : raw.StartsWith('{') ? raw : null;
        if (json is null) return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; } // the standard checks report unparseable JSON

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("cf", out var cf))
                return null;

            if (cf.ValueKind != JsonValueKind.Number || !cf.TryGetInt32(out var version) || version != 1)
                return "This reply uses a compact format version this app doesn't know. Ask Claude again.";

            var plan = new Dictionary<string, object?>();
            foreach (var key in new[] { "id", "name", "color" })
                if (root.TryGetProperty(key, out var v)) plan[key] = v;
            if (root.TryGetProperty("days", out var days)) plan["total_days"] = days;

            if (root.TryGetProperty("b", out var b))
            {
                if (b.ValueKind != JsonValueKind.Array)
                    return "The compact reply's briefing ('b') must be a list of four items. Ask Claude again.";
                var briefing = new Dictionary<string, object?>();
                var names = new[] { "high_leverage", "ignore_completely", "common_time_wasters", "realistic_timeline" };
                var i = 0;
                foreach (var item in b.EnumerateArray())
                {
                    if (i >= names.Length) break;
                    if (item.ValueKind != JsonValueKind.Null) briefing[names[i]] = item;
                    i++;
                }
                plan["briefing"] = briefing;
            }

            if (!root.TryGetProperty("p", out var p) || p.ValueKind != JsonValueKind.Array)
                return "The compact reply has no 'p' list of phases. Ask Claude again.";

            var phases = new List<object>();
            var phaseNo = 0;
            foreach (var phase in p.EnumerateArray())
            {
                phaseNo++;
                if (phase.ValueKind != JsonValueKind.Array || phase.GetArrayLength() != 2 ||
                    phase[1].ValueKind != JsonValueKind.Array)
                    return $"Phase {phaseNo} isn't in the compact form [name, [tasks]]. Ask Claude again.";

                var tasks = new List<object>();
                var taskNo = 0;
                foreach (var t in phase[1].EnumerateArray())
                {
                    taskNo++;
                    if (t.ValueKind != JsonValueKind.Array || t.GetArrayLength() < 2)
                        return $"Task {taskNo} of phase {phaseNo} isn't in the compact form {TaskShape}. Ask Claude again.";
                    tasks.Add(ExpandTask(t));
                }
                phases.Add(new Dictionary<string, object?> { ["name"] = phase[0], ["tasks"] = tasks });
            }
            plan["phases"] = phases;

            normalized = "```json\n" + JsonSerializer.Serialize(plan, Pretty) + "\n```";
            return null;
        }
    }

    private static Dictionary<string, object?> ExpandTask(JsonElement t)
    {
        var task = new Dictionary<string, object?> { ["day"] = t[0], ["task"] = t[1] };
        object? At(int i) => i < t.GetArrayLength() && !IsEmpty(t[i]) ? t[i] : null;

        if (At(2) is { } detail) task["detail"] = detail;
        if (At(3) is { } note) task["mentor_note"] = note;
        if (At(4) is { } category) task["category"] = category;
        if (At(5) is { } minutes) task["duration_min"] = minutes;
        if (t.GetArrayLength() > 6 && t[6].ValueKind == JsonValueKind.Array) task["tools"] = t[6];
        return task;
    }

    private static bool IsEmpty(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ||
        (e.ValueKind == JsonValueKind.String && e.GetString()!.Length == 0);
}

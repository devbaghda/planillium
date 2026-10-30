using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Planillium.App.Services;

/// <summary>
/// G10 (2026-10-29): the compact plan exchange used only by "Ask Claude" (the AiAskPanel round trip). Claude is asked
/// for one line of minified JSON with short keys and per-task arrays instead of a prose briefing plus an indented,
/// fully keyed ```json block, so it writes (and the user waits for) far less. <see cref="Expand"/> turns that reply
/// back into exactly the full plan JSON the dialogs already validate, so everything the user sees (the reply box,
/// the preview, the apply step) is unchanged. The copy-the-prompt-yourself path, "Load from file .json" and the
/// dialogs' own validation are untouched and still use the full form.
/// A reply that is not in the compact form (e.g. the old full form) is refused with a message; nothing is expanded.
/// Plain C#, no WinUI types.
/// </summary>
public static class CompactReply
{
    /// <summary>Appended to the prompt when it is sent to Claude by the Ask button.</summary>
    public const string Instruction = """


        REPLY FORMAT FOR THIS REQUEST (overrides every earlier instruction about a written briefing, the ```json fence and the schema layout):
        Reply with ONE line of minified JSON and nothing else - no prose, no markdown, no code fence. Compact shape:
        {"i":"kebab-id","n":"Plan title","d":14,"b":[["lever 1","lever 2","lever 3"],"ignore","time wasters","timeline"],"p":[{"n":"Phase name","t":[[1,"task title","detail","mentor note","category",60,["tool"]]]}]}
        Keys: i = id, n = name, d = total days, b = briefing (4 items: the list of high-leverage points, then ignore-completely, common time wasters, realistic timeline; leave "b" out if the request above did not ask for a briefing), p = phases (n = phase name, t = its tasks).
        Each task is an array in this fixed order: day, title, detail, mentor note, category, duration in minutes, tools. Use "" for an empty text field, null for an unknown duration and [] for no tools. Contents are the same as before - only the layout is compact.
        For a request that replaces the remaining tasks of an existing plan, send only {"p":[...]} (leave out i, n, d and b).
        """;

    // Built from .Default (G11): a from-scratch options object has no TypeInfoResolver, so
    // ToJsonString threw for every reply holding a generically-added node (the tools array).
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public const string NotCompact =
        "Claude's reply was not in the compact form the app asked for, so nothing was used and nothing was changed.";

    /// <summary>Returns null and the full-form reply (a ```json block, as a hand-pasted reply would look) in
    /// <paramref name="full"/>, or a plain-language problem and an empty <paramref name="full"/>.</summary>
    public static string? Expand(string reply, out string full)
    {
        full = "";
        var raw = reply.Trim();
        var fence = Regex.Match(raw, "^```(?:json)?\\s*(.*?)\\s*```$", RegexOptions.Singleline);
        if (fence.Success) raw = fence.Groups[1].Value.Trim();
        if (!raw.StartsWith('{')) return NotCompact;

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(raw, documentOptions: new JsonDocumentOptions
                { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException) { return NotCompact + " (It could not be read as JSON.)"; }
        if (root is null || root["p"] is not JsonArray phasesIn || phasesIn.Count == 0) return NotCompact;

        var outRoot = new JsonObject();
        if (root["i"] is { } i) { if (Str(i) is not { } s) return Bad("i"); outRoot["id"] = s; }
        if (root["n"] is { } n) { if (Str(n) is not { } s) return Bad("n"); outRoot["name"] = s; }
        if (root["d"] is { } d)
        {
            if (d is not JsonValue dv || !dv.TryGetValue<int>(out var days)) return Bad("d");
            outRoot["total_days"] = days;
        }
        if (root["b"] is { } b)
        {
            if (b is not JsonArray ba || ba.Count != 4 || ba[0] is not JsonArray lev) return Bad("b");
            var levers = new JsonArray();
            foreach (var l in lev) { if (Str(l) is not { } ls) return Bad("b"); levers.Add(ls); }
            if (Str(ba[1]) is not { } ign || Str(ba[2]) is not { } waste || Str(ba[3]) is not { } time) return Bad("b");
            outRoot["briefing"] = new JsonObject
            {
                ["high_leverage"] = levers,
                ["ignore_completely"] = ign,
                ["common_time_wasters"] = waste,
                ["realistic_timeline"] = time,
            };
        }

        var phasesOut = new JsonArray();
        foreach (var pn in phasesIn)
        {
            if (pn is not JsonObject ph || ph["t"] is not JsonArray tasksIn) return Bad("p");
            var tasksOut = new JsonArray();
            foreach (var tn in tasksIn)
            {
                if (tn is not JsonArray t || t.Count < 2) return Bad("t");
                if (t[0] is not JsonValue dayV || !dayV.TryGetValue<int>(out var day)) return Bad("t");
                if (Str(t[1]) is not { } title) return Bad("t");
                var o = new JsonObject { ["day"] = day, ["task"] = title };
                if (!Opt(t, 2, "detail", o) || !Opt(t, 3, "mentor_note", o) || !Opt(t, 4, "category", o)) return Bad("t");
                if (t.Count > 5 && t[5] is { } mv)
                {
                    if (mv is not JsonValue mval || !mval.TryGetValue<int>(out var mins)) return Bad("t");
                    o["duration_min"] = mins;
                }
                if (t.Count > 6 && t[6] is { } tv)
                {
                    if (tv is not JsonArray ta) return Bad("t");
                    var tools = new JsonArray();
                    foreach (var x in ta) { if (Str(x) is not { } xs) return Bad("t"); tools.Add(xs); }
                    o["tools"] = tools;
                }
                tasksOut.Add(o);
            }
            var phase = new JsonObject();
            if (ph["n"] is { } pnm) { if (Str(pnm) is not { } ps) return Bad("p"); phase["name"] = ps; }
            phase["tasks"] = tasksOut;
            phasesOut.Add(phase);
        }
        outRoot["phases"] = phasesOut;

        full = "```json\n" + outRoot.ToJsonString(Pretty) + "\n```";
        return null;
    }

    private static string Bad(string part) => NotCompact + $" (Part \"{part}\" was malformed.)";

    private static string? Str(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Optional text slot at <paramref name="idx"/>: absent / null / "" = omitted; a non-string is a fault.</summary>
    private static bool Opt(JsonArray t, int idx, string key, JsonObject o)
    {
        if (t.Count <= idx || t[idx] is null) return true;
        if (Str(t[idx]) is not { } s) return false;
        if (s.Length > 0) o[key] = s;
        return true;
    }
}

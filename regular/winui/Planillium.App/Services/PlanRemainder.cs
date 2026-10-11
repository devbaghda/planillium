using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Planillium.App.Models;

namespace Planillium.App.Services;

/// <summary>
/// "Replace remaining tasks" (2026-10-09 request): keep every task already ticked complete, throw
/// away everything else in the plan (overdue, today, future) and splice in a fresh list pasted
/// from Claude. Plain C# on purpose — no WinUI types — so the parsing/validation is unit-testable.
/// The file/DB write itself is <see cref="PlanStore.ReplaceRemainingTasks"/>.
/// </summary>
public static class PlanRemainder
{
    /// <summary>What applying a pasted reply would do — computed without touching anything.</summary>
    public sealed class Change
    {
        public required int DoneCount { get; init; }
        /// <summary>Titles of the unfinished tasks that will be removed.</summary>
        public required List<string> RemovedTexts { get; init; }
        /// <summary>Titles of the done tasks that stay (the write keeps exactly these).</summary>
        public required HashSet<string> KeptTexts { get; init; }
        /// <summary>Plan day the first new task occupies: after the last done task and not before today.</summary>
        public required int StartDay { get; init; }
        public required int AddedCount { get; init; }
        /// <summary>Highest plan day among the new tasks, after shifting.</summary>
        public required int LastNewDay { get; init; }
        /// <summary>Ready-to-insert phase objects, task days already shifted to start at StartDay.</summary>
        public required List<JsonObject> NewPhases { get; init; }
    }

    public static bool HasUnfinished(List<AssignedTask> tasks) => tasks.Any(t => !t.Completed);

    /// <summary>The first plan day a replacement task may take: right after the last done task (day 1 if
    /// none is done). User decision 2026-10-09, reaffirmed after the objection that a plan that has
    /// fallen behind then lands its new list in the past as overdue — deliberately NOT clamped to today.</summary>
    public static int StartDayFor(Plan plan, List<AssignedTask> tasks) =>
        tasks.Where(t => t.Completed).Select(t => t.AssignedDay).DefaultIfEmpty(0).Max() + 1;

    /// <summary>The prompt the user copies to claude.ai: what's done (kept), what's currently left,
    /// what they want changed, and the exact reply shape the app can import.</summary>
    public static string BuildPrompt(Plan plan, List<AssignedTask> tasks, string whatToChange)
    {
        var done = tasks.Where(t => t.Completed).OrderBy(t => t.AssignedDay).ToList();
        var left = tasks.Where(t => !t.Completed).OrderBy(t => t.AssignedDay).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"I'm following a plan called \"{plan.Name}\". Part of it is already done and must stay as it is. " +
                      "I want you to rewrite ONLY the remaining part.");
        sb.AppendLine();
        sb.AppendLine("What I want changed:");
        sb.AppendLine(whatToChange.Trim().Length > 0 ? whatToChange.Trim() : "(nothing specific — improve the remaining part as you see fit)");
        sb.AppendLine();
        sb.AppendLine("ALREADY DONE (do not repeat, do not reuse these titles):");
        if (done.Count == 0) sb.AppendLine("- (nothing yet)");
        foreach (var t in done) sb.AppendLine($"- Day {t.AssignedDay}: {t.Task.Text}");
        sb.AppendLine();
        sb.AppendLine("CURRENT REMAINING TASKS (the part to be replaced — keep what is still good, drop or change the rest):");
        foreach (var t in left) sb.AppendLine($"- Day {t.AssignedDay}: {t.Task.Text}");
        sb.AppendLine();
        sb.AppendLine("Reply with ONE ```json block and nothing the app needs outside it, in exactly this shape:");
        sb.AppendLine("{ \"phases\": [ { \"phase\": 1, \"name\": \"Phase name\", \"tasks\": [");
        sb.AppendLine("  { \"day\": 1, \"task\": \"Short task title\", \"detail\": \"The concrete how-to\", " +
                      "\"mentor_note\": \"Why this matters\", \"category\": \"short-label\", \"duration_min\": 60, " +
                      "\"tools\": [\"App or site this task needs\"] } ] } ] }");
        sb.AppendLine();
        sb.AppendLine("Rules: number \"day\" from 1 for the first remaining task (the app shifts it to continue after " +
                      "my done tasks). Every \"task\" title must be unique and must not match any done title above. " +
                      "Do not include the done tasks.");
        return sb.ToString();
    }

    /// <summary>Validates a pasted reply against the plan's current state. Returns an error message
    /// (plain language), or null on success with <paramref name="change"/> filled in.</summary>
    public static string? Prepare(Plan plan, List<AssignedTask> tasks, string replyText, out Change? change)
    {
        change = null;
        var done = tasks.Where(t => t.Completed).ToList();
        var removed = tasks.Where(t => !t.Completed).Select(t => t.Task.Text).ToList();
        if (removed.Count == 0)
            return "Every task in this plan is already done — there is nothing to replace.";
        if (replyText.Trim().Length == 0)
            return "Paste Claude's reply first.";

        var raw = replyText.Trim();
        var fence = Regex.Match(raw, "```(?:json)?\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline);
        var json = fence.Success ? fence.Groups[1].Value : raw.StartsWith('{') ? raw : null;
        if (json == null)
            return "Couldn't find valid JSON — paste the whole reply including the ```json block.";

        JsonObject? root;
        try { root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject; }
        catch (JsonException)
        {
            return "That JSON doesn't parse — check it's the complete reply including the ```json fence.";
        }
        if (root?["phases"] is not JsonArray phases || phases.Count == 0)
            return "The reply is missing a non-empty 'phases' list.";

        // Exact set for the file write (task identity is the exact title everywhere else in the app);
        // case-insensitive set only for the collision check, so a near-duplicate is still refused.
        var keptTexts = new HashSet<string>(done.Select(t => t.Task.Text));
        var keptLoose = new HashSet<string>(keptTexts, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startDay = StartDayFor(plan, tasks);
        var newPhases = new List<JsonObject>();
        var added = 0;
        var lastDay = startDay - 1;

        foreach (var phaseNode in phases)
        {
            if (phaseNode is not JsonObject phase || phase["tasks"] is not JsonArray phaseTasks)
                return "Every phase in the reply needs a 'tasks' list.";
            var outTasks = new JsonArray();
            foreach (var taskNode in phaseTasks)
            {
                if (taskNode is not JsonObject t) return "Every task in the reply must be an object.";
                var title = (t["task"] as JsonValue)?.TryGetValue<string>(out var s) == true ? s.Trim() : "";
                if (title.Length == 0) return "A task in the reply has no 'task' title.";
                if (t["day"] is not JsonValue dv || !dv.TryGetValue<int>(out var day) || day < 1)
                    return $"Task \"{title}\" needs a 'day' that is a whole number 1 or higher.";
                if (keptLoose.Contains(title))
                    return $"Task \"{title}\" has the same title as a task you've already done — give it a different title.";
                if (!seen.Add(title))
                    return $"Task \"{title}\" appears twice in the reply — titles must be unique.";

                var copy = (JsonObject)t.DeepClone();
                copy["task"] = title;
                var shifted = day + startDay - 1;
                copy["day"] = shifted;
                lastDay = Math.Max(lastDay, shifted);
                outTasks.Add(copy);
                added++;
            }
            if (outTasks.Count == 0) continue;
            var name = (phase["name"] as JsonValue)?.TryGetValue<string>(out var n) == true && n.Trim().Length > 0
                ? n.Trim() : "Revised plan";
            newPhases.Add(new JsonObject { ["phase"] = 0, ["name"] = name, ["tasks"] = outTasks });
        }
        if (added == 0) return "The reply has no tasks in it.";

        change = new Change
        {
            DoneCount = done.Count,
            RemovedTexts = removed,
            KeptTexts = keptTexts,
            StartDay = startDay,
            AddedCount = added,
            LastNewDay = lastDay,
            NewPhases = newPhases,
        };
        return null;
    }
}

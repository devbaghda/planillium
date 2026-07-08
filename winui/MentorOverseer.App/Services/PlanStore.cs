using System.Text.Json;
using System.Text.Json.Nodes;
using MentorOverseer.App.Models;

namespace MentorOverseer.App.Services;

public static class PlanStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<Plan> LoadActivePlans()
    {
        var plans = new List<Plan>();
        if (!Directory.Exists(AppPaths.ActivePlansDir)) return plans;
        foreach (var file in Directory.GetFiles(AppPaths.ActivePlansDir, "*.json").OrderBy(f => f))
        {
            try
            {
                var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(file), Options);
                if (plan is { Id.Length: > 0 }) plans.Add(plan);
            }
            catch (JsonException)
            {
                // A malformed plan file shouldn't take the whole app down;
                // the Plans page will surface it in a later phase.
            }
        }
        return plans;
    }

    /// <summary>
    /// All tasks of a plan with override-adjusted assigned days, completion
    /// state, and overdue flags — same rules as the Python app: assigned day
    /// comes from task_overrides (fallback: the task's own day); overdue =
    /// incomplete and assigned before the current plan day.
    /// </summary>
    public static List<AssignedTask> TasksFor(Plan plan, Database db,
        Dictionary<(string, int, string), bool> completions)
    {
        var overrides = db.LoadOverrides(plan.Id);
        var result = new List<AssignedTask>();
        var planDay = plan.PlanDay;

        foreach (var phase in plan.Phases)
        {
            foreach (var task in phase.Tasks)
            {
                var assigned = overrides.TryGetValue(task.Text, out var o) ? o : task.Day;
                var done = completions.TryGetValue((plan.Id, assigned, task.Text), out var c) && c;
                result.Add(new AssignedTask
                {
                    Task = task,
                    OriginalDay = task.Day,
                    AssignedDay = assigned,
                    Completed = done,
                    Overdue = !done && assigned < planDay,
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Surgical patch of one plan file's excluded_weekdays — parses as a raw
    /// JsonNode rather than round-tripping through the Plan model, so fields
    /// the C# model doesn't know about (hand-authored plans carry extras per
    /// phase like days_range/cost_eur/effort/key_win) survive untouched. A
    /// full deserialize-then-reserialize would silently drop them.
    /// </summary>
    public static void SetExcludedWeekdays(string planId, List<int> weekdays)
    {
        var path = Path.Combine(AppPaths.ActivePlansDir, $"{planId}.json");
        var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidOperationException($"Plan file for '{planId}' isn't a JSON object.");
        var arr = new JsonArray();
        foreach (var d in weekdays) arr.Add(d);
        node["excluded_weekdays"] = arr;
        // A bare `new JsonSerializerOptions { WriteIndented = true }` has no
        // TypeInfoResolver and throws on ToJsonString in .NET 8 — copy from
        // the pre-configured Default instance instead.
        File.WriteAllText(path, node.ToJsonString(
            new JsonSerializerOptions(JsonSerializerOptions.Default) { WriteIndented = true }));
    }

    /// <summary>
    /// Appends a manually-added step to a plan file's last phase — same
    /// surgical JsonNode patch as SetExcludedWeekdays, so hand-authored
    /// extras elsewhere in the file survive untouched. Multiple tasks
    /// sharing a day is already normal (most days already have more than
    /// one), so this never needs to shift anything.
    /// </summary>
    public static void AddTask(string planId, int day, string text, string? detail,
        string? category, int? durationMin)
    {
        var path = Path.Combine(AppPaths.ActivePlansDir, $"{planId}.json");
        var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidOperationException($"Plan file for '{planId}' isn't a JSON object.");
        var phases = node["phases"] as JsonArray;
        if (phases is null || phases.Count == 0)
            throw new InvalidOperationException($"Plan '{planId}' has no phase to add a task to.");
        var lastPhase = phases[^1] as JsonObject
            ?? throw new InvalidOperationException($"Plan '{planId}''s last phase isn't a JSON object.");
        if (lastPhase["tasks"] is not JsonArray tasks)
            lastPhase["tasks"] = tasks = new JsonArray();

        var task = new JsonObject { ["day"] = day, ["task"] = text };
        if (detail != null) task["detail"] = detail;
        if (category != null) task["category"] = category;
        if (durationMin is int d) task["duration_min"] = d;
        tasks.Add(task);

        File.WriteAllText(path, node.ToJsonString(
            new JsonSerializerOptions(JsonSerializerOptions.Default) { WriteIndented = true }));
    }
}

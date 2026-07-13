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

    public static List<Plan> LoadActivePlans() => LoadActivePlans(out _);

    /// <summary>
    /// Overload used by the Plans page: <paramref name="failedFiles"/> lists
    /// the file names (not full paths) of any plan JSON that failed to
    /// parse, so the page can tell the user which plan silently vanished
    /// and why, instead of it just disappearing with no trace anywhere
    /// (2026-07-09 audit finding #5 — a malformed file used to be dropped
    /// with no log entry and a comment promising UI surfacing that was
    /// never built).
    /// </summary>
    public static List<Plan> LoadActivePlans(out List<string> failedFiles)
    {
        var plans = new List<Plan>();
        failedFiles = new List<string>();
        if (!Directory.Exists(AppPaths.ActivePlansDir)) return plans;
        foreach (var file in Directory.GetFiles(AppPaths.ActivePlansDir, "*.json").OrderBy(f => f))
        {
            try
            {
                var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(file), Options);
                if (plan is { Id.Length: > 0 }) plans.Add(plan);
            }
            catch (JsonException ex)
            {
                // A malformed plan file shouldn't take the whole app down —
                // but it must leave a trace, both for diagnosis (the log)
                // and for the user (failedFiles, surfaced as an InfoBar).
                Log.Error($"PlanStore.LoadActivePlans({Path.GetFileName(file)})", ex);
                failedFiles.Add(Path.GetFileName(file));
            }
        }
        return plans;
    }

    /// <summary>
    /// True when <paramref name="d"/> is a recurring rest day for EVERY
    /// active plan — all of them treat that weekday as an excluded
    /// (non-advancing) day. Used only to pause activity tracking itself on
    /// the user's days off; ignores manually-marked single-day exclusions on
    /// purpose (unlike ScoreService.IsScoringExemptFor, which is per-plan and
    /// does consider them) — the two rules answer different questions and
    /// are deliberately not unified. Returns false when no plans are loaded,
    /// so tracking never quietly stops just because a plan file failed to
    /// load or none exists yet.
    /// </summary>
    public static bool AllPlansExclude(DateOnly d)
    {
        var plans = LoadActivePlans();
        return plans.Count > 0 && plans.All(p => p.IsExcluded(d));
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
    /// <summary>
    /// planId is always attacker-free in practice today (plan files only
    /// ever come from the user's own filesystem, per plan.Id in Plan JSON
    /// they already have write access to), but this file-path build was one
    /// unvalidated string away from a path-traversal shape — cheap defense-
    /// in-depth before any future feature imports/shares a plan from
    /// somewhere else (2026-07-09 audit finding #25). Matches the
    /// kebab-case-slug format the plan template already mandates.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ValidPlanId = new(@"^[a-z0-9-]+$");

    private static string PlanFilePath(string planId)
    {
        if (!ValidPlanId.IsMatch(planId))
            throw new ArgumentException($"'{planId}' isn't a valid plan id (expected kebab-case-slug).");
        return Path.Combine(AppPaths.ActivePlansDir, $"{planId}.json");
    }

    public static void SetExcludedWeekdays(string planId, List<int> weekdays)
    {
        var path = PlanFilePath(planId);
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
        var path = PlanFilePath(planId);
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

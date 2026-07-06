using System.Text.Json;
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
}

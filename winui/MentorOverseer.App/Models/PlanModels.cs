using System.Text.Json.Serialization;

namespace MentorOverseer.App.Models;

// Mirrors the plan JSON schema used by the Python app (plans/active/*.json).
// This format is the compatibility contract — do not rename fields.

public class Plan
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = "";
    [JsonPropertyName("total_days")] public int? TotalDays { get; set; }
    [JsonPropertyName("phases")] public List<Phase> Phases { get; set; } = new();
    [JsonPropertyName("briefing")] public PlanBriefing? Briefing { get; set; }

    public DateOnly StartDateParsed =>
        DateOnly.TryParse(StartDate, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

    public int PlanDay =>
        DateOnly.FromDateTime(DateTime.Today).DayNumber - StartDateParsed.DayNumber + 1;

    public int TotalDaysComputed
    {
        get
        {
            if (TotalDays is int t && t > 0) return t;
            var max = 0;
            foreach (var ph in Phases)
                foreach (var task in ph.Tasks)
                    if (task.Day > max) max = task.Day;
            return max;
        }
    }
}

/// <summary>The 4-point strategic briefing the plan-generation templates ask
/// Claude for, saved with the plan (main.py's _show_plan_briefing_dialog
/// shape — see PlanTemplates.cs for the exact field meanings).</summary>
public class PlanBriefing
{
    [JsonPropertyName("high_leverage")] public List<string> HighLeverage { get; set; } = new();
    [JsonPropertyName("ignore_completely")] public string? IgnoreCompletely { get; set; }
    [JsonPropertyName("common_time_wasters")] public string? CommonTimeWasters { get; set; }
    [JsonPropertyName("realistic_timeline")] public string? RealisticTimeline { get; set; }
}

public class Phase
{
    [JsonPropertyName("phase")] public int Number { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("tasks")] public List<PlanTask> Tasks { get; set; } = new();
}

public class PlanTask
{
    [JsonPropertyName("day")] public int Day { get; set; }
    [JsonPropertyName("task")] public string Text { get; set; } = "";
    [JsonPropertyName("detail")] public string? Detail { get; set; }
    [JsonPropertyName("mentor_note")] public string? MentorNote { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("duration_min")] public int? DurationMin { get; set; }
}

/// <summary>A task as it appears in a day list: original day + assigned day after overrides.</summary>
public class AssignedTask
{
    public required PlanTask Task { get; init; }
    public required int OriginalDay { get; init; }
    public required int AssignedDay { get; init; }
    public bool Completed { get; set; }
    public bool Overdue { get; set; }
}

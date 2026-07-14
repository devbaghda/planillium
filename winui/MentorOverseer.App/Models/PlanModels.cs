using System.Linq;
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

    /// <summary>Days of the week this plan never schedules anything on —
    /// .NET DayOfWeek values (0=Sunday..6=Saturday). Per-plan, editable any
    /// time from the Plans page. When today falls on one of these, the plan
    /// day counter simply doesn't advance — day N's tasks land on the next
    /// non-excluded date instead, cascading if that's excluded too.</summary>
    [JsonPropertyName("excluded_weekdays")] public List<int> ExcludedWeekdays { get; set; } = new();

    public DateOnly StartDateParsed =>
        DateOnly.TryParse(StartDate, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

    public bool IsExcluded(DateOnly d) => ExcludedWeekdays.Contains((int)d.DayOfWeek);

    /// <summary>Calendar date day N's tasks actually land on — walks forward
    /// from the start date counting only non-excluded days. With no
    /// exclusions this is exactly StartDateParsed.AddDays(planDay - 1).</summary>
    public DateOnly DateForPlanDay(int planDay)
    {
        if (ExcludedWeekdays.Count == 0) return StartDateParsed.AddDays(planDay - 1);
        var date = StartDateParsed;
        var count = 0;
        // planDay is always small relative to any realistic plan length, and
        // exclusions are at most a handful of weekdays, so this terminates
        // quickly — no need for a closed-form skip-ahead calculation.
        while (true)
        {
            if (!IsExcluded(date))
            {
                count++;
                if (count == planDay) return date;
            }
            date = date.AddDays(1);
        }
    }

    /// <summary>Inverse of DateForPlanDay — how many non-excluded days have
    /// elapsed from the start date through (and including) target. If
    /// target itself is excluded, the count doesn't advance for it, so the
    /// result is the same as the last non-excluded day before it (nothing
    /// new becomes due on an excluded day; work picks back up where it left
    /// off on the next valid day).</summary>
    public int PlanDayForDate(DateOnly target)
    {
        if (ExcludedWeekdays.Count == 0)
            return target.DayNumber - StartDateParsed.DayNumber + 1;
        if (target < StartDateParsed)
            return target.DayNumber - StartDateParsed.DayNumber + 1;
        var count = 0;
        for (var date = StartDateParsed; date <= target; date = date.AddDays(1))
            if (!IsExcluded(date)) count++;
        return count;
    }

    public int PlanDay => PlanDayForDate(DateOnly.FromDateTime(DateTime.Today));

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

    /// <summary>
    /// Days late (positive) or ahead (negative) of this plan's originally-due
    /// finish date — the single source of truth both the Plans page card and
    /// the sidebar status line read from, so they can't drift apart from each
    /// other the way two independently-typed copies of this formula could.
    /// The originally-due date comes from un-overridden <see cref="TotalDaysComputed"/>,
    /// which never moves; the currently-due date follows wherever the last
    /// task's <see cref="AssignedTask.AssignedDay"/> has ended up after any
    /// reschedules/day-offs/early finishes.
    /// </summary>
    public int DriftDays(List<AssignedTask> tasks)
    {
        var originalEndDate = DateForPlanDay(TotalDaysComputed);
        var currentEndDate = DateForPlanDay(
            tasks.Count > 0 ? tasks.Max(t => t.AssignedDay) : TotalDaysComputed);
        return currentEndDate.DayNumber - originalEndDate.DayNumber;
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

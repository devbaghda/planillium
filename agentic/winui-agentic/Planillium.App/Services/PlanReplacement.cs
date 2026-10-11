using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Planillium.App.Models;

namespace Planillium.App.Services;

/// <summary>
/// "Replace remaining tasks…" on an active plan (spec replace-remaining-tasks). Keeps every
/// ticked task exactly where it is, removes every other task from the plan file, and appends a
/// new list pasted from Claude, shifted to start on the day after the last ticked task (day 1 when
/// none is ticked). Not clamped to today: a far-behind plan's new tasks can show as overdue.
/// Pure logic only, no WinUI types, so the test project can link this file.
///
/// Order of writes on Apply: the plan file first (atomic), then the database clean-up. A failed
/// file write therefore changes nothing; a failed clean-up after a successful write is reported
/// back to the caller with <see cref="ApplyResult.CleanupFailed"/>.
/// </summary>
public static class PlanReplacement
{
    /// <summary>A task the user has ticked complete, with its override-adjusted day.</summary>
    public sealed record DoneTask(int AssignedDay, string Title);

    /// <summary>
    /// The plan as it stands right now. Recomputed when each step opens and again at confirm,
    /// because the start day depends on the ticked tasks as they are at that moment.
    /// </summary>
    public sealed record Situation(
        Plan Plan,
        IReadOnlyList<DoneTask> Done,
        int RemovedCount,
        int LastDoneDay,
        int StartDay,
        DateOnly StartDate,
        DateOnly OldFinish);

    /// <summary>A validated paste. <see cref="PhasesJson"/> is the raw text of the pasted phase
    /// objects (phases that are not JSON objects are dropped); Apply re-parses it so each write
    /// gets fresh nodes.</summary>
    public sealed record Replacement(string PhasesJson, int TaskCount, int MinDay, int MaxDay);

    public enum ApplyOutcome
    {
        /// <summary>The plan file was rewritten (and the clean-up attempted).</summary>
        Applied,
        /// <summary>The start day recomputed now no longer matches the one the caller showed; nothing written.</summary>
        StartChanged,
        /// <summary>Every task is already done; nothing written.</summary>
        NothingToReplace,
    }

    /// <param name="CleanupFailed">True when the plan file was written but the database clean-up
    /// threw. Its exception is logged; old overrides for removed titles may remain.</param>
    public sealed record ApplyResult(ApplyOutcome Outcome, Situation Situation, bool CleanupFailed);

    /// <summary>The preview figures, all derived from the same Situation and Replacement that
    /// Apply will use.</summary>
    public sealed record Preview(
        int Removed,
        int Added,
        int DoneCount,
        int StartDay,
        DateOnly StartDate,
        DateOnly OldFinish,
        DateOnly NewFinish,
        int NewTotalDays);

    /// <summary>Works out the done/not-done split and the start day for an active plan. Reads
    /// the plan file and the database; throws if the plan isn't active.</summary>
    public static Situation Assess(string planId, Database db)
    {
        var plan = PlanStore.LoadActivePlans().FirstOrDefault(p => p.Id == planId)
            ?? throw new InvalidOperationException($"Plan '{planId}' isn't an active plan.");
        var tasks = PlanStore.TasksFor(plan, db, db.LoadCompletions());

        var done = tasks.Where(t => t.Completed)
            .Select(t => new DoneTask(t.AssignedDay, t.Task.Text))
            .OrderBy(d => d.AssignedDay)
            .ThenBy(d => d.Title, StringComparer.Ordinal)
            .ToList();
        var removed = tasks.Count(t => !t.Completed);
        var lastDone = done.Count == 0 ? 0 : done.Max(d => d.AssignedDay);
        var start = lastDone + 1;

        return new Situation(
            plan,
            done,
            removed,
            lastDone,
            start,
            plan.DateForPlanDay(start),
            plan.CurrentEndDate(tasks));
    }

    /// <summary>
    /// Parses a pasted reply (a ```json fence, or a bare {...} object) into a Replacement. Returns
    /// the first problem found, in the order the dialog's error table lists them, or null when
    /// the paste is valid. Only "phases" is read; id, name, briefing and color are ignored.
    /// </summary>
    public static string? ParseReplacement(string replyText, IReadOnlyCollection<string> doneTitles,
        out Replacement? replacement)
    {
        replacement = null;
        if (replyText.Trim().Length == 0)
            return "Paste Claude's reply first.";

        var raw = replyText.Trim();
        var fence = Regex.Match(raw, "```(?:json)?\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline);
        var json = fence.Success ? fence.Groups[1].Value
                 : raw.StartsWith('{') ? raw : null;
        if (json is null)
            return "Couldn't find valid JSON — paste the whole reply including the ```json block.";

        const string noTasks = "The reply has no tasks. It needs a 'phases' list with at least one task.";
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            Log.Warn("PlanReplacement.ParseReplacement", ex.Message);
            return "That JSON doesn't parse — check it's the complete reply including the ```json fence.";
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return "Couldn't find valid JSON — paste the whole reply including the ```json block.";
            if (!root.TryGetProperty("phases", out var phases) || phases.ValueKind != JsonValueKind.Array)
                return noTasks;

            var phaseObjects = new List<string>();
            var tasks = new List<JsonElement>();
            foreach (var phase in phases.EnumerateArray())
            {
                if (phase.ValueKind != JsonValueKind.Object) continue;
                phaseObjects.Add(phase.GetRawText());
                if (phase.TryGetProperty("tasks", out var phaseTasks) && phaseTasks.ValueKind == JsonValueKind.Array)
                    foreach (var task in phaseTasks.EnumerateArray()) tasks.Add(task);
            }
            if (tasks.Count == 0) return noTasks;

            // Checked pass by pass, so the first problem reported follows the table's order
            // across the whole reply, not the order of tasks within it.
            var titles = new List<string?>(tasks.Count);
            foreach (var task in tasks) titles.Add(TitleOf(task));
            for (var k = 0; k < titles.Count; k++)
                if (titles[k] is null) return $"Task {k + 1} has no 'task' title.";
            var named = titles.Select(t => t!).ToList();

            var days = new List<int>(tasks.Count);
            for (var k = 0; k < tasks.Count; k++)
            {
                var day = DayOf(tasks[k]);
                if (day is null) return $"'{named[k]}' needs a whole-number 'day' of 1 or more.";
                days.Add(day.Value);
            }

            var done = doneTitles.Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var title in named)
                if (done.Contains(title))
                    return $"'{title}' is already a done task in this plan. Give the new task a different title.";

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var title in named)
                if (!seen.Add(title))
                    return $"'{title}' appears twice in the reply. Titles must be unique.";

            replacement = new Replacement(
                PhasesJson: "[" + string.Join(",", phaseObjects) + "]",
                TaskCount: tasks.Count,
                MinDay: days.Min(),
                MaxDay: days.Max());
            return null;
        }
    }

    /// <summary>The figures the confirmation step shows. The new total is the last new task's
    /// day, which is also the value Apply writes to total_days.</summary>
    public static Preview BuildPreview(Situation situation, Replacement replacement)
    {
        var newTotal = NewTotalDays(situation, replacement);
        return new Preview(
            Removed: situation.RemovedCount,
            Added: replacement.TaskCount,
            DoneCount: situation.Done.Count,
            StartDay: situation.StartDay,
            StartDate: situation.StartDate,
            OldFinish: situation.OldFinish,
            NewFinish: situation.Plan.DateForPlanDay(newTotal),
            NewTotalDays: newTotal);
    }

    /// <summary>
    /// Rewrites the plan file (surgical JSON patch, so briefing and unmodelled fields survive),
    /// then clears the stale database rows for the removed titles. Returns
    /// <see cref="ApplyOutcome.StartChanged"/> without writing if the start day recomputed now
    /// differs from <paramref name="expectedStartDay"/>. A file write failure throws and leaves
    /// the plan file unchanged.
    /// </summary>
    public static ApplyResult Apply(string planId, Replacement replacement, int expectedStartDay, Database db)
    {
        var situation = Assess(planId, db);
        if (situation.RemovedCount == 0)
            return new ApplyResult(ApplyOutcome.NothingToReplace, situation, false);
        if (situation.StartDay != expectedStartDay)
            return new ApplyResult(ApplyOutcome.StartChanged, situation, false);

        var node = PlanStore.ReadActivePlanObject(planId);
        var phases = node["phases"] as JsonArray
            ?? throw new InvalidOperationException($"Plan '{planId}' has no phases array.");
        var completions = db.LoadCompletions();
        var overrides = db.LoadOverrides(planId);

        var removedTitles = new HashSet<string>(StringComparer.Ordinal);
        var keptTitles = new HashSet<string>(StringComparer.Ordinal);
        for (var p = phases.Count - 1; p >= 0; p--)
        {
            if (phases[p] is not JsonObject phase || phase["tasks"] is not JsonArray tasks) continue;
            var hadTasks = tasks.Count > 0;
            for (var i = tasks.Count - 1; i >= 0; i--)
            {
                if (tasks[i] is not JsonObject task) continue;
                var text = task["task"]?.GetValue<string>() ?? "";
                var day = task["day"]?.GetValue<int>() ?? 0;
                if (IsDone(planId, text, day, overrides, completions))
                    keptTitles.Add(text);
                else
                {
                    removedTitles.Add(text);
                    tasks.RemoveAt(i);
                }
            }
            if (hadTasks && tasks.Count == 0) phases.RemoveAt(p);
        }

        // Shifts pasted days onto the calendar: relative spacing kept, first pasted day lands on
        // the start day. Each pasted phase is parsed fresh and appended verbatim, only "day" changes.
        var start = situation.StartDay;
        var pasted = JsonNode.Parse(replacement.PhasesJson) as JsonArray
            ?? throw new InvalidOperationException("Pasted phases didn't parse as an array.");
        while (pasted.Count > 0)
        {
            var phase = pasted[0]!;
            pasted.RemoveAt(0);
            if (phase["tasks"] is JsonArray newTasks)
                foreach (var t in newTasks)
                    if (t is JsonObject to && to["day"] is JsonNode dayNode)
                        to["day"] = start + (dayNode.GetValue<int>() - replacement.MinDay);
            phases.Add(phase);
        }

        node["total_days"] = NewTotalDays(situation, replacement);
        PlanStore.WriteActivePlanObject(planId, node);

        // Title shared by a kept (ticked) task and a removed one: the override row belongs to the
        // kept task too (overrides are keyed by plan and title only), so it stays.
        var toClean = removedTitles.Where(t => !keptTitles.Contains(t)).ToList();
        var cleanupFailed = false;
        try
        {
            db.DeleteRemovedTaskRows(planId, toClean);
        }
        catch (Exception ex)
        {
            Log.Error("PlanReplacement.Apply (database clean-up)", ex);
            cleanupFailed = true;
        }
        return new ApplyResult(ApplyOutcome.Applied, situation, cleanupFailed);
    }

    /// <summary>Plural-aware preview headline, e.g. "6 unfinished tasks removed, 5 new tasks added".</summary>
    public static string Headline(int removed, int added) =>
        $"{Plural(removed, "unfinished task")} removed, {Plural(added, "new task")} added";

    /// <summary>"no done tasks" / "1 done task" / "4 done tasks".</summary>
    public static string KeptText(int doneCount) => doneCount switch
    {
        0 => "no done tasks",
        1 => "1 done task",
        _ => $"{doneCount} done tasks",
    };

    /// <summary>Words only, never a bare sign: "3 days later", "1 day earlier", "same day".</summary>
    public static string DescribeFinishChange(DateOnly oldFinish, DateOnly newFinish)
    {
        var delta = newFinish.DayNumber - oldFinish.DayNumber;
        if (delta == 0) return "same day";
        var days = Math.Abs(delta);
        var amount = days == 1 ? "1 day" : $"{days} days";
        return delta > 0 ? $"{amount} later" : $"{amount} earlier";
    }

    /// <summary>
    /// False when every task is already ticked done, so there is nothing to replace. The dialog
    /// checks this before showing step 1 and refuses; Apply checks it again before writing.
    /// </summary>
    public static bool HasSomethingToReplace(Situation situation) => situation.RemovedCount > 0;

    private static int NewTotalDays(Situation situation, Replacement replacement) =>
        situation.StartDay + (replacement.MaxDay - replacement.MinDay);

    // Same rule as PlanStore.TasksFor: the override day wins over the task's own day, and a row
    // counts as done only when it is ticked on that assigned day.
    private static bool IsDone(string planId, string text, int day,
        Dictionary<string, int> overrides, Dictionary<(string PlanId, int Day, string Text), bool> completions)
    {
        var assigned = overrides.TryGetValue(text, out var o) ? o : day;
        return completions.TryGetValue((planId, assigned, text), out var done) && done;
    }

    private static string Plural(int n, string singular) =>
        n == 1 ? $"1 {singular}" : $"{n} {singular}s";

    private static string? TitleOf(JsonElement task)
    {
        if (task.ValueKind != JsonValueKind.Object) return null;
        if (!task.TryGetProperty("task", out var title) || title.ValueKind != JsonValueKind.String) return null;
        var trimmed = title.GetString()?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static int? DayOf(JsonElement task)
    {
        if (task.ValueKind != JsonValueKind.Object) return null;
        if (!task.TryGetProperty("day", out var day) || day.ValueKind != JsonValueKind.Number) return null;
        return day.TryGetInt32(out var d) && d >= 1 ? d : null;
    }
}

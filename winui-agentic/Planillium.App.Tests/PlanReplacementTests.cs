using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// "Replace remaining tasks…" (spec replace-remaining-tasks): PlanReplacement's paste parsing,
/// start-day rule, preview wording, and the plan-file/database apply. Every test uses a unique
/// plan id under the shared scratch MENTOR_ROOT (TestRootFixture), never the real data folder.
/// </summary>
[Collection("TestRoot")]
public sealed class PlanReplacementTests
{
    private static string NewId() => "replace-" + Guid.NewGuid().ToString("N");

    private static string TaskJson(int day, string title) =>
        $"{{ \"day\": {day}, \"task\": \"{title}\", \"detail\": \"d\", \"mentor_note\": \"m\", \"tools\": [] }}";

    /// <summary>Writes the fixture plan: ten tasks on days 1-10 ("Task 1".."Task 10") in one phase
    /// with an unmodelled phase field, plus a briefing and excluded_weekdays that must survive.</summary>
    private static string WritePlan(string id, string startIso, bool withTotalDays = true)
    {
        var tasks = string.Join(",\n", Enumerable.Range(1, 10).Select(d => TaskJson(d, $"Task {d}")));
        var totalLine = withTotalDays ? "\"total_days\": 10," : "";
        var json = $$"""
        {
          "id": "{{id}}",
          "name": "Replace Test",
          "color": "#3b82f6",
          "start_date": "{{startIso}}",
          {{totalLine}}
          "briefing": { "high_leverage": ["one"], "realistic_timeline": "keep me" },
          "excluded_weekdays": [],
          "phases": [
            { "phase": 1, "name": "Phase A", "cost_eur": 5, "tasks": [
        {{tasks}}
            ] }
          ]
        }
        """;
        Directory.CreateDirectory(AppPaths.ActivePlansDir);
        var path = Path.Combine(AppPaths.ActivePlansDir, $"{id}.json");
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>A reply in the ```json fence shape Claude produces, with one phase "New".</summary>
    private static string Reply(params (int Day, string Title)[] tasks) =>
        "Here you go:\n```json\n{ \"name\": \"Pasted\", \"phases\": [ { \"name\": \"New\", \"tasks\": [" +
        string.Join(",", tasks.Select(t => TaskJson(t.Day, t.Title))) +
        "] } ] }\n```";

    private static void SetOverride(string planId, string title, int original, int assigned)
    {
        using (var seed = new Database()) { } // opening Database creates the schema before the raw insert
        using var conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO task_overrides (plan_id, task_text, original_day, assigned_day) " +
                          "VALUES ($pid, $text, $orig, $assigned)";
        cmd.Parameters.AddWithValue("$pid", planId);
        cmd.Parameters.AddWithValue("$text", title);
        cmd.Parameters.AddWithValue("$orig", original);
        cmd.Parameters.AddWithValue("$assigned", assigned);
        cmd.ExecuteNonQuery();
    }

    private static List<(int Day, string Title)> TasksIn(JsonObject node) =>
        node["phases"]!.AsArray()
            .SelectMany(p => p!["tasks"]!.AsArray())
            .Select(t => (Day: t!["day"]!.GetValue<int>(), Title: t!["task"]!.GetValue<string>()))
            .ToList();

    private static string? Parse(string reply, IEnumerable<string>? done = null) =>
        PlanReplacement.ParseReplacement(reply, (done ?? Array.Empty<string>()).ToList(), out _);

    [Fact]
    public void Fixture_TickedTasksStayPut_UnfinishedReplaced_StateAndFilesAsSpecified()
    {
        var id = NewId();
        var path = WritePlan(id, DateTime.Today.ToIsoDate());
        using (var db = new Database())
        {
            for (var d = 1; d <= 4; d++) db.SaveCompletion(id, d, $"Task {d}", true);
            db.SaveCompletion(id, 6, "Task 6", false);   // incomplete row: must go
            db.SaveCompletion(id, 2, "Task 6", true);    // ticked row on a stale day: must stay (AC 10)
        }
        SetOverride(id, "Task 7", original: 7, assigned: 12);

        using var database = new Database();
        var situation = PlanReplacement.Assess(id, database);
        Assert.Equal(6, situation.RemovedCount);
        Assert.Equal(4, situation.Done.Count);
        Assert.Equal(4, situation.LastDoneDay);
        Assert.Equal(5, situation.StartDay);

        Assert.Null(PlanReplacement.ParseReplacement(
            Reply((1, "New A"), (2, "New B"), (3, "New C"), (4, "New D"), (5, "New E")),
            situation.Done.Select(d => d.Title).ToList(), out var replacement));
        Assert.NotNull(replacement);

        var preview = PlanReplacement.BuildPreview(situation, replacement!);
        Assert.Equal(9, preview.NewTotalDays);
        Assert.Equal("6 unfinished tasks removed, 5 new tasks added",
            PlanReplacement.Headline(preview.Removed, preview.Added));

        var result = PlanReplacement.Apply(id, replacement!, expectedStartDay: 5, database);
        Assert.Equal(PlanReplacement.ApplyOutcome.Applied, result.Outcome);
        Assert.False(result.CleanupFailed);

        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(9, node["total_days"]!.GetValue<int>());
        Assert.Equal("keep me", node["briefing"]!["realistic_timeline"]!.GetValue<string>());
        Assert.Equal(5, node["phases"]![0]!["cost_eur"]!.GetValue<int>());
        Assert.Equal(2, node["phases"]!.AsArray().Count);
        Assert.Equal(
            new[] { (1, "Task 1"), (2, "Task 2"), (3, "Task 3"), (4, "Task 4"),
                    (5, "New A"), (6, "New B"), (7, "New C"), (8, "New D"), (9, "New E") },
            TasksIn(node));

        var overrides = database.LoadOverrides(id);
        Assert.False(overrides.ContainsKey("Task 7"));
        var completions = database.LoadCompletions();
        for (var d = 1; d <= 4; d++) Assert.True(completions[(id, d, $"Task {d}")]);
        Assert.True(completions[(id, 2, "Task 6")]);
        Assert.False(completions.ContainsKey((id, 6, "Task 6")));

        var plan = PlanStore.LoadActivePlans().Single(p => p.Id == id);
        Assert.Equal(9, plan.TotalDaysComputed);
    }

    [Fact]
    public void ZeroDone_PlanStartedToday_StartsAtDayOne()
    {
        var id = NewId();
        WritePlan(id, DateTime.Today.ToIsoDate());
        using var db = new Database();
        var situation = PlanReplacement.Assess(id, db);
        Assert.Empty(situation.Done);
        Assert.Equal(0, situation.LastDoneDay);
        Assert.Equal(1, situation.StartDay);
    }

    [Fact]
    public void ZeroDone_PlanFiftyNineDaysOld_StillStartsAtDayOne()
    {
        var id = NewId();
        WritePlan(id, DateTime.Today.AddDays(-59).ToIsoDate());

        using var db = new Database();
        var situation = PlanReplacement.Assess(id, db);
        Assert.Empty(situation.Done);
        Assert.Equal(1, situation.StartDay);
    }

    [Fact]
    public void PlanFiftyNineDaysOld_StartsAfterLastDoneTask_NotAtTodaysPlanDay()
    {
        var id = NewId();
        WritePlan(id, DateTime.Today.AddDays(-59).ToIsoDate());
        using (var db = new Database())
            for (var d = 1; d <= 4; d++) db.SaveCompletion(id, d, $"Task {d}", true);

        using var database = new Database();
        var situation = PlanReplacement.Assess(id, database);
        Assert.Equal(5, situation.StartDay);
    }

    [Fact]
    public void AllTasksDone_NothingToReplace_FileUntouched()
    {
        var id = NewId();
        var path = WritePlan(id, DateTime.Today.ToIsoDate());
        using (var db = new Database())
            for (var d = 1; d <= 10; d++) db.SaveCompletion(id, d, $"Task {d}", true);

        using var database = new Database();
        var situation = PlanReplacement.Assess(id, database);
        Assert.False(PlanReplacement.HasSomethingToReplace(situation));

        var before = File.ReadAllBytes(path);
        Assert.Null(PlanReplacement.ParseReplacement(Reply((1, "Anything")), Array.Empty<string>(), out var replacement));
        var result = PlanReplacement.Apply(id, replacement!, situation.StartDay, database);
        Assert.Equal(PlanReplacement.ApplyOutcome.NothingToReplace, result.Outcome);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void StartDayChangedSinceDialogOpened_NothingWritten()
    {
        var id = NewId();
        var path = WritePlan(id, DateTime.Today.ToIsoDate());
        using (var db = new Database())
            for (var d = 1; d <= 4; d++) db.SaveCompletion(id, d, $"Task {d}", true);
        var before = File.ReadAllBytes(path);

        using var database = new Database();
        PlanReplacement.ParseReplacement(Reply((1, "New A")), Array.Empty<string>(), out var replacement);
        var result = PlanReplacement.Apply(id, replacement!, expectedStartDay: 3, database);

        Assert.Equal(PlanReplacement.ApplyOutcome.StartChanged, result.Outcome);
        Assert.Equal(5, result.Situation.StartDay);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void PlanWithoutTotalDays_GetsTotalDaysSetToLastNewDay()
    {
        var id = NewId();
        var path = WritePlan(id, DateTime.Today.ToIsoDate(), withTotalDays: false);
        using var database = new Database();
        var situation = PlanReplacement.Assess(id, database);
        PlanReplacement.ParseReplacement(Reply((1, "New A"), (3, "New B")), Array.Empty<string>(), out var replacement);

        var result = PlanReplacement.Apply(id, replacement!, situation.StartDay, database);

        Assert.Equal(PlanReplacement.ApplyOutcome.Applied, result.Outcome);
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(3, node["total_days"]!.GetValue<int>());
        Assert.Equal(new[] { (1, "New A"), (3, "New B") }, TasksIn(node));
    }

    [Fact]
    public void DoneTaskRescheduledLater_PushesStartPastIt()
    {
        var id = NewId();
        WritePlan(id, DateTime.Today.ToIsoDate());
        using (var db = new Database())
        {
            db.SaveCompletion(id, 1, "Task 1", true);
            db.SaveCompletion(id, 3, "Task 3", true);
            db.SaveCompletion(id, 4, "Task 4", true);
            db.SaveCompletion(id, 9, "Task 2", true);
        }
        SetOverride(id, "Task 2", original: 2, assigned: 9);

        using var database = new Database();
        var situation = PlanReplacement.Assess(id, database);
        Assert.Equal(9, situation.LastDoneDay);
        Assert.Equal(10, situation.StartDay);
    }

    [Fact]
    public void ParseReplacement_ReportsTheFirstProblemInTableOrder()
    {
        Assert.Equal("Paste Claude's reply first.", Parse("   "));
        Assert.Equal("Couldn't find valid JSON — paste the whole reply including the ```json block.",
            Parse("just some words"));
        Assert.Equal("That JSON doesn't parse — check it's the complete reply including the ```json fence.",
            Parse("```json\n{ \"phases\": [ }\n```"));
        const string noTasks = "The reply has no tasks. It needs a 'phases' list with at least one task.";
        Assert.Equal(noTasks, Parse("```json\n{ \"name\": \"x\" }\n```"));
        Assert.Equal(noTasks, Parse("```json\n{ \"phases\": [ { \"tasks\": [] } ] }\n```"));
        Assert.Equal("Task 1 has no 'task' title.", Parse("```json\n{ \"phases\": [ { \"tasks\": [ { \"day\": 1 } ] } ] }\n```"));
        // A missing title anywhere outranks a bad day earlier in the reply.
        Assert.Equal("Task 2 has no 'task' title.", Parse(
            "```json\n{ \"phases\": [ { \"tasks\": [ { \"day\": 0, \"task\": \"Bad day\" }, { \"day\": 1 } ] } ] }\n```"));
        Assert.Equal("'Bad day' needs a whole-number 'day' of 1 or more.", Parse(
            "```json\n{ \"phases\": [ { \"tasks\": [ { \"day\": 0, \"task\": \"Bad day\" } ] } ] }\n```"));
        Assert.Equal("'Text day' needs a whole-number 'day' of 1 or more.", Parse(
            "```json\n{ \"phases\": [ { \"tasks\": [ { \"day\": \"3\", \"task\": \"Text day\" } ] } ] }\n```"));
        Assert.Equal("'Half day' needs a whole-number 'day' of 1 or more.", Parse(
            "```json\n{ \"phases\": [ { \"tasks\": [ { \"day\": 2.5, \"task\": \"Half day\" } ] } ] }\n```"));
        Assert.Equal("'task 1' is already a done task in this plan. Give the new task a different title.",
            Parse(Reply((1, "  task 1 ")), new[] { "Task 1" }));
        Assert.Equal("'a' appears twice in the reply. Titles must be unique.",
            Parse(Reply((1, "A"), (2, " a ")), Array.Empty<string>()));
    }

    [Fact]
    public void ParseReplacement_AcceptsFencedAndBareJson_AndReportsSpan()
    {
        var fenced = Reply((2, "Second"), (4, "Fourth"), (5, "Fifth"));
        Assert.Null(PlanReplacement.ParseReplacement(fenced, Array.Empty<string>(), out var r));
        Assert.NotNull(r);
        Assert.Equal(3, r!.TaskCount);
        Assert.Equal(2, r.MinDay);
        Assert.Equal(5, r.MaxDay);

        var bare = "{ \"phases\": [ { \"tasks\": [ { \"day\": 1, \"task\": \"Only\" } ] } ] }";
        Assert.Null(PlanReplacement.ParseReplacement(bare, Array.Empty<string>(), out var bareR));
        Assert.Equal(1, bareR!.TaskCount);
    }

    [Fact]
    public void FinishWording_IsWordsNotSigns()
    {
        var old = new DateOnly(2026, 10, 10);
        Assert.Equal("same day", PlanReplacement.DescribeFinishChange(old, old));
        Assert.Equal("1 day later", PlanReplacement.DescribeFinishChange(old, old.AddDays(1)));
        Assert.Equal("3 days earlier", PlanReplacement.DescribeFinishChange(old, old.AddDays(-3)));
        Assert.Equal("1 unfinished task removed, 1 new task added", PlanReplacement.Headline(1, 1));
        Assert.Equal("no done tasks", PlanReplacement.KeptText(0));
        Assert.Equal("1 done task", PlanReplacement.KeptText(1));
    }
}

using System.Text.Json.Nodes;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>"Replace remaining tasks" (2026-10-09): done tasks stay, the rest is swapped for a pasted
/// list. Every test uses its own plan id (the test DB is shared) and a scratch MENTOR_ROOT.</summary>
[Collection("TestRoot")]
public sealed class PlanRemainderTests
{
    private static string NewId() => "rr-" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>10 tasks on days 1-10; tasks 1-4 complete; task 7 rescheduled to day 12.</summary>
    private static (Plan plan, Database db, string path) Fixture(string id, int doneCount = 4, bool withTotalDays = true)
    {
        Directory.CreateDirectory(AppPaths.ActivePlansDir);
        var path = Path.Combine(AppPaths.ActivePlansDir, id + ".json");
        var tasks = new JsonArray();
        for (var d = 1; d <= 10; d++) tasks.Add(new JsonObject { ["day"] = d, ["task"] = $"Task {d}" });
        var root = new JsonObject
        {
            ["id"] = id, ["name"] = "Test plan", ["start_date"] = DateTime.Today.ToString("yyyy-MM-dd"), ["briefing"] = new JsonObject { ["ignore_completely"] = "x" },
            ["phases"] = new JsonArray { new JsonObject { ["phase"] = 1, ["name"] = "Only", ["days_range"] = "1-10", ["tasks"] = tasks } },
        };
        if (withTotalDays) root["total_days"] = 10;
        File.WriteAllText(path, root.ToJsonString());

        var db = new Database();
        for (var d = 1; d <= doneCount; d++) db.SaveCompletion(id, d == 7 ? 12 : d, $"Task {d}", true);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO task_overrides (plan_id, task_text, original_day, assigned_day) VALUES ($p,'Task 7',7,12)";
            cmd.Parameters.AddWithValue("$p", id);
            cmd.ExecuteNonQuery();
        }
        var plan = System.Text.Json.JsonSerializer.Deserialize<Plan>(File.ReadAllText(path))!;
        return (plan, db, path);
    }

    private static List<AssignedTask> Assigned(Plan plan, Database db) =>
        PlanStore.TasksFor(plan, db, db.LoadCompletions());

    private const string Reply = """
        ```json
        { "phases": [ { "phase": 1, "name": "New", "tasks": [
          { "day": 1, "task": "N1" }, { "day": 2, "task": "N2" }, { "day": 4, "task": "N3", "tools": ["Excel"] },
          { "day": 5, "task": "N4" }, { "day": 6, "task": "N5" } ] } ] }
        ```
        """;

    [Fact]
    public void Prepare_ShiftsNewTasksToStartAfterLastDoneDay_AndCountsEverything()
    {
        var id = NewId();
        var (plan, db, _) = Fixture(id);
        using var _d = db;

        Assert.Null(PlanRemainder.Prepare(plan, Assigned(plan, db), Reply, out var c));

        Assert.Equal(4, c!.DoneCount);
        Assert.Equal(6, c.RemovedTexts.Count);
        Assert.Equal(5, c.AddedCount);
        Assert.Equal(5, c.StartDay);
        Assert.Equal(10, c.LastNewDay);   // day 6 + 5 - 1
    }

    [Fact]
    public void Apply_KeepsDone_RemovesRest_AddsNew_PreservesExtras_AndClearsOverrides()
    {
        var id = NewId();
        var (plan, db, path) = Fixture(id);
        using var _d = db;
        Assert.Null(PlanRemainder.Prepare(plan, Assigned(plan, db), Reply, out var c));

        PlanStore.ReplaceRemainingTasks(id, c!, db);

        var node = JsonNode.Parse(File.ReadAllText(path))!;
        var all = node["phases"]!.AsArray().SelectMany(p => p!["tasks"]!.AsArray()).Select(t => (string)t!["task"]!).ToList();
        Assert.Equal(new[] { "Task 1", "Task 2", "Task 3", "Task 4", "N1", "N2", "N3", "N4", "N5" }, all);
        var n3 = node["phases"]!.AsArray().SelectMany(p => p!["tasks"]!.AsArray()).Single(t => (string)t!["task"]! == "N3")!;
        Assert.Equal(8, (int)n3["day"]!);                       // 4 + 5 - 1
        Assert.Equal("Excel", (string)n3["tools"]![0]!);        // extra fields carried through
        Assert.Equal("x", (string)node["briefing"]!["ignore_completely"]!);
        Assert.Equal("1-10", (string)node["phases"]![0]!["days_range"]!);   // untouched phase extras
        Assert.Equal(2, (int)node["phases"]![1]!["phase"]!);    // renumbered after the surviving phase
        Assert.Equal(10, (int)node["total_days"]!);
        Assert.Empty(db.LoadOverrides(id));                     // Task 7's override is gone
        for (var d = 1; d <= 4; d++)                            // completions untouched
            Assert.True(db.LoadCompletions()[(id, d, $"Task {d}")]);
    }

    [Fact]
    public void Apply_NoDoneTasks_StartsAtDayOne_AndDropsTheOldPhase()
    {
        var id = NewId();
        var (plan, db, path) = Fixture(id, doneCount: 0);
        using var _d = db;
        Assert.Null(PlanRemainder.Prepare(plan, Assigned(plan, db), Reply, out var c));
        Assert.Equal(1, c!.StartDay);

        PlanStore.ReplaceRemainingTasks(id, c, db);

        var node = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Single(node["phases"]!.AsArray());
        Assert.Equal(1, (int)node["phases"]![0]!["phase"]!);
        Assert.Equal(6, (int)node["total_days"]!);
    }

    [Fact]
    public void Prepare_AllDone_IsRefused()
    {
        var id = NewId();
        var (plan, db, _) = Fixture(id, doneCount: 10);
        using var _d = db;

        var err = PlanRemainder.Prepare(plan, Assigned(plan, db), Reply, out var c);

        Assert.Null(c);
        Assert.Contains("nothing to replace", err);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"phases\": [] }")]
    [InlineData("{ \"phases\": [ { \"tasks\": [ { \"day\": 1, \"task\": \"Task 2\" } ] } ] }")]   // collides with a done title
    [InlineData("{ \"phases\": [ { \"tasks\": [ { \"day\": 1, \"task\": \"task 2\" } ] } ] }")]   // ...ignoring case
    [InlineData("{ \"phases\": [ { \"tasks\": [ { \"day\": 1, \"task\": \"A\" }, { \"day\": 2, \"task\": \"A\" } ] } ] }")]
    [InlineData("{ \"phases\": [ { \"tasks\": [ { \"day\": 0, \"task\": \"A\" } ] } ] }")]
    [InlineData("{ \"phases\": [ { \"tasks\": [ { \"day\": 1 } ] } ] }")]
    public void Prepare_RejectsBadReplies(string reply)
    {
        var id = NewId();
        var (plan, db, path) = Fixture(id);
        using var _d = db;
        var before = File.ReadAllText(path);

        var err = PlanRemainder.Prepare(plan, Assigned(plan, db), reply, out var c);

        Assert.NotNull(err);
        Assert.Null(c);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void Prepare_PlanFarBehind_StillStartsRightAfterLastDoneTask()
    {
        var id = NewId();
        var (plan, db, path) = Fixture(id);
        using var _d = db;
        plan.StartDate = DateTime.Today.AddDays(-59).ToString("yyyy-MM-dd");   // today = plan day 60

        Assert.Null(PlanRemainder.Prepare(plan, Assigned(plan, db), Reply, out var c));
        Assert.Equal(5, c!.StartDay);      // user decision: not clamped to today (plan day 60)
        Assert.Equal(10, c.LastNewDay);

        PlanStore.ReplaceRemainingTasks(id, c, db);
        var after = System.Text.Json.JsonSerializer.Deserialize<Plan>(File.ReadAllText(path))!;
        after.StartDate = plan.StartDate;
        Assert.Equal(10, after.TotalDaysComputed);   // sidebar "Finishes" / drift follow the new length
        Assert.Equal(0, after.DriftDays(Assigned(after, db)));
    }

    [Fact]
    public void Prompt_ListsDoneTasks_AndTheWish()
    {
        var id = NewId();
        var (plan, db, _) = Fixture(id);
        using var _d = db;

        var p = PlanRemainder.BuildPrompt(plan, Assigned(plan, db), "drop Reddit");

        Assert.Contains("Day 1: Task 1", p);
        Assert.Contains("drop Reddit", p);
        Assert.DoesNotContain("ALREADY DONE (do not repeat, do not reuse these titles):\n- Day 5", p.Replace("\r\n", "\n"));
    }
}

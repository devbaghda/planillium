using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// PlanStore.DistinctTools collects every task's "tools" entries into the flat,
/// deduplicated list AddPlanDialog teaches to config.json's activity_rules.on_plan
/// right after import (see PlanTask.Tools' own doc comment).
/// </summary>
public sealed class PlanStoreToolsTests
{
    private static Plan MakePlan(params (string Task, string[] Tools)[] tasks) => new()
    {
        Id = "test",
        Name = "Test Plan",
        Phases = new List<Phase>
        {
            new()
            {
                Name = "Phase 1",
                Tasks = tasks.Select((t, i) => new PlanTask
                {
                    Day = i + 1,
                    Text = t.Task,
                    Tools = t.Tools.ToList(),
                }).ToList(),
            },
        },
    };

    [Fact]
    public void DistinctTools_CollectsAcrossAllTasks()
    {
        var plan = MakePlan(
            ("Task 1", new[] { "VS Code", "Terminal" }),
            ("Task 2", new[] { "Anki" }));

        var tools = PlanStore.DistinctTools(plan);

        Assert.Equal(new[] { "VS Code", "Terminal", "Anki" }, tools);
    }

    [Fact]
    public void DistinctTools_DedupesCaseInsensitively()
    {
        var plan = MakePlan(
            ("Task 1", new[] { "VS Code" }),
            ("Task 2", new[] { "vs code", "VS CODE" }));

        var tools = PlanStore.DistinctTools(plan);

        Assert.Equal(new[] { "VS Code" }, tools);
    }

    [Fact]
    public void DistinctTools_IgnoresBlankAndWhitespaceEntries()
    {
        var plan = MakePlan(("Task 1", new[] { "  ", "", "Chrome - Coursera", "  " }));

        var tools = PlanStore.DistinctTools(plan);

        Assert.Equal(new[] { "Chrome - Coursera" }, tools);
    }

    [Fact]
    public void DistinctTools_EmptyWhenNoTaskNamesAny()
    {
        var plan = MakePlan(("Task 1", Array.Empty<string>()), ("Task 2", Array.Empty<string>()));

        Assert.Empty(PlanStore.DistinctTools(plan));
    }
}

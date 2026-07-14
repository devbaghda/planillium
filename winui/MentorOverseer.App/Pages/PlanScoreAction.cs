using MentorOverseer.App.Models;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

/// <summary>
/// Shared shape behind every "pick a task action that mutates score/schedule
/// state, then refresh" button on Today/Schedule: reload plans from disk (so
/// the mutation applies to the live copy, not a possibly-stale one the page
/// rendered from), find this plan's current copy, open a scoped
/// Database/ScoreService pair, run the mutation, log on failure — then let
/// the caller decide what "refresh" means for its own page. <paramref name="after"/>
/// receives whether the mutation actually succeeded — a failed write used to look
/// identical to a successful one (page just re-rendered either way), so callers can
/// now surface it the same way a failed task-checkbox save already does (round-5
/// audit finding #8).
/// </summary>
internal static class PlanScoreAction
{
    public static void Run(Plan plan, Action<ScoreService, Plan> mutate, string logTag, Action<bool> after)
    {
        var ok = true;
        try
        {
            var plans = PlanStore.LoadActivePlans();
            var p = plans.FirstOrDefault(x => x.Id == plan.Id) ?? plan;
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            mutate(score, p);
        }
        catch (Exception ex) { Log.Error(logTag, ex); ok = false; }
        after(ok);
    }
}

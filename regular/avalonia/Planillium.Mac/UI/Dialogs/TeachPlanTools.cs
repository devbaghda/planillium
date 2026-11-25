using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// Activates a queued plan idea (shared by StartQueuedPlanDialog and PlansPage's "Start now").
/// Until 2026-11-12 this also taught the plan's "tools" to the on-plan activity keywords; those lists were
/// removed (learned rules are the only classifier), so activation is all that is left.
/// </summary>
internal static class TeachPlanTools
{
    /// <summary>Activates a queued plan idea. Returns false (and logs under <paramref name="logContext"/>)
    /// only if activation itself failed; the caller decides what a failure looks like on screen.</summary>
    public static async Task<bool> ActivateQueuedPlanAsync(XamlRoot xamlRoot, string planId, string logContext)
    {
        try
        {
            PlanStore.ActivateQueuedPlan(planId);
        }
        catch (Exception ex)
        {
            Log.Error(logContext, ex);
            return false;
        }
        await Task.CompletedTask;
        return true;
    }
}

using Microsoft.UI.Xaml;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// Teaches a plan's distinct "tools" entries to config.json's activity_rules.on_plan list
/// (same mechanism as the Diary's manual "mark selected as on-plan" bulk action) and shows a
/// one-button notice naming what was learned — shared between a fresh "Add Plan" import
/// (AddPlanDialog) and re-running it later against an already-active plan (PlansPage's "Teach
/// on-plan apps…" button, for plans that already had tools added to their file some other way),
/// since both need the exact same teach-then-notify behavior.
/// </summary>
internal static class TeachPlanTools
{
    public static async Task RunAsync(XamlRoot xamlRoot, List<string> tools)
    {
        if (tools.Count == 0) return;

        // LearnActivityRule refuses a bare browser name ("Chrome") — too wide to mean
        // anything as a rule, since a browser hosts both on-plan and off-plan content
        // depending on the tab — so what's actually taught can be a subset of what was
        // asked for. Reported honestly below rather than claiming credit for a skip.
        var taught = new List<string>();
        var skipped = new List<string>();
        foreach (var tool in tools)
            (ConfigService.LearnActivityRule(tool, DiaryCategory.OnPlan) ? taught : skipped).Add(tool);
        if (taught.Count > 0) (App.MainWindow as MainWindow)?.RestartTracker();

        // A short, dismissable heads-up — not a confirmation gate — since teaching these
        // keywords already happened above; this only exists so it isn't a silent change to
        // something that affects scoring/alerts going forward. Settings' own ACTIVITY
        // KEYWORDS list is where to edit/remove any of these later.
        var msg = taught.Count > 0
            ? $"Taught {taught.Count} on-plan keyword(s) from this plan's tasks, " +
              "so time spent in them now counts toward staying on track: " +
              $"{string.Join(", ", taught)}. Edit or remove any of these anytime " +
              "in Settings under ACTIVITY KEYWORDS."
            : "Nothing new to teach from this plan's tools list.";
        if (skipped.Count > 0)
            msg += $" Skipped as too generic to teach safely (a whole browser, not a specific " +
                   $"site, would count as on-plan): {string.Join(", ", skipped)}.";
        var notice = DialogControls.Build(xamlRoot, "Learned new on-plan apps", msg, closeButtonText: "Got it");
        await DialogGate.ShowAsync(notice);
    }

    /// <summary>Activates a queued plan idea and teaches its tools — the exact "activate,
    /// then re-read the now-active plan from disk, then teach" sequence StartQueuedPlanDialog
    /// and PlansPage's "Start now" button both need, previously hand-typed identically in
    /// each (2026-07-28 audit finding). Re-reads from disk rather than trusting an
    /// already-loaded copy, since ActivateQueuedPlan patches start_date on the file after
    /// any in-memory copy of it was loaded. Returns false (and logs under
    /// <paramref name="logContext"/>) only if activation itself failed; the caller decides
    /// what a failure looks like on screen.</summary>
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
        var activated = PlanStore.LoadActivePlans().FirstOrDefault(p => p.Id == planId);
        if (activated != null)
            await RunAsync(xamlRoot, PlanStore.DistinctTools(activated));
        return true;
    }
}

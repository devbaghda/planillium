using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Models;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// "Want to start one of your queued ideas?" — offered right after a plan archives
/// (a slot just freed up) and available directly from the Plans page's "Queued ideas"
/// row too, so both entry points share one picker instead of two independently-built
/// dialogs drifting apart (2026-07-22 request).
/// </summary>
public static class StartQueuedPlanDialog
{
    /// <summary>Returns true if a plan was actually activated (caller should Render()
    /// and RefreshScore()); false if the user dismissed without picking one.</summary>
    public static async Task<bool> ShowAsync(Page host, List<Plan> queued)
    {
        if (queued.Count == 0) return false;

        var list = new RadioButtons { SelectedIndex = 0 };
        foreach (var plan in queued) list.Items.Add(plan.Name);

        var dialog = DialogControls.Build(host.XamlRoot, "Start a queued idea?",
            new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "A plan slot just freed up. Pick one of your saved ideas to " +
                               "start now, or do it later from the Plans page.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    list,
                },
            },
            primaryButtonText: "Start", closeButtonText: "Not now", defaultButton: ContentDialogButton.Primary);

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return false;
        var chosen = queued[list.SelectedIndex];
        try
        {
            PlanStore.ActivateQueuedPlan(chosen.Id);
        }
        catch (Exception ex)
        {
            Log.Error("StartQueuedPlanDialog.Activate", ex);
            return false;
        }
        return true;
    }
}

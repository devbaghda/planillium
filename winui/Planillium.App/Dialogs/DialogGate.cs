using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// WinUI throws if two ContentDialogs are open on the same XamlRoot, and this
/// app has three independent triggers (kickoff, idle-return, EOD review) plus
/// user-opened dialogs. Every ShowAsync in the app goes through this gate so
/// they queue instead of colliding.
/// </summary>
public static class DialogGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    // How long a wait for the gate has to run before it's suspicious rather than just normal
    // queuing behind another dialog the user is actively looking at.
    private static readonly TimeSpan SlowWaitThreshold = TimeSpan.FromSeconds(15);

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        // Diagnostic only (2026-07-20 request): a user reported the end-of-day review never
        // appearing at all, with no error in the log — one plausible cause is an earlier
        // dialog (e.g. the idle-return "where have you been?" prompt this same review flow
        // shows first) sitting open and un-dismissed, silently holding this gate and starving
        // every dialog queued behind it, including the review. This can't be confirmed from
        // the code alone, so log a Warn naming both dialogs if it happens again instead of
        // guessing a fix.
        var waitStarted = DateTime.UtcNow;
        await Gate.WaitAsync();
        var waited = DateTime.UtcNow - waitStarted;
        if (waited > SlowWaitThreshold)
            Log.Warn("DialogGate.ShowAsync",
                $"waited {waited.TotalSeconds:0}s for the gate before showing '{dialog.Title}' — " +
                "another dialog held it open that long, which can starve later prompts entirely");
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Gate.Release();
        }
    }
}

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// Shared "show the real dialog if the window is visible, otherwise raise a
/// toast" routing decision — every timed prompt (kickoff, idle-return,
/// evening review) needs this, because a bare ContentDialog inside a hidden
/// window is invisible and never seen. Extracted once a third copy of the
/// exact same pattern showed up (ReviewDialog.Trigger); KickoffDialog and
/// IdleReturnDialog had already duplicated it independently before that.
///
/// Callers keep their own once-per-day toast throttle as a plain nullable
/// date-string field (a static field can't be passed by <c>ref</c> through
/// an async method), passed in as a pair of callbacks instead — this router
/// owns the routing decision and the actual Show call, not the throttle
/// state itself.
/// </summary>
internal static class PromptRouter
{
    public static async Task ShowOrToast(MainWindow window, Func<Task> showDialog,
        Func<bool> toastAlreadySentToday, Action markToastSent,
        string toastTitle, string toastMessage, string toastTag,
        params (string Key, string Value)[] toastArgs)
    {
        if (window.IsOnScreen())
        {
            // IsOnScreen only means "not hidden/minimized" — the window
            // could still be buried behind whatever's actually in front.
            // Bring it forward so the dialog that's about to open isn't
            // opening unseen behind another app.
            window.Activate();
            await showDialog();
            return;
        }
        if (toastAlreadySentToday()) return;
        markToastSent();
        Services.ToastNotifier.Show(toastTitle, toastMessage, toastTag, toastArgs);
    }
}

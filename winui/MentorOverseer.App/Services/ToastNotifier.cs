using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace MentorOverseer.App.Services;

/// <summary>
/// Thin wrapper around the Windows App SDK's toast notifications — the native
/// "bubble from the tray/notification area" surface Windows already uses for
/// every other background app. Timed prompts (morning kickoff, welcome-back)
/// route through this instead of opening a ContentDialog directly, because a
/// ContentDialog only renders if the main window happens to be visible — and
/// this app spends most of its life hidden in the tray. <see cref="AddArgument"/>
/// key/value pairs round-trip through <c>AppNotificationActivatedEventArgs.Arguments</c>
/// when the user clicks the toast, so MainWindow's activation handler knows
/// which dialog to open (and with what data) in response.
/// </summary>
/// <summary>
/// Argument keys/values shared between a toast's producer (ToastNotifier
/// callers) and its consumer (MainWindow's NotificationInvoked handler) —
/// named once so a typo in either place is a compile error instead of a
/// silently-broken "click notification to reopen the prompt."
/// </summary>
public static class ToastArgs
{
    public const string Action = "action";
    public const string Kickoff = "kickoff";
    public const string IdleReturn = "idlereturn";
    public const string Review = "review";
    public const string Mins = "mins";
    public const string Start = "start";
}

public static class ToastNotifier
{
    /// <param name="tag">When set, a later Show with the same tag replaces
    /// this notification in Windows' Action Center instead of stacking a
    /// new one beside it — used by the recurring prompts (kickoff,
    /// idle-return, review) so a day's worth of missed toasts can't pile up
    /// into a lingering history of exactly when/how-long the user was away.
    /// Leave null for one-off alerts that aren't meant to replace anything
    /// (e.g. the focus-nudge alert).</param>
    public static void Show(string title, string message, string? tag,
        params (string Key, string Value)[] args)
    {
        try
        {
            var builder = new AppNotificationBuilder().AddText(title).AddText(message);
            foreach (var (key, value) in args)
                builder.AddArgument(key, value);
            if (tag is { Length: > 0 }) builder.SetTag(tag);
            AppNotificationManager.Default.Show(builder.BuildNotification());
            // Centralized here rather than at each call site so every current and future
            // toast automatically participates in the unread tray dot (2026-07-20 request).
            NotificationCenter.MarkUnread();
        }
        catch (Exception ex)
        {
            Log.Error("ToastNotifier.Show", ex);
        }
    }
}

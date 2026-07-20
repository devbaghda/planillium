namespace MentorOverseer.App.Services;

/// <summary>
/// Tracks whether any toast notification has fired since the user last actually looked at the
/// app — backs the tray icon's unread dot (2026-07-20 request: "make all the notifications
/// remain unread until I read them"). "Read" means the main window was brought to the
/// foreground; there's no separate per-notification inbox, so looking at the app is the same
/// "I've seen what's going on" signal every other tray-icon badge uses. Persisted via
/// StateService so a notification that fires while the app is closed still shows as unread on
/// next launch, and a quit-right-after-a-toast doesn't silently lose the unread state.
/// </summary>
public static class NotificationCenter
{
    public static event Action? UnreadChanged;

    public static bool HasUnread => StateService.Load().UnreadNotifications > 0;

    public static void MarkUnread()
    {
        var s = StateService.Load();
        s.UnreadNotifications++;
        StateService.Save(s);
        UnreadChanged?.Invoke();
    }

    public static void MarkAllRead()
    {
        var s = StateService.Load();
        if (s.UnreadNotifications == 0) return;
        s.UnreadNotifications = 0;
        StateService.Save(s);
        UnreadChanged?.Invoke();
    }
}

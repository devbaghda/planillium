namespace Planillium.App.Services;

/// <summary>
/// Tracks what's fired since the user last actually looked at the app — backs the tray icon's
/// unread dot (2026-07-20 request: "make all the notifications remain unread until I read
/// them") and, since 2026-07-22, a short recap of what the dot was actually about (the dot
/// alone gave no way to see what triggered it after clicking the tray icon — there was no
/// per-notification inbox at all). "Read" means <see cref="TakePending"/> was called, which
/// MainWindow does right after showing that recap; simply being brought to the foreground no
/// longer clears it on its own; the recap dialog is what clears it, not the act of looking.
/// Persisted via StateService so anything that fires while the app is closed, or right before
/// it's quit, still shows up next launch.
/// </summary>
public static class NotificationCenter
{
    public static event Action? UnreadChanged;

    public static bool HasUnread => StateService.Load().UnreadNotifications > 0;

    // Keeps the recap a short "what did I miss" list, not an unbounded log — oldest entries
    // drop off first if this genuinely never gets checked for a long stretch.
    private const int MaxPending = 10;

    public static void Record(string title, string message)
    {
        var s = StateService.Load();
        s.UnreadNotifications++;
        s.PendingNotifications.Add(new PendingNotification
        {
            Title = title,
            Message = message,
            AtIso = DateTime.Now.ToIsoTimestamp(),
        });
        if (s.PendingNotifications.Count > MaxPending)
            s.PendingNotifications.RemoveRange(0, s.PendingNotifications.Count - MaxPending);
        StateService.Save(s);
        UnreadChanged?.Invoke();
    }

    /// <summary>Returns everything recorded since the last time this was called, clearing both
    /// the list and the unread count in the same step — the recap dialog calls this once, right
    /// before showing what it returns, so nothing shown is left dangling as still-unread.</summary>
    public static List<PendingNotification> TakePending()
    {
        var s = StateService.Load();
        if (s.PendingNotifications.Count == 0 && s.UnreadNotifications == 0)
            return new List<PendingNotification>();
        var pending = s.PendingNotifications;
        s.PendingNotifications = new List<PendingNotification>();
        s.UnreadNotifications = 0;
        StateService.Save(s);
        UnreadChanged?.Invoke();
        return pending;
    }
}

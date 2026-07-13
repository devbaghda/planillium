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
public static class ToastNotifier
{
    public static void Show(string title, string message, params (string Key, string Value)[] args)
    {
        try
        {
            var builder = new AppNotificationBuilder().AddText(title).AddText(message);
            foreach (var (key, value) in args)
                builder.AddArgument(key, value);
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            Log.Error("ToastNotifier.Show", ex);
        }
    }
}

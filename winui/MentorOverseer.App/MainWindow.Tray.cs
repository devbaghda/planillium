using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

// The notify-icon / tray menu — see MainWindow.xaml.cs for the file split.
public sealed partial class MainWindow
{
    private H.NotifyIcon.TaskbarIcon? _tray;

    // Two cached Icon instances (built once) instead of compositing on every unread-state
    // change — Icon.FromHandle wraps a raw HICON that isn't cleaned up by normal GC/Dispose,
    // so building it repeatedly would leak a GDI handle per toggle. _badgedIconHandle is the
    // one that needs an explicit DestroyIcon call on shutdown; _plainIcon owns its own handle
    // via the normal Icon(path) constructor and disposes fine on its own.
    private System.Drawing.Icon? _plainIcon;
    private System.Drawing.Icon? _badgedIcon;
    private IntPtr _badgedIconHandle;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private void InitTray()
    {
        try
        {
            var open = new MenuFlyoutItem { Text = $"Open {AppInfo.DisplayName}" };
            open.Click += (_, _) => ShowFromTray();
            var quit = new MenuFlyoutItem { Text = "Quit (stops tracking)" };
            quit.Click += (_, _) => { _reallyClose = true; Close(); };
            var menu = new MenuFlyout();
            menu.Items.Add(open);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(quit);

            var openCommand = new Microsoft.UI.Xaml.Input.XamlUICommand();
            openCommand.ExecuteRequested += (_, _) => ShowFromTray();

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            _plainIcon = new System.Drawing.Icon(iconPath);
            _badgedIcon = BuildBadgedIcon(iconPath);

            _tray = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = AppInfo.DisplayName,
                Icon = NotificationCenter.HasUnread ? _badgedIcon : _plainIcon,
                ContextFlyout = menu,
                ContextMenuMode = H.NotifyIcon.ContextMenuMode.SecondWindow,
                LeftClickCommand = openCommand,
            };
            _tray.ForceCreate();

            NotificationCenter.UnreadChanged += OnUnreadChanged;
        }
        catch (Exception ex)
        {
            // No tray = closing must really close, or the app becomes unkillable.
            Log.Error("InitTray (window close will quit instead)", ex);
            _tray = null;
            _reallyClose = true;
        }
    }

    /// <summary>Composites a small red dot onto the bottom-right corner of the base tray icon
    /// — built once at startup and cached rather than generated per-toggle (see field-level
    /// comment on _badgedIcon).</summary>
    private System.Drawing.Icon BuildBadgedIcon(string basePath)
    {
        using var baseIcon = new System.Drawing.Icon(basePath, 32, 32);
        using var bmp = baseIcon.ToBitmap();
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var dotSize = bmp.Width / 3;
            var rect = new System.Drawing.Rectangle(bmp.Width - dotSize, bmp.Height - dotSize, dotSize, dotSize);
            // Thin white ring so the dot reads clearly against a light or dark taskbar.
            using var ring = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            g.FillEllipse(ring, System.Drawing.Rectangle.Inflate(rect, 1, 1));
            using var dot = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 224, 43, 43));
            g.FillEllipse(dot, rect);
        }
        // Icon.FromHandle wraps this HICON without taking ownership of it — the handle is
        // destroyed explicitly in Closed (MainWindow.xaml.cs), same as every other native
        // resource this app opens directly.
        _badgedIconHandle = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(_badgedIconHandle);
    }

    private void OnUnreadChanged()
    {
        if (_tray is null || _plainIcon is null || _badgedIcon is null) return;
        _dq.TryEnqueue(() =>
        {
            // Diagnostic (2026-07-22 request): a user report of the tray icon briefly
            // disappearing right after this exact swap (off-plan toast fired -> badge
            // appeared -> clicking the icon made it vanish, though the process itself
            // never actually died — confirmed still running/responding throughout, and
            // the global UnhandledException logger caught nothing). Logging the swap
            // itself, guarded, so a future occurrence can be time-correlated against
            // whatever click/H.NotifyIcon activity happens in the same second, instead
            // of guessing at a fix with no evidence of the actual mechanism.
            try
            {
                var hasUnread = NotificationCenter.HasUnread;
                _tray.Icon = hasUnread ? _badgedIcon : _plainIcon;
                Log.Info($"MainWindow.OnUnreadChanged: swapped tray icon, hasUnread={hasUnread}");
            }
            catch (Exception ex)
            {
                Log.Error("MainWindow.OnUnreadChanged (icon swap)", ex);
            }
        });
    }

    private void DisposeTray()
    {
        NotificationCenter.UnreadChanged -= OnUnreadChanged;
        _tray?.Dispose();
        _plainIcon?.Dispose();
        _badgedIcon?.Dispose();
        if (_badgedIconHandle != IntPtr.Zero) DestroyIcon(_badgedIconHandle);
    }

    public void HideToTray() => AppWindow.Hide();

    private void ShowFromTray()
    {
        _dq.TryEnqueue(() =>
        {
            // Diagnostic + defensive try/catch (2026-07-22 request): a user report of the
            // whole app appearing to close right after clicking the tray icon (it hadn't
            // actually — same process, still responding throughout), right after an
            // off-plan toast had just swapped the tray icon to its badged version. This
            // was the one dispatcher callback in this file with no guard at all, unlike
            // OnNotificationInvoked's established "guarded dispatch" pattern elsewhere in
            // MainWindow — bringing it in line, and logging entry/exit so a recurrence is
            // provable instead of guessed at.
            Log.Info("MainWindow.ShowFromTray: invoked");
            try
            {
                AppWindow.Show();
                Activate();
            }
            catch (Exception ex)
            {
                Log.Error("MainWindow.ShowFromTray", ex);
            }
        });
    }
}

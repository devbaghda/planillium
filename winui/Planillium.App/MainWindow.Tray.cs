using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App;

// The notify-icon / tray menu — see MainWindow.xaml.cs for the file split.
public sealed partial class MainWindow
{
    private H.NotifyIcon.TaskbarIcon? _tray;
    private string _iconPath = "";
    // Pre-rendered badged-icon bytes (a real .ico file image in memory), built once at startup
    // — NOT a live Icon object. A fresh, self-owned Icon is constructed from these bytes (or
    // from the plain icon file) on every swap instead (2026-07-22 fix, see OnUnreadChanged):
    // the previous design cached two long-lived Icon instances and handed the same objects to
    // H.NotifyIcon's TaskbarIcon.Icon setter over and over — reproduced live, this throws
    // ObjectDisposedException on the second swap back to an icon that was already handed over
    // once (confirmed via the exact stack trace: TaskbarIcon.UpdateIcon reading .Handle on an
    // already-disposed Icon), which left the tray stuck on whatever badge state it was in at
    // the moment of the first swap-back and silently broke every activation after that — the
    // real cause of both "the red dot never clears" and the earlier, never-explained "tray icon
    // vanished" reports. Building via Icon(Stream)/Icon(path) also sidesteps the raw-HICON
    // ownership dance the old Icon.FromHandle approach needed (that handle is destroyed
    // synchronously inside BuildBadgedIconBytes, right after it's serialized to bytes, so there
    // is no longer a handle that needs explicit cleanup at shutdown either).
    private byte[]? _badgedIconBytes;

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

            _iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            _badgedIconBytes = BuildBadgedIconBytes(_iconPath);

            _tray = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = AppInfo.DisplayName,
                // Not disposed after handing it over — see the field-level comment: the
                // evidence from the reproduced crash is that TaskbarIcon takes ownership of
                // whatever Icon it's given and disposes the previous one itself once a new one
                // replaces it, so a `using` here would just double-dispose (harmless) at best
                // and risk racing the library's own internal read of it at worst.
                Icon = BuildIcon(NotificationCenter.HasUnread),
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

    /// <summary>A fresh, self-owned Icon for the given badge state — build a new one on every
    /// call, never cache/reuse the result (see the field-level comment on why reuse breaks).</summary>
    private System.Drawing.Icon BuildIcon(bool badged) =>
        badged
            ? new System.Drawing.Icon(new MemoryStream(_badgedIconBytes!))
            : new System.Drawing.Icon(_iconPath);

    /// <summary>Composites a small red dot onto the bottom-right corner of the base tray icon
    /// and serializes the result to real .ico bytes — done once at startup. Going through
    /// Icon.Save (not just wrapping the raw HICON via Icon.FromHandle) is what lets every later
    /// Icon built from these bytes be a normal, fully self-owned icon that's safe to construct
    /// and dispose freely, with no manual handle-lifetime bookkeeping needed.</summary>
    private byte[] BuildBadgedIconBytes(string basePath)
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
        var hicon = bmp.GetHicon();
        try
        {
            using var temp = System.Drawing.Icon.FromHandle(hicon);
            using var ms = new MemoryStream();
            temp.Save(ms);
            return ms.ToArray();
        }
        finally
        {
            // The raw HICON is only needed long enough to serialize it above — destroy it here
            // rather than leaving it for a shutdown-time cleanup step that no longer exists.
            DestroyIcon(hicon);
        }
    }

    private void OnUnreadChanged()
    {
        if (_tray is null) return;
        _dq.TryEnqueue(() =>
        {
            try
            {
                var hasUnread = NotificationCenter.HasUnread;
                // Not disposed here either, for the same reason as InitTray's assignment above
                // — TaskbarIcon appears to take ownership of the icon it's handed.
                _tray.Icon = BuildIcon(hasUnread);
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

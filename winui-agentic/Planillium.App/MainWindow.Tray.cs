using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

    // The only previous way to stop being tracked was to quit the whole app — a heavier
    // action than a short deliberate break really needs (2026-07-23 audit finding #17,
    // "no lightweight pause option"). Session-only: a fresh launch always starts tracking
    // again, same as today; this just adds a lighter in-between option while the app stays
    // open.
    private MenuFlyoutItem? _pauseTrackingItem;

    // True only while the user has explicitly paused from the tray. RestartTracker() (Settings
    // autosave, a diary re-categorization teaching a new keyword) checks this so an unrelated
    // action doesn't silently un-pause tracking behind the user's back and leave the tray label
    // lying about what's actually running (2026-07-23 re-audit finding: the first cut of this
    // feature let RestartTracker unconditionally restart the tracker with no way to tell it was
    // supposed to stay paused).
    private bool _trackingPaused;

    private void InitTray()
    {
        try
        {
            var open = new MenuFlyoutItem { Text = $"Open {AppInfo.DisplayName}" };
            open.Click += (_, _) => ShowFromTray();
            _pauseTrackingItem = new MenuFlyoutItem { Text = "Pause tracking" };
            _pauseTrackingItem.Click += (_, _) => TogglePauseTracking();
            var quit = new MenuFlyoutItem { Text = "Quit (stops tracking)" };
            quit.Click += (_, _) => { _reallyClose = true; Close(); };
            var menu = new MenuFlyout();
            menu.Items.Add(open);
            menu.Items.Add(_pauseTrackingItem);
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

    /// <summary>Stops or restarts the tracker in place, without closing the window —
    /// the tray's "Pause tracking" item. Mirrors RestartTracker's own stop/(re)start
    /// halves; resuming builds a fresh ActivityTracker the same way Settings-save
    /// already does, rather than trying to resurrect the paused one's in-flight state.</summary>
    private void TogglePauseTracking()
    {
        if (_pauseTrackingItem is null) return;
        if (Tracker is { Running: true })
        {
            Tracker.Stop();
            _trackingPaused = true;
            _pauseTrackingItem.Text = "Resume tracking";
            ActivityText.Text = "Paused";
            // The dot and window-name line used to keep showing whatever they last displayed
            // while tracking was live, right next to a label saying it's paused — a mixed
            // signal for a feature whose whole point is confirming tracking actually stopped
            // (2026-07-23 UX re-audit). Reset both to the same neutral/blank state UpdatePill
            // starts from before the first poll.
            ActivityDot.Fill = (Brush)Application.Current.Resources[CategoryStyle.BrushKey(DiaryCategory.Neutral)];
            ActivityWindow.Text = "";
        }
        else
        {
            _trackingPaused = false;
            StartTracker();
            _pauseTrackingItem.Text = "Pause tracking";
        }
        RaiseTrackingStateChanged();
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

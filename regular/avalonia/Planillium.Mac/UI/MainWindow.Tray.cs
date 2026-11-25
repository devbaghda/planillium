using Avalonia.Controls;
using Planillium.App.Services;

namespace Planillium.App;

// Menu-bar icon (macOS status item). Closing the window hides it; Quit really quits.
public sealed partial class MainWindow
{
    private TrayIcon? _tray;
    private NativeMenuItem? _pauseTrackingItem;
    private bool _trackingPaused;

    private void InitTray()
    {
        try
        {
            var open = new NativeMenuItem($"Open {AppInfo.DisplayName}");
            open.Click += (_, _) => ShowFromTray();
            _pauseTrackingItem = new NativeMenuItem("Pause tracking");
            _pauseTrackingItem.Click += (_, _) => TogglePauseTracking();
            var quit = new NativeMenuItem("Quit (stops tracking)");
            quit.Click += (_, _) => { _reallyClose = true; Close(); (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown(); };
            var menu = new NativeMenu();
            menu.Items.Add(open); menu.Items.Add(_pauseTrackingItem); menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(quit);
            _tray = new TrayIcon { ToolTipText = AppInfo.DisplayName, Menu = menu, Icon = LoadTrayIcon(NotificationCenter.HasUnread), IsVisible = true };
            _tray.Clicked += (_, _) => ShowFromTray();
            NotificationCenter.UnreadChanged += OnUnreadChanged;
        }
        catch (Exception ex)
        {
            Log.Error("InitTray (window close will quit instead)", ex);
            _tray = null; _reallyClose = true;
        }
    }

    private static WindowIcon? LoadTrayIcon(bool badged)
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://Planillium.Mac/Assets/icon.png"));
            var bmp = new Avalonia.Media.Imaging.Bitmap(stream);
            var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(44, 44), new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            {
                ctx.DrawImage(bmp, new Rect(0, 0, bmp.Size.Width, bmp.Size.Height), new Rect(0, 0, 44, 44));
                if (badged)
                {
                    ctx.DrawEllipse(Avalonia.Media.Brushes.White, null, new Point(35, 35), 9, 9);
                    ctx.DrawEllipse(new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(224, 43, 43)), null, new Point(35, 35), 7.5, 7.5);
                }
            }
            return new WindowIcon(rtb);
        }
        catch (Exception ex) { Log.Error("LoadTrayIcon", ex); return null; }
    }

    private void OnUnreadChanged()
    {
        if (_tray is null) return;
        _dq.TryEnqueue(() => { try { _tray.Icon = LoadTrayIcon(NotificationCenter.HasUnread); } catch (Exception ex) { Log.Error("OnUnreadChanged", ex); } });
    }

    private void DisposeTray()
    {
        NotificationCenter.UnreadChanged -= OnUnreadChanged;
        _tray?.Dispose();
    }

    private void TogglePauseTracking()
    {
        if (_pauseTrackingItem is null) return;
        if (Tracker is { Running: true })
        {
            Tracker.Stop();
            _trackingPaused = true;
            _pauseTrackingItem.Header = "Resume tracking";
            ActivityText.Text = "Paused";
            ActivityDot.Fill = Ui.Brush(CategoryStyle.BrushKey(DiaryCategory.Neutral));
            ActivityWindow.Text = "";
        }
        else
        {
            _trackingPaused = false;
            StartTracker();
            _pauseTrackingItem.Header = "Pause tracking";
        }
        RaiseTrackingStateChanged();
    }

    public void HideToTray() => Hide();

    private void ShowFromTray()
    {
        _dq.TryEnqueue(() =>
        {
            try { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
            catch (Exception ex) { Log.Error("MainWindow.ShowFromTray", ex); }
        });
    }
}

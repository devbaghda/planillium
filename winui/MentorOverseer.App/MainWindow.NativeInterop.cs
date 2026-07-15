using MentorOverseer.App.Services;

namespace MentorOverseer.App;

// Window opacity and the min-size WndProc hook — see MainWindow.xaml.cs for
// the file split. Kept together since both are raw Win32 interop against
// this window's own HWND.
public sealed partial class MainWindow
{
    // ── window opacity ────────────────────────────────────────────────────

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
    private const int GwlExstyle = -20;
    private const long WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x2;

    /// <summary>Whole-window opacity (Settings slider), 40–100 %. At 100 the
    /// layered style is removed so DWM effects (Mica) run untouched.</summary>
    public void ApplyOpacity(int percent)
    {
        try
        {
            percent = Math.Clamp(percent, 40, 100);
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var exStyle = GetWindowLongPtrW(hwnd, GwlExstyle).ToInt64();
            if (percent >= 100)
            {
                if ((exStyle & WsExLayered) != 0)
                    SetWindowLongPtrW(hwnd, GwlExstyle, new IntPtr(exStyle & ~WsExLayered));
                return;
            }
            if ((exStyle & WsExLayered) == 0)
                SetWindowLongPtrW(hwnd, GwlExstyle, new IntPtr(exStyle | WsExLayered));
            SetLayeredWindowAttributes(hwnd, 0, (byte)(percent * 255 / 100), LwaAlpha);
        }
        catch (Exception ex)
        {
            Log.Error("ApplyOpacity", ex);
        }
    }

    // ── minimum window size ──────────────────────────────────────────────
    //
    // WinAppSDK 1.5's OverlappedPresenter has no PreferredMinimumSize yet, so
    // an unresizable-by-us window could be dragged down to a few pixels and
    // wedge NavigationView/page content into a broken state — read by the
    // user as "resize doesn't work well." WM_GETMINMAXINFO is the native way
    // to enforce a floor: the OS itself refuses to drag past it, so it's as
    // smooth as any other window's resize (no fighting the live drag).

    private const int MinWidthDip = 900;
    private const int MinHeightDip = 600;
    private const int GwlpWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;

    // Windows session lock/unlock notifications (2026-07-09 audit finding
    // #11) — piggybacks on the same WndProc subclass hook the min-size fix
    // already installs, rather than adding a second one.
    private const uint WmWtsSessionChange = 0x02B1;
    private const int WtsSessionLock = 0x7;
    // Fast User Switching (another user takes the console) and an active RDP
    // session dropping — both leave this session just as unattended as a
    // lock does, but neither raises WTS_SESSION_LOCK, so the tracker kept
    // attributing elapsed time to whatever was in the foreground for up to
    // the full idle threshold after either one (2026-07-14 round-6 audit
    // finding #14 — the same gap WtsSessionLock was added to close, just
    // two more triggers of it).
    private const int WtsConsoleDisconnect = 0x2;
    private const int WtsRemoteDisconnect = 0x4;
    private const int NotifyForThisSession = 0;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _origWndProc;
    private IntPtr _hwnd;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);
    [System.Runtime.InteropServices.DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    private void InstallMinSizeHook()
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _wndProcDelegate = (hWnd2, msg, wParam, lParam) =>
        {
            if (msg == WmGetMinMaxInfo)
            {
                var scale = GetDpiForWindow(hWnd2) / 96.0;
                var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MinMaxInfo>(lParam);
                mmi.ptMinTrackSize.X = (int)(MinWidthDip * scale);
                mmi.ptMinTrackSize.Y = (int)(MinHeightDip * scale);
                System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }
            // Session lock (Win+L, screen-saver): tell the tracker to close
            // out the current session right away instead of waiting out the
            // full idle threshold before noticing the user is gone
            // (2026-07-09 audit finding #11). Still forwarded to the
            // original WndProc below — this is observation, not a message
            // we need to override the default handling of.
            if (msg == WmWtsSessionChange && wParam.ToInt32() is WtsSessionLock or WtsConsoleDisconnect or WtsRemoteDisconnect)
                Tracker?.NotifySessionLocked();
            return CallWindowProcW(_origWndProc, hWnd2, msg, wParam, lParam);
        };
        _origWndProc = SetWindowLongPtrW(_hwnd, GwlpWndProc,
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        if (!WTSRegisterSessionNotification(_hwnd, NotifyForThisSession))
            Log.Error("InstallMinSizeHook.WTSRegisterSessionNotification",
                new InvalidOperationException(
                    $"Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}"));
    }

    private void SaveWindowSize()
    {
        try
        {
            var s = StateService.Load();
            s.WindowWidth = Math.Max(AppWindow.Size.Width, MinWidthDip);
            s.WindowHeight = Math.Max(AppWindow.Size.Height, MinHeightDip);
            StateService.Save(s);
        }
        catch (Exception ex)
        {
            Log.Error("SaveWindowSize", ex);
        }
    }
}

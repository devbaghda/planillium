using System.Runtime.InteropServices;
using System.Text;

namespace Planillium.App.Services;

/// <summary>
/// Every Win32 call the activity tracker makes, and nothing else: which window is in front,
/// and how long since the user last touched the machine.
///
/// Split out of <see cref="ActivityTracker"/> on 2026-08-04 (2026-07-23 audit finding #8 — that
/// file was an 819-line God Object and is behind most of this app's real historical bugs). The
/// point of the boundary is that this file has *no state at all*: it reads what Windows says
/// right now and returns plain data. The tracker's state machine, which is where the bugs
/// actually live, no longer sits in the same file as the P/Invoke declarations.
///
/// These imports are all from user32.dll, which is a protected KnownDLL — deliberately noted
/// because a 2026-07-24 "DLL hijack hardening" change that called SetDefaultDllDirectories
/// broke WinUI's own native activation and left the app unable to start for three days
/// (reverted 2026-07-27). Nothing here needs a custom search order.
/// </summary>
internal static class NativeInput
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO lii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    /// <summary>The foreground window's raw title bar text and owning process id, read in one
    /// go. Returned together on purpose: they must describe the same window, and reading them
    /// through two separate calls invites the foreground changing in between.</summary>
    internal readonly record struct ForegroundWindowInfo(string RawTitle, uint ProcessId);

    internal static ForegroundWindowInfo Foreground()
    {
        var hwnd = GetForegroundWindow();
        var len = GetWindowTextLength(hwnd);
        var sb = new StringBuilder(len + 1);
        if (len > 0) GetWindowText(hwnd, sb, len + 1);
        GetWindowThreadProcessId(hwnd, out var pid);
        return new ForegroundWindowInfo(sb.ToString(), pid);
    }

    /// <summary>Seconds since the last keyboard/mouse input, system-wide.</summary>
    internal static double IdleSeconds()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        return (Environment.TickCount - (int)lii.dwTime) / 1000.0;
    }
}

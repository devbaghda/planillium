using System.Runtime.InteropServices;

namespace Planillium.App.Services;

/// <summary>
/// Mac version of the Windows NativeInput: which window is in front, and seconds since the last
/// keyboard/mouse input. Window titles need the macOS "Accessibility" permission; without it the
/// app name is still read from the running process and the title is left out (never a crash).
/// The raw title is composed as "&lt;window title&gt; – &lt;app name&gt;" because Windows titles
/// already end with the app name and the shared rules (AppNames, learned rules) expect that shape.
/// </summary>
internal static class NativeInput
{
    private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(AppServices)] private static extern bool AXIsProcessTrusted();
    [DllImport(AppServices)] private static extern IntPtr AXUIElementCreateSystemWide();
    [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr objc_getClass(string name);
    [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr MsgSend(IntPtr target, IntPtr sel);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern int MsgSendInt(IntPtr target, IntPtr sel);

    /// <summary>Frontmost app's pid via NSWorkspace (never asks our own process through Accessibility,
    /// which crashes inside Avalonia's native window).</summary>
    private static int FrontmostPid()
    {
        try
        {
            var ws = MsgSend(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
            var app = MsgSend(ws, sel_registerName("frontmostApplication"));
            return app == IntPtr.Zero ? 0 : MsgSendInt(app, sel_registerName("processIdentifier"));
        }
        catch { return 0; }
    }
    [DllImport(AppServices)] private static extern IntPtr AXUIElementCreateApplication(int pid);
    [DllImport(AppServices)] private static extern int AXUIElementCopyAttributeValue(IntPtr el, IntPtr attr, out IntPtr value);
    [DllImport(AppServices)] private static extern int AXUIElementGetPid(IntPtr el, out int pid);
    [DllImport(AppServices)] private static extern double CGEventSourceSecondsSinceLastEventType(int state, uint eventType);
    [DllImport(CoreFoundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string s, uint enc);
    [DllImport(CoreFoundation)] private static extern long CFStringGetLength(IntPtr s);
    [DllImport(CoreFoundation)] private static extern bool CFStringGetCString(IntPtr s, byte[] buf, long size, uint enc);
    [DllImport(CoreFoundation)] private static extern long CFGetTypeID(IntPtr o);
    [DllImport(CoreFoundation)] private static extern long CFStringGetTypeID();
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr o);

    private const uint Utf8 = 0x08000100;
    private static bool _warned;

    internal readonly record struct ForegroundWindowInfo(string RawTitle, uint ProcessId);

    /// <summary>True when macOS has granted this app the Accessibility permission.</summary>
    internal static bool HasPermission()
    {
        try { return AXIsProcessTrusted(); } catch { return false; }
    }

    private static string? Str(IntPtr cf)
    {
        if (cf == IntPtr.Zero || CFGetTypeID(cf) != CFStringGetTypeID()) return null;
        var n = CFStringGetLength(cf) * 4 + 1;
        var buf = new byte[n];
        return CFStringGetCString(cf, buf, n, Utf8)
            ? System.Text.Encoding.UTF8.GetString(buf, 0, Array.IndexOf(buf, (byte)0)) : null;
    }

    private static IntPtr Attr(IntPtr el, string name)
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, name, Utf8);
        try { return AXUIElementCopyAttributeValue(el, key, out var v) == 0 ? v : IntPtr.Zero; }
        finally { CFRelease(key); }
    }

    internal static ForegroundWindowInfo Foreground()
    {
        if (!HasPermission())
        {
            if (!_warned) { _warned = true; Log.Warn("NativeInput.Foreground", "Accessibility permission missing - window titles are not recorded"); }
            return new ForegroundWindowInfo("", 0);
        }
        IntPtr sys = IntPtr.Zero, app = IntPtr.Zero, win = IntPtr.Zero;
        try
        {
            var pid = FrontmostPid();
            if (pid <= 0) return new ForegroundWindowInfo("", 0);
            var appName = "";
            try { using var p = System.Diagnostics.Process.GetProcessById(pid); appName = p.ProcessName; } catch { }
            string? title = null;
            if (pid != Environment.ProcessId)
            {
                app = AXUIElementCreateApplication(pid);
                win = Attr(app, "AXFocusedWindow");
            }
            if (win != IntPtr.Zero)
            {
                var t = Attr(win, "AXTitle");
                title = Str(t);
                if (t != IntPtr.Zero) CFRelease(t);
            }
            var raw = string.IsNullOrWhiteSpace(title) ? appName
                    : string.IsNullOrEmpty(appName) ? title! : $"{title} – {appName}";
            return new ForegroundWindowInfo(raw, (uint)pid);
        }
        catch (Exception ex)
        {
            Log.Error("NativeInput.Foreground", ex);
            return new ForegroundWindowInfo("", 0);
        }
        finally
        {
            foreach (var p in new[] { win, app, sys }) if (p != IntPtr.Zero) CFRelease(p);
        }
    }

    /// <summary>Seconds since the last keyboard/mouse input, system-wide (no permission needed).</summary>
    internal static double IdleSeconds()
    {
        try { return CGEventSourceSecondsSinceLastEventType(0, uint.MaxValue); }
        catch { return 0; }
    }
}

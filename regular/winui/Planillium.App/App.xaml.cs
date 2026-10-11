using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Planillium.App.Services;

namespace Planillium.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    // Held for the process lifetime — same single-instance pattern the Python
    // app uses (its mutex fix, 2026-07-02); two instances would double-track.
    private static Mutex? _instanceMutex;

    public App()
    {
        InitializeComponent();

        // REMOVED (2026-07-27): 2026-07-24 audit finding #10 added a SetDefaultDllDirectories
        // call here as defense-in-depth against DLL-hijacking via the legacy CWD/PATH search
        // order — but the app was never actually relaunched again after that fix shipped, so it
        // sat untested for 3 days. Root-caused 2026-07-27 after the user reported the app
        // wouldn't start: calling SetDefaultDllDirectories AT ALL — regardless of which
        // directory flags are passed — breaks WinRT's native activation of the bundled Windows
        // App SDK/WinUI3 DLLs (Microsoft.UI.Xaml.dll etc.) on this machine, surfacing as an
        // unrecoverable "Cannot locate resource from
        // ms-appx:///Microsoft.UI.Xaml/Themes/themeresources.xaml" crash on literally every
        // launch. Confirmed by git-bisecting a clean Release build of the commit before this one
        // (launches fine) against this commit (crashes) and by testing multiple flag
        // combinations (still crashes) — the call itself is incompatible with this app's
        // self-contained WinUI3 activation path, not just a missing flag. The finding's own
        // reasoning already noted every DLL this app actually loads via explicit DllImport
        // (user32/advapi32/wtsapi32) is a protected Windows KnownDLL, always resolved from the
        // real system folder regardless of search-path manipulation — so there was no real
        // hijack hole for this call to close in the first place; removing it is a straight
        // revert, not a tradeoff.

        // Global safety net: without these, one stray exception in an async
        // path is a silent process death with zero diagnostics.
        UnhandledException += (_, e) =>
        {
            Log.Error("UnhandledException", e.Exception);
            e.Handled = true;  // log-and-survive, matching the Python app's callback hook
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("AppDomain.UnhandledException", e.ExceptionObject as Exception
                ?? new Exception(e.ExceptionObject?.ToString() ?? "unknown"));
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Info($"{AppInfo.DisplayName} v{AppVersion.Current} starting");

        // Optional test-only isolation: MENTOR_INSTANCE_SUFFIX lets a build
        // running from a separate worktree/branch (e.g. this audit-fixes
        // testing environment) use its own single-instance mutex, so it can
        // run side by side with the real app instead of being silently
        // blocked and exiting. #if DEBUG-gated (round-5 audit finding #24) —
        // a real user running the shipped Release exe has no reason to run
        // two instances side by side, so this has no business being live there.
        var mutexName = AppInfo.SingleInstanceMutex;
#if DEBUG
        mutexName += Environment.GetEnvironmentVariable("MENTOR_INSTANCE_SUFFIX") is { Length: > 0 } suffix
            ? "_" + suffix : "";
#endif
        _instanceMutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            Log.Info("Second instance blocked by mutex — exiting.");
            Environment.Exit(0);
        }

        // MainWindow's constructor subscribes to AppNotificationManager
        // .NotificationInvoked (so a clicked toast can reopen the right
        // dialog) — that subscription must happen before Register() below,
        // matching Microsoft's documented ordering; subscribing after
        // Register() threw a COMException (0x80070490 "Element not found")
        // that crashed the app at launch on 2026-07-13.
        //
        // The constructor also resolves AppPaths.Root (config.json/plans/
        // the database) as one of its very first steps — if that folder
        // can't be found (a moved install, a bad MENTOR_ROOT), the
        // exception used to propagate out with nothing to catch it here,
        // leaving a process that already holds the single-instance mutex
        // alive with no window and no tray icon: invisible, unreachable
        // except via Task Manager, and blocking any real launch attempt.
        // A plain Win32 MessageBox needs no window/XamlRoot to exist, so it
        // works even when the thing that failed IS the window.
        MainWindow window;
        try
        {
            window = new MainWindow();
        }
        catch (Exception ex)
        {
            Log.Error("MainWindow construction failed — exiting", ex);
            MessageBoxW(IntPtr.Zero,
                $"{AppInfo.DisplayName} couldn't find its data folder and can't start.\n\n" +
                "If you moved the install, check that it's next to a folder containing " +
#if DEBUG
                "config.json and plans\\, or that MENTOR_ROOT points at one.\n\n" +
#else
                // MENTOR_ROOT is a Debug/test-only hook (see AppPaths.Root) — telling a real
                // Release user to set it would send them down a dead end (2026-07-24 audit
                // finding #9).
                "config.json and plans\\.\n\n" +
#endif
                $"Details: {ex.Message}",
                $"{AppInfo.DisplayName} — startup failed", MbIconError);
            _instanceMutex?.ReleaseMutex();
            Environment.Exit(1);
            return;
        }
        MainWindow = window;

        // Required before Show() for unpackaged apps — without it every toast
        // throws and focus alerts silently never appear (audit finding #3).
        try
        {
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            Log.Error("AppNotificationManager.Register", ex);
        }

        // --minimized (autostart at boot): start straight into the tray.
        // Same defense-in-depth as the constructor above: InitTray() already
        // ran inside the (now successfully-returned) constructor, so a
        // failure here would still leave a tray icon behind — but without a
        // catch, it'd otherwise reproduce the exact "alive, mutex held, no
        // way to bring the window up" failure one call later.
        try
        {
            if (Environment.GetCommandLineArgs().Contains("--minimized"))
                window.HideToTray();
            else
                window.Activate();
        }
        catch (Exception ex)
        {
            Log.Error("Post-construction HideToTray/Activate", ex);
        }
    }

    private const uint MbIconError = 0x10;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}

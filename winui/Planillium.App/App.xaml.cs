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

    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
    private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    public App()
    {
        InitializeComponent();

        // Every native DLL this app actually loads today (user32/advapi32/wtsapi32) is one of
        // Windows' own protected "KnownDLLs," always loaded from the real system folder
        // regardless of what's next to the exe — so there's no real DLL-hijack hole today. This
        // costs nothing and removes any doubt if a native dependency less inherently protected
        // is ever added later (2026-07-24 audit finding #10).
        SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);

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
                "If you moved the install, or set MENTOR_ROOT, check that it " +
                "points at a folder containing config.json and plans\\.\n\n" +
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

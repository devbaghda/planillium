using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    // Held for the process lifetime — same single-instance pattern the Python
    // app uses (its mutex fix, 2026-07-02); two instances would double-track.
    private static Mutex? _instanceMutex;

    public App()
    {
        InitializeComponent();

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
        Log.Info($"Planillium v{AppVersion.Current} starting");

        _instanceMutex = new Mutex(true, AppInfo.SingleInstanceMutex, out var createdNew);
        if (!createdNew)
        {
            Log.Info("Second instance blocked by mutex — exiting.");
            Environment.Exit(0);
        }

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

        var window = new MainWindow();
        MainWindow = window;
        // --minimized (autostart at boot): start straight into the tray.
        if (Environment.GetCommandLineArgs().Contains("--minimized"))
            window.HideToTray();
        else
            window.Activate();
    }
}

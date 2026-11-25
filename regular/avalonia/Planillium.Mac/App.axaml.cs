using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Planillium.App.Services;

namespace Planillium.Mac;

public partial class App : Application
{
    public static Window? MainWindow => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (AppPaths.MissingScratchFolder is { } missing)
            {
                Console.Error.WriteLine($"Planillium can't start: the data folder MENTOR_ROOT points at does not exist: {missing}");
                Environment.Exit(1);
            }
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                _ = AppPaths.Root;
                Log.Info($"{AppInfo.DisplayName} (Mac) starting");
                var win = new Planillium.App.MainWindow();
                desktop.MainWindow = win;
                if (Environment.GetCommandLineArgs().Contains("--minimized")) win.Hide();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Planillium can't start: {ex.Message}");
                Log.Error("Startup failed", ex);
                Environment.Exit(1);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}

using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Services;

namespace MentorOverseer.App;

// The notify-icon / tray menu — see MainWindow.xaml.cs for the file split.
public sealed partial class MainWindow
{
    private H.NotifyIcon.TaskbarIcon? _tray;

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

            _tray = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = AppInfo.DisplayName,
                Icon = new System.Drawing.Icon(
                    Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")),
                ContextFlyout = menu,
                ContextMenuMode = H.NotifyIcon.ContextMenuMode.SecondWindow,
                LeftClickCommand = openCommand,
            };
            _tray.ForceCreate();
        }
        catch (Exception ex)
        {
            // No tray = closing must really close, or the app becomes unkillable.
            Log.Error("InitTray (window close will quit instead)", ex);
            _tray = null;
            _reallyClose = true;
        }
    }

    public void HideToTray() => AppWindow.Hide();

    private void ShowFromTray()
    {
        _dq.TryEnqueue(() =>
        {
            AppWindow.Show();
            Activate();
        });
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// TickTick credentials + browser authorization, all inside this app.
/// Client ID goes to config.json; the secret and tokens go to Windows
/// Credential Manager only.
/// </summary>
public static class TickTickConnectDialog
{
    public static async Task<bool> ShowAsync(XamlRoot root)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = "From your app at developer.ticktick.com/manage. The app's OAuth " +
                   "redirect URL must be exactly:  http://localhost:8765/callback",
        });

        var idBox = new TextBox
        {
            Header = "Client ID",
            Text = ConfigService.TickTickClientId,
        };
        var secretBox = new PasswordBox
        {
            Header = "Client secret",
            Password = TickTickAuth.ClientSecret ?? "",
        };
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var busy = new ProgressRing { IsActive = false, Width = 20, Height = 20 };
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        statusRow.Children.Add(busy);
        statusRow.Children.Add(status);

        panel.Children.Add(idBox);
        panel.Children.Add(secretBox);
        panel.Children.Add(statusRow);

        var dialog = new ContentDialog
        {
            Title = "Connect TickTick",
            Content = panel,
            PrimaryButtonText = "Save & authorize",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };

        var connected = false;
        dialog.PrimaryButtonClick += async (d, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                args.Cancel = true;   // stay open until the flow finishes
                var id = idBox.Text.Trim();
                var secret = secretBox.Password.Trim();
                if (id.Length == 0 || secret.Length == 0)
                {
                    status.Text = "Both fields are required.";
                    return;
                }

                ConfigService.Mutate(cfg =>
                {
                    var tt = cfg["ticktick"] as System.Text.Json.Nodes.JsonObject
                             ?? new System.Text.Json.Nodes.JsonObject();
                    tt["client_id"] = id;
                    cfg["ticktick"] = tt;
                });
                if (!TickTickAuth.SaveClientSecret(secret))
                {
                    status.Text = "Couldn't save the secret to Windows Credential Manager.";
                    return;
                }

                busy.IsActive = true;
                d.IsPrimaryButtonEnabled = false;
                status.Text = "Waiting for the browser… approve access, then come back here.";
                var (ok, message) = await TickTickAuth.AuthorizeAsync();
                status.Text = message;
                busy.IsActive = false;
                d.IsPrimaryButtonEnabled = true;
                if (ok)
                {
                    connected = true;
                    d.PrimaryButtonText = "Done";
                    d.Hide();
                }
            }
            catch (Exception ex)
            {
                Log.Error("TickTickConnectDialog", ex);
                status.Text = "Unexpected error: " + ex.Message;
                busy.IsActive = false;
                d.IsPrimaryButtonEnabled = true;
            }
            finally { deferral.Complete(); }
        };

        await DialogGate.ShowAsync(dialog);
        return connected;
    }
}

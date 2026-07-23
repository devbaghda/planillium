using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

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
            // Reads TickTickAuth's own RedirectUri instead of retyping the URL as a
            // separate literal — the two used to be independent, so a future port change
            // would leave this instruction telling the user to register the wrong address
            // (2026-07-18 audit finding R10-05, the same "two copies of one fact" shape
            // round-5 finding #15 already fixed once for the app's own internal use of it).
            Text = "From your app at developer.ticktick.com/manage. The app's OAuth " +
                   $"redirect URL must be exactly:  {TickTickAuth.RedirectUri}",
        });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12,
            // What's actually being agreed to, stated plainly before the user clicks
            // "Save & authorize" — the dialog previously only showed OAuth setup
            // mechanics and never said what data this connects (audit finding #7).
            Text = "Connecting lets this app read your TickTick task titles, projects, and " +
                   "due dates, and mark tasks complete there — that data leaves this PC and " +
                   "goes to TickTick's servers and back.",
        });

        var idBox = new TextBox
        {
            Header = "Client ID",
            Text = ConfigService.TickTickClientId,
        };
        // Never redisplay a secret that's already saved — WinUI's PasswordBox
        // ships with a one-click "reveal" icon, so pre-filling it would let
        // the real secret be read straight off this dialog (round-4 audit
        // finding). Left blank on reopen; blank on save means "keep what's
        // already stored," only a fresh value overwrites it.
        var hasSavedSecret = TickTickAuth.ClientSecret is { Length: > 0 };
        var secretBox = new PasswordBox
        {
            Header = "Client secret",
            PlaceholderText = hasSavedSecret ? "Already saved — leave blank to keep it" : "",
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
                if (id.Length == 0 || (secret.Length == 0 && !hasSavedSecret))
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
                // Blank means "keep the already-saved secret" — only a typed
                // value overwrites Credential Manager.
                if (secret.Length > 0 && !TickTickAuth.SaveClientSecret(secret))
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
                status.Text = Log.Friendly("Something went wrong finishing the connection", ex);
                busy.IsActive = false;
                d.IsPrimaryButtonEnabled = true;
            }
            finally { deferral.Complete(); }
        };

        await DialogGate.ShowAsync(dialog);
        return connected;
    }
}

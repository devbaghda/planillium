using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Planillium.App.Dialogs;

/// <summary>
/// The one-time "what is sent to Anthropic" notice (ai-layer spec, D3). Built as an inline panel
/// that sits in the Ask Claude strip, not as its own ContentDialog: WinUI allows only one
/// ContentDialog open at a time, and Ask Claude is pressed inside Replace remaining tasks or Add
/// plan, which are themselves ContentDialogs. Same copy, same Accept/Decline outcomes.
/// </summary>
internal static class AiNoticeDialog
{
    /// <summary>Builds the notice. <paramref name="noticeBullet"/> is the one bullet for the
    /// dialog that is asking (Replace remaining tasks or Add plan). Accept and Decline are
    /// reported through the two callbacks; nothing is stored here.</summary>
    public static StackPanel Build(string noticeBullet, Action accept, Action decline)
    {
        var panel = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };

        var title = Body("Before Planillium asks Claude", semiBold: true, size: 12);
        var intro = Body("Planillium will send the prompt you can see in this window to Anthropic, the company " +
                         "behind Claude, using your own API key. Nothing else leaves your computer — no diary, " +
                         "scores, earnings, tags or settings.");
        var sentHeading = Body("What is sent", semiBold: true, size: 12);
        var bullet = Body("•  " + noticeBullet);
        var keyLine = Body("Your key is only used to sign the request. What you send is handled under " +
                           "Anthropic's own terms and privacy policy. Claude's reply changes nothing in " +
                           "Planillium until you confirm it yourself.");
        var once = Body("You will only be asked this once. You can always use the copy-and-paste way instead.",
            secondary: true, size: 11);

        var acceptBtn = new Button { Content = "Accept and continue", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        AutomationProperties.SetName(acceptBtn, "Accept and continue");
        acceptBtn.Click += (_, _) => accept();
        var declineBtn = new Button { Content = "Decline" };
        AutomationProperties.SetName(declineBtn, "Decline");
        declineBtn.Click += (_, _) => decline();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(acceptBtn);
        buttons.Children.Add(declineBtn);

        panel.Children.Add(title);
        panel.Children.Add(intro);
        panel.Children.Add(sentHeading);
        panel.Children.Add(bullet);
        panel.Children.Add(keyLine);
        panel.Children.Add(once);
        panel.Children.Add(buttons);
        return panel;
    }

    private static TextBlock Body(string text, bool semiBold = false, bool secondary = false, double size = 11.5)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = semiBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources[secondary
                ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush"],
        };
        AutomationProperties.SetName(block, text);
        return block;
    }
}

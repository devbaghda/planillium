using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Planillium.App.Views;

/// <summary>
/// "No active plans yet" panel — Today and Schedule each built this by hand
/// and had already drifted apart in wording ("No active plans yet." vs.
/// "No active plans.") before this fix, for the very first thing a new user
/// sees on either page (round-5 audit finding #19).
/// </summary>
public static class EmptyPlansState
{
    public static StackPanel Build(Page host, string blurb, Action onAdded)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 16, 0, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = "No active plans yet.",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        panel.Children.Add(new TextBlock
        {
            Text = blurb,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        var add = new Button
        {
            Content = "+ Add Plan",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin = new Thickness(0, 8, 0, 0),
        };
        add.Click += async (_, _) =>
        {
            if (await Dialogs.AddPlanDialog.ShowAsync(host)) onAdded();
        };
        panel.Children.Add(add);
        return panel;
    }
}

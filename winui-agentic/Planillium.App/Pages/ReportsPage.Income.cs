using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Income card builder for the Reports page.
public sealed partial class ReportsPage
{
    private static StackPanel IncomeCard(double sum, string periodName)
    {
        var card = new StackPanel { Spacing = 2 };
        var signWord = IncomeSign(sum);
        card.Children.Add(Caption($"{signWord} — {periodName}"));
        card.Children.Add(new TextBlock
        {
            Text = sum.ToString("F2"),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            Foreground = IncomeBrush(sum),
        });
        card.Children.Add(new TextBlock
        {
            Text = "€",
            FontSize = 13,
            Foreground = IncomeBrush(sum),
            Margin = new Microsoft.UI.Xaml.Thickness(0, -8, 0, 0),
        });
        return card;
    }
}

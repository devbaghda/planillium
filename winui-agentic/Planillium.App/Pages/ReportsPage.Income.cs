using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Income card builder for the Reports page.
public sealed partial class ReportsPage
{
    private static StackPanel IncomeCard(double sum, string periodName, int offMin)
    {
        var lost = sum < 0;

        var card = new StackPanel { Spacing = 2 };
        card.Children.Add(Caption($"{(lost ? "UNEARNED" : "EXTRA")} INCOME — {periodName}"));
        card.Children.Add(new TextBlock
        {
            Text = MainWindow.FormatEur(sum),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            Foreground = IncomeBrush(sum),
        });
        var timeWord = _period switch
        {
            ReportPeriod.Day => "today",
            ReportPeriod.Week => "this week",
            ReportPeriod.Month => "this month",
            ReportPeriod.Year => "this year",
            _ => "this period",
        };
        var verb = lost ? "lost" : "gained";
        card.Children.Add(Dim(
            $"You've {verb} {MainWindow.FormatEur(Math.Abs(sum))} {timeWord}, based on a potential " +
            $"{MainWindow.FormatEur(ConfigService.PotentialMonthlyIncomeEur())}/month."));
        if (offMin > 0)
        {
            var perHour = sum / (offMin / 60.0);
            card.Children.Add(Dim($"That's {MainWindow.FormatEur(perHour)} per off-plan hour."));
        }
        return card;
    }
}

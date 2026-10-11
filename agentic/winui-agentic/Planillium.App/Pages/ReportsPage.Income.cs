using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Income card builder for the Reports page.
public sealed partial class ReportsPage
{
    /// <summary>Signed hour value: magnitude from HourValueEur, sign following the period income sum.
    /// When sum is 0, returns positive (no loss/gain to colour it).</summary>
    private static double SignedHourValue(double incomeSum)
    {
        return SignHelper.SignedHourValue(incomeSum, IncomeService.HourValueEur());
    }

    private static StackPanel IncomeCard(double net, double credit, string periodName, int offMin, double earned = 0)
    {
        // Round to cents to avoid float dust
        var roundedNet = Math.Round(net, 2);
        var roundedCredit = Math.Round(credit, 2);

        var card = new StackPanel { Spacing = 2 };

        // Determine caption and main text based on net and credit
        string caption, text;
        if (roundedNet < 0)
        {
            caption = $"UNEARNED INCOME — {periodName}";
            var timeWord = _period switch
            {
                ReportPeriod.Day => "today",
                ReportPeriod.Week => "this week",
                ReportPeriod.Month => "this month",
                ReportPeriod.Year => "this year",
                _ => "this period",
            };
            text = $"You've lost {MainWindow.FormatEur(Math.Abs(roundedNet))} {timeWord}, based on a potential " +
                   $"{MainWindow.FormatEur(ConfigService.PotentialMonthlyIncomeEur())}/month.";
        }
        else if (roundedNet > 0)
        {
            caption = $"EXTRA INCOME — {periodName}";
            var timeWord = _period switch
            {
                ReportPeriod.Day => "today",
                ReportPeriod.Week => "this week",
                ReportPeriod.Month => "this month",
                ReportPeriod.Year => "this year",
                _ => "this period",
            };
            text = $"You've gained {MainWindow.FormatEur(roundedNet)} {timeWord}, based on a potential " +
                   $"{MainWindow.FormatEur(ConfigService.PotentialMonthlyIncomeEur())}/month.";
        }
        else // roundedNet == 0
        {
            if (roundedCredit > 0)
            {
                caption = $"NO INCOME LOST — {periodName}";
                text = "Plan tasks you completed earned back all of it.";
            }
            else
            {
                caption = $"UNEARNED INCOME — {periodName}"; // Fallback for zero with no credit
                var timeWord = _period switch
                {
                    ReportPeriod.Day => "today",
                    ReportPeriod.Week => "this week",
                    ReportPeriod.Month => "this month",
                    ReportPeriod.Year => "this year",
                    _ => "this period",
                };
                text = $"You've lost {MainWindow.FormatEur(0)} {timeWord}, based on a potential " +
                       $"{MainWindow.FormatEur(ConfigService.PotentialMonthlyIncomeEur())}/month.";
            }
        }

        card.Children.Add(Caption(caption));
        card.Children.Add(new TextBlock
        {
            Text = MainWindow.FormatEur(roundedNet),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            Foreground = IncomeBrush(roundedNet),
        });
        card.Children.Add(Dim(text));

        // Add credit line if credit > 0
        if (roundedCredit > 0)
        {
            var timeWord = _period switch
            {
                ReportPeriod.Day => "today",
                ReportPeriod.Week => "this week",
                ReportPeriod.Month => "this month",
                ReportPeriod.Year => "this year",
                _ => "this period",
            };
            card.Children.Add(Dim($"Includes {MainWindow.FormatEur(roundedCredit)} earned back by completing plan tasks ({timeWord})."));
        }

        if (earned > 0)
            card.Children.Add(Dim($"Includes {MainWindow.FormatEur(earned)} earned from paid work."));

        // Per-hour line uses the net
        if (offMin > 0)
        {
            var perHour = SignedHourValue(roundedNet);
            card.Children.Add(Dim($"That's {MainWindow.FormatEur(perHour)} per off-plan hour."));
        }
        return card;
    }

    /// <summary>Paid-work earnings for the period (payable-tagged diary time x each tag's hourly
    /// value, user decision 2026-10-01). Also counted inside the income figure above; shown
    /// separately so the money actually earned is visible on its own.</summary>
    private static StackPanel EarningsCard(List<IncomeService.PaidEarning> earnings, string periodName)
    {
        var total = earnings.Sum(e => e.Eur);
        var card = new StackPanel { Spacing = 2 };
        card.Children.Add(Caption($"EARNINGS FROM PAID WORK — {periodName}"));
        card.Children.Add(new TextBlock
        {
            Text = MainWindow.FormatEur(total),
            FontSize = 44,
            FontWeight = FontWeights.Bold,
            Foreground = IncomeBrush(total),
        });
        if (earnings.Count == 0)
            card.Children.Add(Dim("No time tagged with a payable tag in this period. Set payable tags and rates in Settings → Income."));
        foreach (var e in earnings)
            card.Children.Add(Dim(
                $"{e.Tag.Label}: {ReportData.FmtHours(e.Minutes)} × {MainWindow.FormatEur(e.Tag.HourlyEur)}/h " +
                $"(monthly {MainWindow.FormatEur(e.Tag.MonthlyEur)} ÷ {IncomeService.WorkingHoursPerMonth} h) = {MainWindow.FormatEur(e.Eur)}"));
        return card;
    }
}

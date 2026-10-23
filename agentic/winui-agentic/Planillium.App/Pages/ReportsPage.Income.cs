using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Income card builder for the Reports page.
public sealed partial class ReportsPage
{
    /// <summary>Below this width (DIP) the two strip columns stack instead of sitting side by side.</summary>
    private const double IncomeStripStackWidth = 560;

    /// <summary>Two figures for the period, side by side: the unchanged headline income (it already
    /// contains paid work) and the paid-work total, labelled "of which" so they are not added up.
    /// No calculation here -- both values come from the caller. Layout and copy: SPEC Design D1.</summary>
    private static Grid IncomeSummaryStrip(double incomeNet, List<IncomeService.PaidEarning> earnings, string periodName)
    {
        var roundedNet = Math.Round(incomeNet, 2);
        var paidWork = earnings.Sum(e => e.Eur);

        var left = SummaryColumn(
            $"INCOME LOST OR GAINED — {periodName}",
            MainWindow.FormatEur(roundedNet), IncomeBrush(roundedNet),
            "Total for the period. Paid work is already inside this figure.");
        var right = SummaryColumn(
            $"OF WHICH PAID WORK — {periodName}",
            MainWindow.FormatEur(paidWork), IncomeBrush(paidWork),
            earnings.Count == 0
                ? "No time tagged with a payable tag in this period."
                : "Part of the total on the left, not added to it.");

        var divider = new Rectangle
        {
            Width = 1,
            VerticalAlignment = VerticalAlignment.Stretch,
            Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        };

        var grid = new Grid { ColumnSpacing = 16 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(left);
        grid.Children.Add(divider);
        grid.Children.Add(right);

        void Layout(bool stacked)
        {
            grid.ColumnDefinitions.Clear();
            if (stacked)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowSpacing = 12;
                divider.Visibility = Visibility.Collapsed;
                Grid.SetRow(left, 0); Grid.SetColumn(left, 0);
                Grid.SetRow(divider, 0); Grid.SetColumn(divider, 0);
                Grid.SetRow(right, 1); Grid.SetColumn(right, 0);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowSpacing = 0;
                divider.Visibility = Visibility.Visible;
                Grid.SetRow(left, 0); Grid.SetColumn(left, 0);
                Grid.SetRow(divider, 0); Grid.SetColumn(divider, 1);
                Grid.SetRow(right, 0); Grid.SetColumn(right, 2);
            }
        }

        var stacked = false;
        Layout(stacked);
        grid.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < IncomeStripStackWidth;
            if (narrow == stacked) return;
            stacked = narrow;
            Layout(stacked);
        };
        return grid;
    }

    private static StackPanel SummaryColumn(string caption, string figure, Brush figureBrush, string note)
    {
        var column = new StackPanel { Spacing = 2 };
        column.Children.Add(Caption(caption));
        column.Children.Add(new TextBlock
        {
            Text = figure,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = figureBrush,
        });
        column.Children.Add(new TextBlock
        {
            Text = note,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        return column;
    }

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

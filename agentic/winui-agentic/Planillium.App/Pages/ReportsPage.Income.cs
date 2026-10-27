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
}

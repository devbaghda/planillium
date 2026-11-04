using Planillium.App.Services;

namespace Planillium.App.Pages;

// Income helper for the Reports page (the summary strip was replaced by table columns, B1i).
public sealed partial class ReportsPage
{
    /// <summary>Signed hour value: magnitude from HourValueEur, sign following the period income sum.
    /// When sum is 0, returns positive (no loss/gain to colour it).</summary>
    private static double SignedHourValue(double incomeSum)
    {
        return SignHelper.SignedHourValue(incomeSum, IncomeService.HourValueEur());
    }
}

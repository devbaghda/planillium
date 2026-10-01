namespace Planillium.App.Services;

/// <summary>
/// Pure logic helper for sign-based operations — extracted so it can be unit-tested
/// without WinUI dependencies.
/// </summary>
public static class SignHelper
{
    /// <summary>
    /// Returns a signed hour value based on the sum of income.
    /// Magnitude is determined by IncomeService.HourValueEur(); sign follows the sum.
    /// When sum is 0 (no gain or loss), returns positive (neutral colour).
    /// </summary>
    /// <param name="incomeSum">The sum to determine sign (negative = loss, positive/zero = gain).</param>
    /// <param name="hourValueEur">The base hour value in EUR (typically from IncomeService.HourValueEur()).</param>
    /// <returns>The signed hour value: -hourValueEur if sum < 0, else +hourValueEur.</returns>
    public static double SignedHourValue(double incomeSum, double hourValueEur)
    {
        return incomeSum < 0 ? -hourValueEur : hourValueEur;
    }
}

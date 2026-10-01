using System.Globalization;

namespace Planillium.App.Services;

/// <summary>
/// Pure static insight generation logic — insights text and the ratio calculation that
/// gates whether the ratio insight fires. Separate from ReportExport so it can be tested
/// independently without WinUI dependencies.
/// </summary>
public static class InsightRules
{
    /// <summary>Format a euro amount as "€X.XX" or "-€X.XX".</summary>
    internal static string FormatEur(double v) =>
        (v < 0 ? "-€" : "€") + Math.Abs(v).ToString("N2", CultureInfo.CurrentCulture);

    /// <summary>
    /// Generate insight suggestions for a report period. <paramref name="period"/> picks the
    /// phrase ("today"/"this week"/"this month"/"this year").
    /// </summary>
    public static List<string> Suggestions(int on, int off, int neutral,
        List<(string Label, int Minutes)> distractions, double hourValueEur, ReportPeriod period = ReportPeriod.Week)
    {
        var phrase = ReportData.PeriodName(period).ToLowerInvariant();
        var hints = new List<string>();

        // First sentence: off-plan time with EUR equivalent.
        if (off > 120)
        {
            var offHours = ReportData.FmtHours(off);
            var offEur = FormatEur(off / 60.0 * hourValueEur);
            hints.Add($"You spent {offHours} ({offEur}) off-plan {phrase}. " +
                      "Try blocking distracting apps during working hours.");
        }

        // Ratio insight: off-plan time as a fraction of on-plan + neutral.
        var denominator = on + neutral;
        if (denominator > 0 && (double)off / denominator > 0.4)
            hints.Add("Off-plan time is over 40% of your productive time. " +
                      "Your goal needs tighter focus blocks.");

        // Biggest-distraction sentence with EUR equivalent.
        if (distractions.Count > 0)
        {
            var top = distractions[0];
            var topHours = ReportData.FmtHours(top.Minutes);
            var topEur = FormatEur(top.Minutes / 60.0 * hourValueEur);
            hints.Add($"'{top.Label}' is your biggest distraction — " +
                      $"{topHours} ({topEur}) off-plan {phrase}.");
        }

        // Fallback if no other hint fired.
        if (hints.Count == 0)
            hints.Add($"No major distraction patterns detected {phrase}. Keep going!");

        return hints;
    }
}

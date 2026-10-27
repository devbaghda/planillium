using System.Globalization;

namespace Planillium.App.Services;

/// <summary>Whole-cent arithmetic for the Reports tables (B1i): rows are rounded by running total
/// so the cells shown always add up, to the cent, to the Total cell shown.</summary>
internal static class EurCents
{
    /// <summary>Whole cents exactly as the EUR display rounds <paramref name="v"/> ("F2" and "N2" round the same way).</summary>
    internal static long Of(double v) =>
        (long)(decimal.Parse(v.ToString("F2", CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture) * 100m);

    /// <summary>Row values as whole cents that add up exactly to <paramref name="totalCents"/>:
    /// cum_i = Of(v_0 + ... + v_i) for i &lt; n-1; cum_(n-1) = totalCents; row_i = cum_i - cum_(i-1).
    /// Each row stays within one cent of its own exact value; the last row absorbs any residue.</summary>
    internal static long[] SplitRows(IReadOnlyList<double> rowValues, long totalCents)
    {
        var n = rowValues.Count;
        var rows = new long[n];
        double running = 0;
        long prev = 0;
        for (var i = 0; i < n; i++)
        {
            running += rowValues[i];
            var cum = i == n - 1 ? totalCents : Of(running);
            rows[i] = cum - prev;
            prev = cum;
        }
        return rows;
    }
}

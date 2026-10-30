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

    /// <summary>G12: whole cents of an exact amount, half a cent rounded away from zero.</summary>
    internal static long OfExact(decimal v) => (long)(Math.Round(v, 2, MidpointRounding.AwayFromZero) * 100m);

    /// <summary>G12: cents for any run of days, cut from ONE running total that starts at a fixed day (the first day
    /// in <c>daily</c>; Reports passes every day from the income ledger's start). A span's cents are
    /// cents(total through its last day) - cents(total through the day before its first day), so the same days give the
    /// same cents in every view (a week in the Week view, as a Month row and inside a Year row), consecutive spans add
    /// up exactly to the span they make up, and each span stays within one cent of its exact value. The days are
    /// added as decimals, so the running total does not depend on the order or grouping of the additions.</summary>
    internal sealed class Running
    {
        private readonly DateOnly _first;
        private readonly Dictionary<DateOnly, long> _through = new();

        internal Running(IReadOnlyDictionary<DateOnly, double> daily)
        {
            _first = daily.Count == 0 ? DateOnly.MaxValue : daily.Keys.Min();
            decimal running = 0;
            for (var d = _first; daily.TryGetValue(d, out var v); d = d.AddDays(1))
            {
                running += (decimal)v;
                _through[d] = OfExact(running);
            }
        }

        /// <summary>Cents of the days <paramref name="from"/>..<paramref name="to"/> inclusive. A day that is not in
        /// <c>daily</c> throws (a missing day must fail loudly, never read as 0), except days before the start.</summary>
        internal long Span(DateOnly from, DateOnly to) => Through(to) - Through(from.AddDays(-1));

        private long Through(DateOnly d) => d < _first ? 0 : _through[d];
    }

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

namespace Planillium.App.Services;

/// <summary>
/// Converts app breakdown data into pie-chart slices with dominant categories, angles, and drill info.
/// Pure logic, no WinUI dependencies — fully testable without UI framework.
/// </summary>
public static class PieSlices
{
    /// <summary>Maximum number of apps shown individually before remainder is merged into "Other".</summary>
    private const int MaxSlices = 6;

    /// <summary>Minimum size as a fraction of level total. Smaller slices are merged into "Other".</summary>
    private const double MinSliceFraction = 0.03;

    /// <summary>Describes a single wedge in the pie, with all info needed to draw and interact with it.</summary>
    public sealed record Slice(
        string Name,                        // app name, or "Other (N apps)" / "Other (N items)", or "(no detail)"
        int Total,                          // total minutes for this slice
        string DominantCategory,            // category with most minutes (ties broken by ReportOrder)
        double StartAngle,                  // degrees from 12 o'clock, clockwise
        double SweepAngle,                  // degrees for this slice's arc
        bool IsDrillable,                   // slice has children (drillable in the UI)
        double Percentage,                  // of the level's total
        int On, int Off, int Neutral, int Paid, int Idle);  // category breakdown

    /// <summary>Build pie slices from app-usage data at the current drill level.
    /// Applies top-6 limit, 3% merge rule, and calculates angles for arc drawing.</summary>
    public static List<Slice> BuildSlices(List<(string Name, ReportData.AppUsage Usage)> items, int levelTotal)
    {
        if (levelTotal <= 0 || items.Count == 0)
            return [];

        var result = new List<Slice>();
        var slicesToMerge = new List<(string Name, ReportData.AppUsage Usage)>();
        var shownCount = 0;

        // Step 1: keep top MaxSlices that are above MinSliceFraction, or keep exactly top 6 if 7+ items exist
        // and only low-value items are below 3% threshold
        foreach (var (name, usage) in items)
        {
            if (shownCount < MaxSlices)
            {
                var fraction = usage.Total / (double)levelTotal;
                if (fraction >= MinSliceFraction || (items.Count == MaxSlices + 1 && shownCount < MaxSlices))
                {
                    // This item is shown individually
                    result.Add(new Slice(
                        Name: name,
                        Total: usage.Total,
                        DominantCategory: DominantCategory(usage),
                        StartAngle: 0,  // Will be set in step 3
                        SweepAngle: 0,
                        IsDrillable: usage.Subs?.Count > 0,
                        Percentage: fraction * 100,
                        On: usage.On,
                        Off: usage.Off,
                        Neutral: usage.Neutral,
                        Paid: usage.Paid,
                        Idle: usage.Idle));
                    shownCount++;
                }
                else
                    slicesToMerge.Add((name, usage));
            }
            else
                slicesToMerge.Add((name, usage));
        }

        // Step 2: merge remainder into "Other" (if any)
        if (slicesToMerge.Count > 0)
        {
            var otherTotal = slicesToMerge.Sum(x => x.Usage.Total);
            var otherOn = slicesToMerge.Sum(x => x.Usage.On);
            var otherOff = slicesToMerge.Sum(x => x.Usage.Off);
            var otherNeutral = slicesToMerge.Sum(x => x.Usage.Neutral);
            var otherPaid = slicesToMerge.Sum(x => x.Usage.Paid);
            var otherIdle = slicesToMerge.Sum(x => x.Usage.Idle);

            var otherName = slicesToMerge.Count == 1
                ? slicesToMerge[0].Name  // Single leftover shown as itself, not merged
                : $"Other ({slicesToMerge.Count} apps)";

            // For the merged "Other" slice, preserve the sub-items so it can be drilled into.
            // Convert the merged list into a Subs dictionary (sub-name => usage).
            SortedDictionary<string, ReportData.AppUsage>? otherSubs;
            if (slicesToMerge.Count > 1)
            {
                otherSubs = new SortedDictionary<string, ReportData.AppUsage>();
                foreach (var (name, usage) in slicesToMerge)
                {
                    otherSubs[name] = usage;
                }
            }
            else
            {
                otherSubs = slicesToMerge[0].Usage.Subs;
            }

            var otherUsage = new ReportData.AppUsage
            {
                Total = otherTotal,
                On = otherOn,
                Off = otherOff,
                Neutral = otherNeutral,
                Paid = otherPaid,
                Idle = otherIdle,
                Subs = otherSubs,  // Drillable for both single leftover and merged group
            };

            result.Add(new Slice(
                Name: otherName,
                Total: otherTotal,
                DominantCategory: DominantCategory(otherUsage),
                StartAngle: 0,
                SweepAngle: 0,
                IsDrillable: otherUsage.Subs?.Count > 0,
                Percentage: (otherTotal / (double)levelTotal) * 100,
                On: otherOn,
                Off: otherOff,
                Neutral: otherNeutral,
                Paid: otherPaid,
                Idle: otherIdle));
        }

        // Step 3: calculate start and sweep angles for each slice
        var slicesWithAngles = new List<Slice>();
        double currentAngle = 0;
        foreach (var slice in result)
        {
            var sweepAngle = (slice.Total / (double)levelTotal) * 360.0;
            slicesWithAngles.Add(slice with
            {
                StartAngle = currentAngle,
                SweepAngle = sweepAngle,
            });
            currentAngle += sweepAngle;
        }

        return slicesWithAngles;
    }

    /// <summary>Add a "(no detail)" slice for the minutes in an app that have no sub-item breakdown.
    /// This reconciles the app's total with the sum of its sub-items.</summary>
    public static Slice? NoDetailSlice(int appTotal, int sumOfSubs)
    {
        if (appTotal <= sumOfSubs)
            return null;  // No unaccounted minutes

        var noDetailTotal = appTotal - sumOfSubs;
        return new Slice(
            Name: "(no detail)",
            Total: noDetailTotal,
            DominantCategory: DiaryCategory.Neutral,  // Default for unassigned
            StartAngle: 0,  // Will be set by angle calculation
            SweepAngle: 0,
            IsDrillable: false,
            Percentage: 0,
            On: 0, Off: 0, Neutral: noDetailTotal, Paid: 0, Idle: 0);
    }

    /// <summary>Determine the dominant category for a slice: the category with the most minutes.
    /// Ties are broken by DiaryCategory.ReportOrder (left-to-right in the legend).</summary>
    private static string DominantCategory(ReportData.AppUsage usage)
    {
        var categoryOrder = DiaryCategory.ReportOrder.Select(o => o.Value).ToArray();
        var categoryMinutes = new[]
        {
            (DiaryCategory.OnPlan, usage.On),
            (DiaryCategory.OffPlan, usage.Off),
            (DiaryCategory.Neutral, usage.Neutral),
            (DiaryCategory.Paid, usage.Paid),
            (DiaryCategory.Idle, usage.Idle),
        };

        var maxMinutes = categoryMinutes.Max(x => x.Item2);
        if (maxMinutes == 0)
            return DiaryCategory.Neutral;  // Default if all zeros

        // Return the first (by ReportOrder) category that has maxMinutes
        foreach (var category in categoryOrder)
        {
            var minutes = categoryMinutes.First(x => x.Item1 == category).Item2;
            if (minutes == maxMinutes)
                return category;
        }

        return DiaryCategory.Neutral;  // Fallback (shouldn't reach here)
    }
}

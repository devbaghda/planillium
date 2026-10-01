namespace Planillium.App.Services;

/// <summary>
/// Pure logic helpers for multi-select diary filters — extracted from ReportsPage.Diary
/// to be unit-testable (no WinUI types).
/// </summary>
public static class DiaryFilter
{
    /// <summary>
    /// Tests whether a single value matches a multi-select filter.
    /// An empty set means "no filter" and matches every value, including null.
    /// </summary>
    /// <param name="selected">The set of selected values (empty = no filter).</param>
    /// <param name="value">The value to test.</param>
    /// <param name="cmp">StringComparer for comparison (OrdinalIgnoreCase for app/page, Ordinal for category/tag).</param>
    /// <returns>True if the value matches the filter (empty set, or value in set), false otherwise.</returns>
    public static bool Matches(HashSet<string> selected, string? value, StringComparer cmp)
    {
        if (selected.Count == 0) return true; // Empty set = no filter, matches all
        if (value is null) return false; // Non-empty set doesn't match null
        return selected.Contains(value, cmp);
    }

    /// <summary>
    /// Produces the button label for a multi-select filter.
    /// - Empty selection: returns allLabel (e.g., "All categories")
    /// - One selection: returns that label
    /// - Multiple selections: returns "&lt;first label&gt; +N" (e.g., "Off-plan +1")
    /// where "first" is the first in display order of selectedLabels.
    /// </summary>
    /// <param name="allLabel">The "no filter" label (e.g., "All categories").</param>
    /// <param name="selectedLabels">Labels corresponding to selected values, in display order.</param>
    /// <returns>The button label text.</returns>
    public static string ButtonLabel(string allLabel, IReadOnlyList<string> selectedLabels)
    {
        return selectedLabels.Count switch
        {
            0 => allLabel,
            1 => selectedLabels[0],
            _ => $"{selectedLabels[0]} +{selectedLabels.Count - 1}"
        };
    }

    /// <summary>
    /// Removes selected values that are not in the available list, keeping the rest.
    /// Used when the scope changes (e.g., switching days or toggling All-time) and some
    /// previously-selected values no longer appear in view. Returns the surviving values.
    /// </summary>
    /// <param name="selected">The set of currently selected values.</param>
    /// <param name="available">The set of values available in the current view (may contain nulls, which are filtered out).</param>
    /// <returns>A new HashSet containing only the selected values still in available.</returns>
    public static HashSet<string> PruneToAvailable(HashSet<string> selected, IEnumerable<string?> available)
    {
        // Build a set of available values for efficient lookup, filtering out nulls.
        // Preserve the comparer from the selected set (OrdinalIgnoreCase for app/page, Ordinal for category/tag).
        var availableSet = new HashSet<string>(available.Where(x => x != null)!, selected.Comparer);
        var pruned = new HashSet<string>(selected.Comparer);
        foreach (var value in selected)
        {
            if (availableSet.Contains(value))
                pruned.Add(value);
        }
        return pruned;
    }
}

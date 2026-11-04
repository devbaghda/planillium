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
        // "No value" (null, empty or whitespace) matches only the "-" entry (NoValue sentinel).
        if (string.IsNullOrWhiteSpace(value)) return selected.Contains(NoValue);
        return selected.Contains(value, cmp);
    }

    /// <summary>Sentinel stored in a filter set for the "-" entry ("this row has no value in
    /// this field"). Contains a NUL so no real category/app/page/tag can equal it.</summary>
    public const string NoValue = "\u0000none";

    /// <summary>What the "-" entry shows.</summary>
    public const string NoValueLabel = "-";

    /// <summary>Which field the diary search box looks in. All = the original combined match.</summary>
    public enum SearchField { All, App, Page, Description, Category, Tag }

    /// <summary>Chooser items in display order, with their labels.</summary>
    public static readonly (string Label, SearchField Field)[] SearchFields =
    {
        ("All fields", SearchField.All), ("App", SearchField.App), ("Page", SearchField.Page),
        ("Description", SearchField.Description), ("Category", SearchField.Category), ("Tag", SearchField.Tag),
    };

    /// <summary>
    /// One definition of the diary text search (OrdinalIgnoreCase contains). All reproduces the
    /// original expression exactly: app label OR description OR category (underscores as spaces)
    /// OR tag label. Empty query matches everything.
    /// </summary>
    /// <param name="appLabel">AppNames.Label(window) (used by All only).</param>
    /// <param name="app">AppNames.Group(window).</param>
    /// <param name="page">AppNames.Sub(window), may be null.</param>
    /// <param name="desc">Description, may be null.</param>
    /// <param name="cat">Raw category value.</param>
    /// <param name="tagLabel">DiaryTag.LabelOf(tag), may be null.</param>
    public static bool SearchMatches(SearchField field, string q, string appLabel, string app,
        string? page, string? desc, string cat, string? tagLabel)
    {
        if (q.Length == 0) return true;
        bool Has(string? s) => s is { Length: > 0 } && s.Contains(q, StringComparison.OrdinalIgnoreCase);
        var catText = cat.Replace('_', ' ');
        return field switch
        {
            SearchField.App => Has(app),
            SearchField.Page => Has(page),
            SearchField.Description => Has(desc),
            SearchField.Category => Has(catText),
            SearchField.Tag => Has(tagLabel),
            _ => appLabel.Contains(q, StringComparison.OrdinalIgnoreCase) || Has(desc) ||
                 catText.Contains(q, StringComparison.OrdinalIgnoreCase) || Has(tagLabel),
        };
    }

    /// <summary>The "Show k more" increments offered by the diary list.</summary>
    public static readonly int[] PagingIncrements = { 50, 100, 500, 1000 };

    /// <summary>Increments to offer for <paramref name="hidden"/> rows still hidden: only those
    /// smaller than the hidden count ("Show all" covers the rest).</summary>
    public static IReadOnlyList<int> IncrementsOffered(int hidden) =>
        PagingIncrements.Where(k => k < hidden).ToList();

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
            if (value == NoValue || availableSet.Contains(value))
                pruned.Add(value);
        }
        return pruned;
    }

    /// <summary>
    /// Determines whether two option lists are the same set of values.
    /// Used to decide whether to rebuild a checkbox list or just update IsChecked on existing checkboxes.
    /// Compares values using the provided StringComparer.
    /// </summary>
    /// <param name="current">The currently displayed option list.</param>
    /// <param name="newOptions">The newly computed option list.</param>
    /// <param name="cmp">StringComparer for comparison (OrdinalIgnoreCase for app/page, Ordinal for category/tag).</param>
    /// <returns>True if both lists contain the same set of values, false otherwise.</returns>
    public static bool SameOptionSet(IReadOnlyList<string> current, IReadOnlyList<string> newOptions, StringComparer cmp)
    {
        if (current.Count != newOptions.Count)
            return false;

        var newSet = new HashSet<string>(newOptions, cmp);
        foreach (var opt in current)
        {
            if (!newSet.Contains(opt, cmp))
                return false;
        }
        return true;
    }
}

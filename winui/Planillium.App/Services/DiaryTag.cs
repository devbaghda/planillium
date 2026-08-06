namespace Planillium.App.Services;

/// <summary>
/// A second, independent classification axis on diary entries — a life-domain label,
/// separate from <see cref="DiaryCategory"/>'s on/off-plan/neutral/paid/idle scoring axis.
/// Added 2026-08-06 per user request ("an additional layer of categorization... not tied
/// to on/off-plan scoring, shown alongside it not replacing it"). ScoreService never reads
/// this column — tagging an entry has zero effect on score, streaks, or the day's totals.
///
/// Nullable at every layer: most entries have no tag. It's an optional refinement you add
/// where it's useful (e.g. telling apart *why* something was off-plan — Procrastination vs.
/// Routine vs. Documents), not a required field on every row the way category is.
///
/// Manual-only in this first pass — set via EditDiaryEntryDialog/SplitDiaryEntryDialog, never
/// auto-assigned by ActivityTracker the way category is via activity_rules keywords. Teaching
/// tag rules the same way category keywords are taught would be a natural v2 if this turns out
/// to be used often enough to want automating; deliberately not built ahead of that being true.
///
/// The five values and their exact spelling are the user's own wording, corrected twice
/// (2026-08-06) after a first pass silently expanded them into "nicer" labels
/// (Home/staff→Routine is a real word change, not just restored capitalization) — kept
/// verbatim here rather than re-prettified a second time.
/// </summary>
public static class DiaryTag
{
    public const string Routine = "routine";
    public const string Documents = "documents";
    public const string StudioShoo = "studioshoo";
    public const string SelfDev = "selfdev";
    public const string Procrastination = "procrastination";

    /// <summary>The five taggable values, with their display labels, in the order every tag
    /// dropdown (Edit, Split, the diary filter row) shows them — one shared list so a future
    /// sixth tag can't be added to a dropdown and forgotten in another, the same drift
    /// DiaryCategory.EditableOptions/ReportOrder already guards against for categories. Labels
    /// are the user's own words, title-cased for the dropdown (a capitalization convention
    /// every other option in this app already follows) — not respelled or split into words.</summary>
    public static readonly (string Label, string Value)[] Options =
    {
        ("Routine", Routine), ("Documents", Documents), ("Studioshoo", StudioShoo),
        ("Selfdev", SelfDev), ("Procrastination", Procrastination),
    };

    /// <summary>Value → label, or null for an unrecognised/legacy value — used wherever a raw
    /// column value needs to become display text (the diary row's details column, search
    /// matching) without every call site re-walking <see cref="Options"/> by hand.</summary>
    public static string? LabelOf(string? value) =>
        value is null ? null : Array.Find(Options, o => o.Value == value).Label is { Length: > 0 } l ? l : null;
}

namespace Planillium.App.Services;

/// <summary>
/// Central definition of the activity-classification category names — previously typed
/// out as bare string literals across 11 files (ActivityTracker, ScoreService,
/// ReportData, ConfigService, several Pages/Dialogs, and the SQLite read/write layer
/// itself). A typo in any one of those spots would have compiled fine and just silently
/// stopped matching, with nothing to catch it (audit finding #22). Values match the
/// on-disk contract exactly — the time_diary.category column, and the
/// activity_rules/idle_activity_rules keys in config.json — so they stay untouched at the
/// actual SQLite/JSON boundary (Database.cs's embedded SQL, ConfigService's raw key
/// reads); this class exists so every other call site references one shared name instead
/// of retyping the string.
///
/// "DayOff" is a tracker/tray-status value, not a time_diary category — never written to
/// that column (see CategoryStyle's doc comment for where the two domains meet).
/// </summary>
public static class DiaryCategory
{
    public const string OnPlan = "on_plan";
    public const string OffPlan = "off_plan";
    public const string Neutral = "neutral";
    public const string Idle = "idle";
    public const string Paid = "paid";
    public const string DayOff = "dayoff";

    /// <summary>The five editable categories, with their display labels, in the order
    /// EditDiaryEntryDialog and SplitDiaryEntryDialog each show them in a category
    /// dropdown — previously copy-pasted verbatim in both dialogs, in sync only by
    /// coincidence (2026-07-18 audit finding R8-11).</summary>
    public static readonly (string Label, string Value)[] EditableOptions =
    {
        ("On-plan", OnPlan), ("Off-plan", OffPlan), ("Paid", Paid), ("Neutral", Neutral), ("Idle", Idle),
    };
}

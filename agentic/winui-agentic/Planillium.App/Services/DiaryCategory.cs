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

    /// <summary>The same five categories in the order every *report* shows them — the Time-by-app
    /// legend and its stacked bars, both summary tables' columns, and the HTML/CSV exports
    /// (2026-08-05). Deliberately its own list rather than a reuse of <see cref="EditableOptions"/>:
    /// that one is ordered for a dropdown you pick from, this one for columns read left to right,
    /// and the two orders genuinely differ (Paid is third there, fourth here). What must never
    /// differ is the membership — DiaryCategoryTests asserts both hold the same five values, so
    /// adding a sixth category to one and not the other fails a test instead of quietly dropping
    /// a column from every report.</summary>
    public static readonly (string Label, string Value)[] ReportOrder =
    {
        ("On-plan", OnPlan), ("Off-plan", OffPlan), ("Neutral", Neutral), ("Paid", Paid), ("Idle", Idle),
    };

    /// <summary>The diary description text written for a not-yet-answered "where were you"
    /// idle/away gap (ActivityTracker.HandleIdleReturn logs this immediately so nothing goes
    /// unrecorded; LogIdleAnswer/LogIdleAnswers replace it in place with the user's real
    /// answer if/when they respond). Was "Dismissed" before a 2026-07-27 rename —
    /// <see cref="LegacyIdlePlaceholder"/> still needs excluding wherever placeholder rows
    /// are matched/filtered, so rows written under the old text don't show up disguised as a
    /// real answer. Centralized here (2026-07-28) after this exact pair of literals turned
    /// up hand-typed in four separate places (ActivityTracker, IdleReturnDialog, and two
    /// Database.cs queries) with no shared source — the same "fix one sibling, miss the
    /// other" drift risk this project has hit before, since the pair was already renamed
    /// once and not every copy was found at the time.</summary>
    public const string IdlePlaceholder = "unaccounted time";
    public const string LegacyIdlePlaceholder = "dismissed";
}

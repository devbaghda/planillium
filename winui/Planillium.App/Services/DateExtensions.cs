using System.Globalization;

namespace Planillium.App.Services;

/// <summary>
/// The one place the app's date-key format (used everywhere a date has to
/// match a DB row — score ledger, task completions, diary rows) is spelled
/// out. Previously typed by hand 26 times across 8 files; one careless
/// retyping (e.g. dropping InvariantCulture) would silently stop matching
/// rows on a non-English-locale Windows install — the exact bug class this
/// app already shipped once (2026-07-07 audit, OS locale Russian, app
/// language English). One helper makes that mistake impossible to repeat.
/// </summary>
internal static class DateExtensions
{
    public static string ToIsoDate(this DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string ToIsoDate(this DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Same reasoning as ToIsoDate, for the "date + time" format used in ts/
    /// completed_at/marked_at/updated_at columns — round 4's ToIsoDate helper didn't
    /// cover this closely related format, which was still hand-typed in 5 separate
    /// places (round-5 audit finding #16).</summary>
    public static string ToIsoTimestamp(this DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Time-of-day only (no date), used for time_diary's start_time/end_time
    /// columns. Same reasoning as ToIsoDate/ToIsoTimestamp — a third persisted-string
    /// shape that was still hand-typed as "HH:mm" with InvariantCulture in 2 separate
    /// files (2026-07-14 round-6 audit finding #12). All three currently got the
    /// InvariantCulture argument right independently, but a shared helper is the
    /// only thing that makes a future retyping unable to drop it.</summary>
    public static string ToIsoTimeOfDay(this DateTime d) => d.ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string ToIsoTimeOfDay(this TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Human-facing "Tue 15.07" display format — English day names
    /// regardless of OS locale, same InvariantCulture rule as every other
    /// persisted/displayed date in this app. Was hand-typed identically in 3
    /// files (2026-07-14 round-6 audit finding #11); unlike ToIsoDate this
    /// one isn't a DB key, but the sibling-drift risk is the same.</summary>
    public static string ToDisplayDate(this DateTime d) => d.ToString("ddd dd.MM", CultureInfo.InvariantCulture);
    public static string ToDisplayDate(this DateOnly d) => d.ToString("ddd dd.MM", CultureInfo.InvariantCulture);

    /// <summary>The read-side counterpart of ToIsoDate — every one of this file's other
    /// helpers covers the write direction, but the reverse ("turn a stored yyyy-MM-dd
    /// string back into a DateOnly") was still hand-typed 6 times in ReportData.cs alone
    /// (2026-07-18 audit finding R11-04), the identical drift risk this class exists to
    /// prevent, just on the leg nothing had covered yet.</summary>
    public static bool TryParseIsoDate(this string s, out DateOnly d) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
}

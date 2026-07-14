using System.Globalization;

namespace MentorOverseer.App.Services;

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
}

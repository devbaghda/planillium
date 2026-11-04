using System.Globalization;

namespace Planillium.App.Services;

/// <summary>
/// Single shared EUR formatter used by both the app and insights logic.
/// Format: "€X.XX" or "-€X.XX" depending on sign.
/// </summary>
internal static class EurFormatter
{
    /// <summary>Format a euro amount as "€X.XX" or "-€X.XX".</summary>
    internal static string Format(double v) =>
        (v < 0 ? "-€" : "€") + Math.Abs(v).ToString("N2", CultureInfo.CurrentCulture);
}

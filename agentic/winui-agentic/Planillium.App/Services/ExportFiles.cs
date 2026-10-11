namespace Planillium.App.Services;

/// <summary>
/// The three files Settings' exporters can write to the data folder — shared so the
/// writers (DataExport, ReportExport) and the delete list (SettingsPage's "clear my data"
/// actions) can't drift on what these filenames actually are. R9-01 (2026-07-18) unified
/// the *delete* side onto one list; the *write* side still independently retyped these
/// three names in a different file that couldn't see that list — this closes the other
/// half (2026-07-18 audit finding R11-06).
/// </summary>
public static class ExportFiles
{
    public const string ReportHtml = "report.html";
    public const string ReportCsv = "report.csv";
    public const string FullExportJson = "full-export.json";

    public static readonly string[] All = { ReportHtml, ReportCsv, FullExportJson };
}

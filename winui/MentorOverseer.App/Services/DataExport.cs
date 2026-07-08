using System.Diagnostics;
using System.Text.Json;

namespace MentorOverseer.App.Services;

/// <summary>
/// Full raw-table export — every row Database.ExportAllTables returns,
/// unlike ReportExport's HTML/CSV which only ever show aggregated numbers.
/// Settings' "Export all my data" action, so there's an actual answer to
/// "what do you have stored about me" beyond reading the report views.
/// </summary>
public static class DataExport
{
    /// <summary>Writes data/full-export.json and opens it. Returns the path.</summary>
    public static string ExportAll()
    {
        using var db = new Database();
        var tables = db.ExportAllTables();

        var json = JsonSerializer.Serialize(tables, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        var outPath = Path.Combine(AppPaths.Root, "data", "full-export.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, json, System.Text.Encoding.UTF8);
        Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
        return outPath;
    }
}

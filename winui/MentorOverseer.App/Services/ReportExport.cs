using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MentorOverseer.App.Services;

/// <summary>
/// Self-contained weekly HTML report — port of main.py's _export_html_report.
/// Everything derived from window titles is HTML-escaped (titles are
/// attacker-influenced text: any web page can set document.title).
/// </summary>
public static class ReportExport
{
    /// <summary>Writes data/report.html and opens it in the browser. Returns the path.</summary>
    public static string ExportWeek()
    {
        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var streak = score.CurrentStreak();

        var dayRows = new StringBuilder();
        var weekOn = 0; var weekOff = 0;
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var (total, done) = score.DayTaskCounts(d);
            var (on, off) = score.DayDiaryMinutes(d);
            weekOn += on; weekOff += off;
            var s = score.DayScore(done, total, on, off, d == today ? streak : 0);
            var col = s >= 20 ? "#30d158" : s < 0 ? "#ff453a" : "#ff9f0a";
            dayRows.Append(
                $"<tr><td>{d.ToString("ddd dd.MM", CultureInfo.InvariantCulture)}</td>" +
                $"<td>{done}/{total}</td><td>{on}m</td><td>{off}m</td>" +
                $"<td style='color:{col};font-weight:bold'>{s}</td></tr>");
        }

        var distractions = TopOffPlanWindows(db);
        var distRows = distractions.Count == 0
            ? "<tr><td colspan='2'>No distractions logged.</td></tr>"
            : string.Join("", distractions.Select(kv =>
                $"<tr><td>{WebUtility.HtmlEncode(Truncate(kv.Key, 60))}</td><td>{kv.Value}m</td></tr>"));

        var hints = Suggestions(weekOn, weekOff, distractions);
        var hintItems = string.Join("", hints.Select(h => $"<li>{WebUtility.HtmlEncode(h)}</li>"));

        var html = $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <title>Mentor Overseer — Weekly Report</title>
            <style>
              body { font-family: 'Segoe UI', sans-serif; max-width: 760px; margin: 40px auto;
                     padding: 0 16px; color: #1a1a1a; }
              h1 { font-size: 22px; } h2 { font-size: 15px; margin-top: 28px; }
              .sub { color: #777; font-size: 12px; margin-bottom: 24px; }
              table { border-collapse: collapse; width: 100%; }
              td, th { padding: 6px 10px; border-bottom: 1px solid #e5e5e5;
                       text-align: left; font-size: 13px; }
              th { color: #777; font-weight: 600; }
              li { font-size: 13px; margin: 6px 0; }
              @media (prefers-color-scheme: dark) {
                body { background: #1c1c1e; color: #eee; }
                td, th { border-color: #333; } .sub { color: #999; }
              }
            </style></head><body>
            <h1>Mentor Overseer</h1>
            <div class="sub">Weekly Report · generated {{DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}}</div>
            <h2>The week</h2>
            <table><tr><th>Day</th><th>Tasks</th><th>On-plan</th><th>Off-plan</th><th>Score</th></tr>{{dayRows}}</table>
            <h2>Top distractions (7 days)</h2>
            <table>{{distRows}}</table>
            <h2>Suggestions</h2>
            <ul>{{hintItems}}</ul>
            </body></html>
            """;

        var outPath = Path.Combine(AppPaths.Root, "data", "report.html");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, html, Encoding.UTF8);
        Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
        return outPath;
    }

    private static List<KeyValuePair<string, int>> TopOffPlanWindows(Database db)
    {
        var result = new Dictionary<string, int>();
        using var conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT window, SUM(duration_min) FROM time_diary " +
            "WHERE category='off_plan' AND date >= date('now', '-7 days') " +
            "GROUP BY window ORDER BY 2 DESC LIMIT 12";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result[r.GetString(0)] = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        return result.OrderByDescending(kv => kv.Value).ToList();
    }

    private static List<string> Suggestions(int weekOn, int weekOff,
        List<KeyValuePair<string, int>> distractions)
    {
        var hints = new List<string>();
        if (weekOff > 120)
            hints.Add($"You spent {weekOff / 60}h {weekOff % 60}m off-plan this week. " +
                      "Try blocking distracting apps during working hours.");
        if (weekOn > 0 && (double)weekOff / Math.Max(weekOn, 1) > 0.4)
            hints.Add("Off-plan time is over 40% of your productive time. " +
                      "Your goal needs tighter focus blocks.");
        if (distractions.Count > 0)
        {
            var top = distractions[0];
            var app = Truncate(top.Key.Split(" - ").Last(), 40);
            hints.Add($"'{app}' is your biggest distraction — {top.Value} min off-plan this week.");
        }
        if (hints.Count == 0)
            hints.Add("Great week — no major distraction patterns detected. Keep going!");
        return hints;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}

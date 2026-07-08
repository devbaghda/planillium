using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;

namespace MentorOverseer.App.Services;

/// <summary>
/// Weekly report exports — HTML (port of main.py's _export_html_report) and
/// CSV. Distractions and the app breakdown use the same grouped
/// "App - sub" labels as the Reports page (via ReportData/AppNames).
/// Everything derived from window titles is HTML-escaped in the HTML export
/// (titles are attacker-influenced: any web page can set document.title).
/// </summary>
public static class ReportExport
{
    /// <summary>Writes data/report.html and opens it in the browser. Returns the path.</summary>
    public static string ExportWeek()
    {
        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);

        var stats = ReportData.WeekStats(score);
        var weekOn = stats.Sum(s => s.OnMin);
        var weekOff = stats.Sum(s => s.OffMin);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var dayRows = new StringBuilder();
        foreach (var s in stats)
        {
            var col = s.Score >= 20 ? "#30d158" : s.Score < 0 ? "#ff453a" : "#ff9f0a";
            dayRows.Append(
                $"<tr><td>{s.Date.ToString("ddd dd.MM", CultureInfo.InvariantCulture)}</td>" +
                $"<td>{s.Done}/{s.Total}</td><td>{s.OnMin}m</td><td>{s.OffMin}m</td>" +
                $"<td style='color:{col};font-weight:bold'>{s.Score}</td></tr>");
        }

        var distractions = ReportData.TopDistractions(ReportPeriod.Week);
        var distRows = distractions.Count == 0
            ? "<tr><td colspan='2'>No distractions logged.</td></tr>"
            : string.Join("", distractions.Select(d =>
                $"<tr><td>{WebUtility.HtmlEncode(d.Label)}</td><td>{d.Minutes}m</td></tr>"));

        var breakdown = ReportData.AppBreakdown(ReportPeriod.Week);
        var appRows = new StringBuilder();
        foreach (var (app, usage) in breakdown)
        {
            appRows.Append(
                $"<tr><td><b>{WebUtility.HtmlEncode(app)}</b></td>" +
                $"<td>{usage.On}m</td><td>{usage.Off}m</td>" +
                $"<td>{ReportData.FmtMins(usage.Total)}</td></tr>");
            if (usage.Subs is null) continue;
            foreach (var (sub, su) in usage.Subs.OrderByDescending(kv => kv.Value.Total).Take(10))
                appRows.Append(
                    $"<tr><td style='padding-left:28px'>{WebUtility.HtmlEncode(sub)}</td>" +
                    $"<td>{su.On}m</td><td>{su.Off}m</td>" +
                    $"<td>{ReportData.FmtMins(su.Total)}</td></tr>");
        }

        var hints = Suggestions(weekOn, weekOff, distractions);
        var hintItems = string.Join("", hints.Select(h => $"<li>{WebUtility.HtmlEncode(h)}</li>"));

        var html = $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <title>Planillium — Weekly Report</title>
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
            <h1>Planillium</h1>
            <div class="sub">Weekly Report · generated {{DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}}</div>
            <h2>The week</h2>
            <table><tr><th>Day</th><th>Tasks</th><th>On-plan</th><th>Off-plan</th><th>Score</th></tr>{{dayRows}}</table>
            <h2>Top distractions (7 days)</h2>
            <table>{{distRows}}</table>
            <h2>Time by app (7 days)</h2>
            <table><tr><th>App</th><th>On-plan</th><th>Off-plan</th><th>Total</th></tr>{{appRows}}</table>
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

    /// <summary>
    /// Writes data/report.csv for the given period and opens it (Excel).
    /// UTF-8 **with BOM** — without it Excel mangles Cyrillic chat names.
    /// </summary>
    public static string ExportCsv(ReportPeriod period)
    {
        var plans = PlanStore.LoadActivePlans();
        using var db = new Database();
        using var score = new ScoreService(plans, db);

        var sb = new StringBuilder();
        var name = ReportData.PeriodName(period);
        sb.AppendLine(Csv("Planillium report", name,
            "generated " + DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)));
        sb.AppendLine();

        if (period is ReportPeriod.Day or ReportPeriod.Week)
        {
            var stats = ReportData.WeekStats(score);
            if (period == ReportPeriod.Day) stats = stats.TakeLast(1).ToList();
            sb.AppendLine(Csv("Date", "Tasks done", "Tasks total",
                "On-plan (min)", "Off-plan (min)", "Score"));
            foreach (var s in stats)
                sb.AppendLine(Csv(
                    s.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    s.Done.ToString(CultureInfo.InvariantCulture),
                    s.Total.ToString(CultureInfo.InvariantCulture),
                    s.OnMin.ToString(CultureInfo.InvariantCulture),
                    s.OffMin.ToString(CultureInfo.InvariantCulture),
                    s.Score.ToString(CultureInfo.InvariantCulture)));
        }
        else
        {
            sb.AppendLine(Csv("Period", "On-plan (min)", "Off-plan (min)", "Total (min)"));
            foreach (var b in ReportData.Buckets(period))
                sb.AppendLine(Csv(b.Label,
                    b.OnMin.ToString(CultureInfo.InvariantCulture),
                    b.OffMin.ToString(CultureInfo.InvariantCulture),
                    (b.OnMin + b.OffMin).ToString(CultureInfo.InvariantCulture)));
        }

        sb.AppendLine();
        sb.AppendLine(Csv("Top distractions — " + name));
        sb.AppendLine(Csv("Application", "Minutes"));
        foreach (var (label, minutes) in ReportData.TopDistractions(period))
            sb.AppendLine(Csv(label, minutes.ToString(CultureInfo.InvariantCulture)));

        sb.AppendLine();
        sb.AppendLine(Csv("Time by app — " + name));
        sb.AppendLine(Csv("Application", "Sub-item",
            "On-plan (min)", "Off-plan (min)", "Neutral (min)", "Total (min)"));
        foreach (var (app, usage) in ReportData.AppBreakdown(period))
        {
            sb.AppendLine(Csv(app, "",
                usage.On.ToString(CultureInfo.InvariantCulture),
                usage.Off.ToString(CultureInfo.InvariantCulture),
                usage.Neutral.ToString(CultureInfo.InvariantCulture),
                usage.Total.ToString(CultureInfo.InvariantCulture)));
            if (usage.Subs is null) continue;
            foreach (var (sub, su) in usage.Subs.OrderByDescending(kv => kv.Value.Total))
                sb.AppendLine(Csv(app, sub,
                    su.On.ToString(CultureInfo.InvariantCulture),
                    su.Off.ToString(CultureInfo.InvariantCulture),
                    su.Neutral.ToString(CultureInfo.InvariantCulture),
                    su.Total.ToString(CultureInfo.InvariantCulture)));
        }

        var outPath = Path.Combine(AppPaths.Root, "data", "report.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
        return outPath;
    }

    private static string Csv(params string[] fields) =>
        string.Join(",", fields.Select(f =>
            f.Contains(',') || f.Contains('"') || f.Contains('\n')
                ? "\"" + f.Replace("\"", "\"\"") + "\"" : f));

    /// <summary>Also rendered on the Reports page as INSIGHTS.</summary>
    public static List<string> Suggestions(int weekOn, int weekOff,
        List<(string Label, int Minutes)> distractions)
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
            hints.Add($"'{top.Label}' is your biggest distraction — " +
                      $"{top.Minutes} min off-plan this week.");
        }
        if (hints.Count == 0)
            hints.Add("Great week — no major distraction patterns detected. Keep going!");
        return hints;
    }
}

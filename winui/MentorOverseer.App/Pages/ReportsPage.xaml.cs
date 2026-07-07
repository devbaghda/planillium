using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Services;

namespace MentorOverseer.App.Pages;

public sealed partial class ReportsPage : Page
{
    public ReportsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try { ReportExport.ExportWeek(); }
        catch (Exception ex) { Log.Error("ReportsPage.Export", ex); }
    }

    private void Render()
    {
        Body.Children.Clear();
        try
        {
            var plans = PlanStore.LoadActivePlans();
            using var db = new Database();
            using var score = new ScoreService(plans, db);
            var today = DateOnly.FromDateTime(DateTime.Today);

            // ── today's score card ────────────────────────────────────────
            var (tTotal, tDone) = score.DayTaskCounts(today);
            var (tOn, tOff) = score.DayDiaryMinutes(today);
            var todayScore = score.DayScore(tDone, tTotal, tOn, tOff, score.CurrentStreak());

            var card = new StackPanel { Spacing = 2 };
            card.Children.Add(Caption("TODAY'S SCORE"));
            card.Children.Add(new TextBlock
            {
                Text = todayScore.ToString(),
                FontSize = 44,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources[
                    todayScore >= 20 ? "SystemFillColorSuccessBrush"
                    : todayScore < 0 ? "SystemFillColorCriticalBrush"
                    : "SystemFillColorCautionBrush"],
            });
            card.Children.Add(Dim($"{tDone}/{tTotal} tasks · {tOn}m on-plan · {tOff}m off-plan"));
            Body.Children.Add(Card(card));

            // ── 7-day table ───────────────────────────────────────────────
            Body.Children.Add(Section("THIS WEEK"));
            var grid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
            for (var c = 0; c < 5; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = c == 0 ? new GridLength(110) : GridLength.Auto });
            var headers = new[] { "Day", "Tasks", "On-plan", "Off-plan", "Score" };
            for (var c = 0; c < headers.Length; c++)
            {
                var h = Caption(headers[c]);
                Grid.SetColumn(h, c); Grid.SetRow(h, 0);
                grid.Children.Add(h);
            }
            grid.RowDefinitions.Add(new RowDefinition());
            var streak = score.CurrentStreak();
            for (var i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var (total, done) = score.DayTaskCounts(d);
                var (on, off) = score.DayDiaryMinutes(d);
                var s = score.DayScore(done, total, on, off, d == today ? streak : 0);
                var row = grid.RowDefinitions.Count;
                grid.RowDefinitions.Add(new RowDefinition());
                var isToday = d == today;
                var cells = new[]
                {
                    d.ToString("ddd dd.MM", CultureInfo.InvariantCulture),
                    $"{done}/{total}", Fmt(on), Fmt(off), s.ToString(),
                };
                for (var c = 0; c < cells.Length; c++)
                {
                    var tb = new TextBlock
                    {
                        Text = cells[c],
                        FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
                    };
                    if (c == 4)
                        tb.Foreground = (Brush)Application.Current.Resources[
                            s >= 20 ? "SystemFillColorSuccessBrush"
                            : s < 0 ? "SystemFillColorCriticalBrush"
                            : "SystemFillColorCautionBrush"];
                    else if (!isToday)
                        tb.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                    Grid.SetColumn(tb, c); Grid.SetRow(tb, row);
                    grid.Children.Add(tb);
                }
            }
            Body.Children.Add(Card(grid));

            // ── top distractions (last 7 days) ────────────────────────────
            Body.Children.Add(Section("TOP DISTRACTIONS · 7 DAYS"));
            var distractions = TopDistractions(7);
            if (distractions.Count == 0)
                Body.Children.Add(Dim("No off-plan time logged. Impressive."));
            else
            {
                var maxMin = distractions[0].Minutes;
                var list = new StackPanel { Spacing = 8 };
                foreach (var (app, minutes) in distractions)
                {
                    var row = new Grid { ColumnSpacing = 12 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var name = new TextBlock { Text = app, TextTrimming = TextTrimming.CharacterEllipsis };
                    var track = new Border
                    {
                        Height = 8, CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    };
                    var fill = new Border
                    {
                        Height = 8, CornerRadius = new CornerRadius(4),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = Math.Max(8, 320.0 * minutes / maxMin),
                        Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                    };
                    var overlay = new Grid();
                    overlay.Children.Add(track);
                    overlay.Children.Add(fill);
                    var mins = Dim(Fmt(minutes));
                    Grid.SetColumn(overlay, 1);
                    Grid.SetColumn(mins, 2);
                    row.Children.Add(name);
                    row.Children.Add(overlay);
                    row.Children.Add(mins);
                    list.Children.Add(row);
                }
                Body.Children.Add(Card(list));
            }

            // ── today's diary ─────────────────────────────────────────────
            Body.Children.Add(Section("TIME DIARY · TODAY"));
            var diary = TodayDiary();
            if (diary.Count == 0)
                Body.Children.Add(Dim("No diary entries yet today. Tracking runs 06:00–20:00."));
            else
            {
                var list = new StackPanel { Spacing = 4 };
                foreach (var (start, end, dur, cat, window, desc) in diary)
                {
                    var row = new Grid { ColumnSpacing = 12 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var time = Dim($"{start} → {end}");
                    var catText = new TextBlock
                    {
                        Text = cat.Replace('_', '-'),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources[cat switch
                        {
                            "on_plan" => "SystemFillColorSuccessBrush",
                            "off_plan" => "SystemFillColorCriticalBrush",
                            "idle" => "SystemFillColorCautionBrush",
                            "paid" => "AccentTextFillColorPrimaryBrush",
                            _ => "TextFillColorSecondaryBrush",
                        }],
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var what = new TextBlock
                    {
                        Text = desc is { Length: > 0 } ? $"“{desc}” ({dur}m)" : $"{window} ({dur}m)",
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontStyle = desc is { Length: > 0 }
                            ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                    };
                    Grid.SetColumn(catText, 1);
                    Grid.SetColumn(what, 2);
                    row.Children.Add(time);
                    row.Children.Add(catText);
                    row.Children.Add(what);
                    list.Children.Add(row);
                }
                Body.Children.Add(Card(list));
            }
        }
        catch (Exception ex)
        {
            Log.Error("ReportsPage.Render", ex);
            Body.Children.Add(new TextBlock
            {
                Text = "Couldn't load report data: " + ex.Message,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private static List<(string App, int Minutes)> TopDistractions(int days)
    {
        using var conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT window, SUM(duration_min) AS m FROM time_diary " +
            "WHERE category='off_plan' AND date >= $from " +
            "GROUP BY window ORDER BY m DESC LIMIT 5";
        cmd.Parameters.AddWithValue("$from",
            DateOnly.FromDateTime(DateTime.Today).AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var result = new List<(string, int)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var window = r.GetString(0);
            result.Add((window.Length > 48 ? window[..48] : window, r.GetInt32(1)));
        }
        return result;
    }

    private static List<(string Start, string End, int Dur, string Cat, string Window, string? Desc)> TodayDiary()
    {
        using var conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT start_time, end_time, duration_min, category, window, description " +
            "FROM time_diary WHERE date=$d ORDER BY start_time";
        cmd.Parameters.AddWithValue("$d", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var result = new List<(string, string, int, string, string, string?)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add((r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3),
                        r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5)));
        return result;
    }

    // ── styling helpers ───────────────────────────────────────────────────

    private static string Fmt(int mins) =>
        mins >= 60 ? $"{mins / 60}h {mins % 60:00}m" : $"{mins}m";

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 60,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        Margin = new Thickness(2, 22, 0, 8),
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 50,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
    };

    private static TextBlock Dim(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private static Border Card(UIElement child) => new()
    {
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18, 14, 18, 14),
        Child = child,
    };
}

using System.Text.Json.Nodes;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>Manual day off tracks like non-working hours (user, 2026-11-20; acceptance
/// context/acceptance/2026-11-20-day-off-tracks-like-after-hours.md, D7). The poll-level cases
/// (D1-D6, D9, D10) need real foreground-window/idle calls and are exercised in the scratch-root
/// live run, not here.</summary>
[Collection("TestRoot")]
public sealed class DayOffTrackingTests
{
    private static DateTime FixedNow() => DateTime.Today.AddHours(12);

    private static void Setup(out Database db, out ActivityTracker tracker, out DateTime now)
    {
        ConfigService.Mutate(cfg =>
            cfg["working_hours"] = new JsonObject { ["start"] = "00:00", ["end"] = "23:59" });
        db = new Database();
        using (var del = db.CreateCommand())
        {
            del.CommandText = "DELETE FROM time_diary WHERE date = $d";
            del.Parameters.AddWithValue("$d", DateOnly.FromDateTime(DateTime.Today).ToIsoDate());
            del.ExecuteNonQuery();
        }
        tracker = new ActivityTracker(ConfigService.Root);
        now = FixedNow();
        var lastEnd = now.AddMinutes(-90);
        using var ins = db.CreateCommand();
        ins.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, 30, $c, 'TestWindow', NULL)";
        ins.Parameters.AddWithValue("$d", DateOnly.FromDateTime(lastEnd).ToIsoDate());
        ins.Parameters.AddWithValue("$s", lastEnd.AddMinutes(-30).ToIsoTimeOfDay());
        ins.Parameters.AddWithValue("$e", lastEnd.ToIsoTimeOfDay());
        ins.Parameters.AddWithValue("$c", DiaryCategory.OnPlan);
        ins.ExecuteNonQuery();
    }

    [Fact]
    public void Day_off_has_no_pending_gap_question()
    {
        Setup(out var db, out var t, out var now);
        using (db)
        {
            Assert.NotNull(t.PendingDayGap(db, now));      // same data on a normal day: asked
            t.FullyOffForTest = true;
            Assert.Null(t.PendingDayGap(db, now));         // day off: not asked
            t.FullyOffForTest = false;
            Assert.NotNull(t.PendingDayGap(db, now));      // un-marked: asked again
        }
    }
}

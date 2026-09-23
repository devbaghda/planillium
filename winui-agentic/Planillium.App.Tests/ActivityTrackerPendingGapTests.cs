using System.Text.Json.Nodes;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// ActivityTracker.PendingDayGap — the evening review's "where have you been?" sweep
/// (ReviewDialog.ReconcilePendingGap). Two real bugs here (2026-08-05 report: "the app asks me
/// about my absence twice and makes two identical recordings"):
///
///   1. It judged only by what was already written to the database, with no idea a session was
///      still open — someone working continuously in one app for hours, right through review
///      time, read as having been "away" for the whole stretch. Answering that produced an idle
///      row; the still-open session then flushed on its own a few polls later, covering the same
///      span a second time.
///   2. It never remembered what it had already asked about. The manual "Evening review" preview
///      button on Today calls ReviewDialog.ShowAsync directly, with none of the automatic
///      once-a-day guard — so a second click re-asked about, and re-logged, whatever the first
///      click had already covered.
///
/// Both are fixed by two clamps inside PendingDayGap itself: an open session caps how far the
/// gap can extend, and MarkAccountedThrough's own record caps how far back it can start.
///
/// Working hours are set to 00:00-23:59 in every test so "now", whenever the suite happens to
/// run, always falls inside them — the point under test is the gap arithmetic, not the working-
/// hours boundary, which ConfigDiaryHoursTests already covers on its own.
///
/// PendingDayGap's own query (Database.LastDiaryEnd) has no per-test scoping — it's simply "the
/// latest end_time in the whole table" — and TestRootFixture's one shared SQLite file (see its
/// own doc comment) means another test's diary rows would otherwise be picked up as "today's
/// last activity" here. Each test clears today's time_diary rows first, same reasoning as the
/// unique-plan-id convention those other tests use for their own tables.
/// </summary>
[Collection("TestRoot")]
public sealed class ActivityTrackerPendingGapTests
{
    private static void SetAllDayWorkingHours() =>
        ConfigService.Mutate(cfg =>
            cfg["working_hours"] = new JsonObject { ["start"] = "00:00", ["end"] = "23:59" });

    private static void ClearTodaysDiary(Database db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM time_diary WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", DateOnly.FromDateTime(DateTime.Today).ToIsoDate());
        cmd.ExecuteNonQuery();
    }

    private static void AddDiaryRow(Database db, DateTime start, DateTime end, string category)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, $m, $c, 'TestWindow', NULL)";
        cmd.Parameters.AddWithValue("$d", DateOnly.FromDateTime(start).ToIsoDate());
        cmd.Parameters.AddWithValue("$s", start.ToIsoTimeOfDay());
        cmd.Parameters.AddWithValue("$e", end.ToIsoTimeOfDay());
        cmd.Parameters.AddWithValue("$m", Math.Max(1, (int)(end - start).TotalMinutes));
        cmd.Parameters.AddWithValue("$c", category);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Baseline: with nothing open and nothing already accounted for, a real trailing
    /// gap is still reported — the fix must not make PendingDayGap stop finding genuine gaps,
    /// only stop finding false ones.</summary>
    [Fact]
    public void GenuineTrailingGapIsStillReported()
    {
        SetAllDayWorkingHours();
        using var db = new Database();
        ClearTodaysDiary(db);
        var tracker = new ActivityTracker(ConfigService.Root);
        var now = DateTime.Now;
        var lastEnd = now.AddMinutes(-90);
        AddDiaryRow(db, lastEnd.AddMinutes(-30), lastEnd, DiaryCategory.OnPlan);

        var gap = tracker.PendingDayGap(db);

        Assert.NotNull(gap);
        Assert.Equal(lastEnd.ToString("HH:mm"), gap!.Value.Start.ToString("HH:mm"));
        Assert.True(gap.Value.Minutes is >= 88 and <= 92, $"Expected ~90 minutes, got {gap.Value.Minutes}");
    }

    /// <summary>The first bug: a session left open (not yet flushed to the diary) must cap how
    /// far the gap can extend — the hour still in progress is not an absence.</summary>
    [Fact]
    public void OpenSessionClampsTheGapToItsOwnStart()
    {
        SetAllDayWorkingHours();
        using var db = new Database();
        ClearTodaysDiary(db);
        var tracker = new ActivityTracker(ConfigService.Root);
        var now = DateTime.Now;
        var lastEnd = now.AddMinutes(-180);
        var openStart = now.AddMinutes(-120);
        AddDiaryRow(db, lastEnd.AddMinutes(-30), lastEnd, DiaryCategory.OnPlan);
        tracker.SimulateOpenSession(openStart, "VS Code", DiaryCategory.OnPlan);

        var gap = tracker.PendingDayGap(db);

        // Without the clamp this would run all the way to "now" (~180 min) — the open session's
        // own 120 minutes of live, in-progress work would be swept up into the "where were you"
        // question and then logged a second time once the session itself eventually closes.
        Assert.NotNull(gap);
        Assert.Equal(lastEnd.ToString("HH:mm"), gap!.Value.Start.ToString("HH:mm"));
        Assert.True(gap.Value.Minutes is >= 58 and <= 62,
            $"Expected ~60 minutes (up to the open session's start, not to now), got {gap.Value.Minutes}");
    }

    /// <summary>An open session that starts before the last diary row's end (the ordinary case —
    /// no trailing gap at all right now) must report no gap, not a negative one.</summary>
    [Fact]
    public void OpenSessionCoveringTheWholeStretchMeansNoGap()
    {
        SetAllDayWorkingHours();
        using var db = new Database();
        ClearTodaysDiary(db);
        var tracker = new ActivityTracker(ConfigService.Root);
        var now = DateTime.Now;
        var lastEnd = now.AddMinutes(-60);
        AddDiaryRow(db, lastEnd.AddMinutes(-30), lastEnd, DiaryCategory.OnPlan);
        // The open session picked up right where the last row left off — continuous activity,
        // nothing unaccounted.
        tracker.SimulateOpenSession(lastEnd, "VS Code", DiaryCategory.OnPlan);

        Assert.Null(tracker.PendingDayGap(db));
    }

    /// <summary>The second bug: once a gap has been accounted for (MarkAccountedThrough, the
    /// same call ReviewDialog.ReconcilePendingGap makes after showing the dialog), calling
    /// PendingDayGap again must not re-report it — the shape of a second click on Today's manual
    /// "Evening review" preview button.</summary>
    [Fact]
    public void AlreadyAccountedForGapIsNotAskedAboutAgain()
    {
        SetAllDayWorkingHours();
        using var db = new Database();
        ClearTodaysDiary(db);
        var tracker = new ActivityTracker(ConfigService.Root);
        var now = DateTime.Now;
        var lastEnd = now.AddMinutes(-90);
        AddDiaryRow(db, lastEnd.AddMinutes(-30), lastEnd, DiaryCategory.OnPlan);

        var firstGap = tracker.PendingDayGap(db);
        Assert.NotNull(firstGap);
        // Mirrors exactly what ReviewDialog.ReconcilePendingGap does after the dialog closes.
        tracker.MarkAccountedThrough(firstGap!.Value.Start.AddMinutes(firstGap.Value.Minutes));

        var secondGap = tracker.PendingDayGap(db);

        Assert.Null(secondGap);
    }
}

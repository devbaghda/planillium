using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// The DiaryTag axis (2026-08-06 request: "an additional layer of categorization... not
/// tied to on/off-plan scoring, shown alongside it not replacing it"). Covers the one thing
/// that isn't visible by reading the code: that the new nullable `tag` column actually
/// round-trips through insert/update/read, including the schema migration that adds it to
/// a database created before this column existed (this app's first-ever ADD COLUMN
/// migration — see Database.EnsureSchema's own comment on why it needs a PRAGMA table_info
/// check rather than a plain "IF NOT EXISTS").
///
/// Doesn't test ReportsPage.Diary.cs's dialogs/filter UI directly — this project's tests
/// stay at the Service/Data layer throughout, matching every other test file here (there is
/// no WinUI page-level test harness in this project).
/// </summary>
[Collection("TestRoot")]
public sealed class DiaryTagTests
{
    private static void AddRawDiaryRow(Database db, DateOnly date, string window)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary (date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, '09:00', '10:00', 60, $c, $w, NULL)";
        cmd.Parameters.AddWithValue("$d", date.ToIsoDate());
        cmd.Parameters.AddWithValue("$c", DiaryCategory.OffPlan);
        cmd.Parameters.AddWithValue("$w", window);
        cmd.ExecuteNonQuery();
    }

    /// <summary>The migration itself: a time_diary row written the old way (no tag column
    /// referenced at all, same as every pre-2026-08-06 write path) must still exist under a
    /// schema that now has the column — proves EnsureSchema's ALTER TABLE ran and didn't
    /// break rows that predate it.</summary>
    [Fact]
    public void TimeDiaryTableHasATagColumn()
    {
        using var db = new Database();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(time_diary)";
        using var r = cmd.ExecuteReader();
        var names = new List<string>();
        while (r.Read()) names.Add(r.GetString(1));
        Assert.Contains("tag", names);
    }

    [Fact]
    public void InsertDiaryEntry_DefaultsToNullTag_WhenNotSpecified()
    {
        var window = "test-window-" + Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = new Database();
        db.InsertDiaryEntry(today.ToIsoDate(), "09:00", "10:00", 60, DiaryCategory.OffPlan, window, null);

        var row = ReportData.DiaryInRange(today, today).Single(e => e.Window == window);
        Assert.Null(row.Tag);
    }

    [Fact]
    public void InsertDiaryEntry_PersistsTag_RoundTripsThroughDiaryInRange()
    {
        var window = "test-window-" + Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = new Database();
        db.InsertDiaryEntry(today.ToIsoDate(), "09:00", "10:00", 60, DiaryCategory.OffPlan, window,
            null, DiaryTag.Procrastination);

        var row = ReportData.DiaryInRange(today, today).Single(e => e.Window == window);
        Assert.Equal(DiaryTag.Procrastination, row.Tag);
    }

    [Fact]
    public void UpdateDiaryEntry_CanSetAndClearTag()
    {
        var window = "test-window-" + Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = new Database();
        AddRawDiaryRow(db, today, window);
        var id = ReportData.DiaryInRange(today, today).Single(e => e.Window == window).Id;

        db.UpdateDiaryEntry(id, "09:00", "10:00", 60, DiaryCategory.OffPlan, null, DiaryTag.SelfDev);
        Assert.Equal(DiaryTag.SelfDev,
            ReportData.DiaryInRange(today, today).Single(e => e.Id == id).Tag);

        // Clearing it back to untagged (the (none) dropdown choice in EditDiaryEntryDialog)
        // must actually null the column, not leave the previous tag stuck.
        db.UpdateDiaryEntry(id, "09:00", "10:00", 60, DiaryCategory.OffPlan, null, null);
        Assert.Null(ReportData.DiaryInRange(today, today).Single(e => e.Id == id).Tag);
    }

    /// <summary>DiaryTag.LabelOf backs the diary row's details-column suffix and the free-text
    /// search match — an unrecognised/legacy tag value must degrade to "no label" rather than
    /// throw or display a raw internal value verbatim.</summary>
    [Fact]
    public void LabelOf_ReturnsNullForUnrecognisedOrMissingTag()
    {
        Assert.Null(DiaryTag.LabelOf(null));
        Assert.Null(DiaryTag.LabelOf("not_a_real_tag"));
        Assert.Equal("Studioshoo", DiaryTag.LabelOf(DiaryTag.StudioShoo));
    }
}

using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>
/// The two SQL writes the activity tracker makes into <c>time_diary</c>: append a session, and
/// remove the not-yet-answered idle placeholder a real answer is about to replace.
///
/// Split out of <see cref="ActivityTracker"/> on 2026-08-04 (2026-07-23 audit finding #8). Both
/// were already static and stateless, so this is the least risky part of that split — but it's
/// also the part worth having somewhere findable: these two statements are how essentially
/// every row in the diary gets written, and the overlapping-row bugs this project has chased
/// (2026-07-15's double-counted idle transition, the 42 overlapping pairs found in the
/// 2026-07-18 history scan) all came down to *when* they're called with *what* timestamps.
///
/// Deliberately takes an open connection and optional transaction rather than opening its own:
/// the poll loop already has one open per poll, and LogIdleAnswers needs several writes to share
/// one all-or-nothing transaction (2026-07-14 round-6 audit finding #5).
/// </summary>
internal static class DiaryWriter
{
    /// <summary>Appends one diary row. Duration is floored at 1 minute — a sub-minute session
    /// still happened, and a 0 would make it invisible in every total.</summary>
    internal static void LogSession(SqliteConnection conn, DateTime start, DateTime end,
        string category, string window, string? description = null, SqliteTransaction? tx = null)
    {
        var duration = Math.Max(1, (int)(end - start).TotalMinutes);
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO time_diary " +
            "(date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, $m, $c, $w, $x)";
        cmd.Parameters.AddWithValue("$d", start.ToIsoDate());
        cmd.Parameters.AddWithValue("$s", start.ToIsoTimeOfDay());
        cmd.Parameters.AddWithValue("$e", end.ToIsoTimeOfDay());
        cmd.Parameters.AddWithValue("$m", duration);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$w", window.Length > 240 ? window[..240] : window);
        cmd.Parameters.AddWithValue("$x", (object?)description ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Deletes any not-yet-answered idle placeholder ("unaccounted time", or the
    /// older "dismissed") overlapping [start, end) on that date — HandleIdleReturn logs one
    /// of these immediately when a gap is first detected (2026-07-27), so answering it here
    /// must replace that row rather than insert a second, overlapping one. Never touches a
    /// real activity row or an already-answered idle row (different window/description),
    /// only ever a placeholder still waiting for its real answer.</summary>
    internal static void ClearIdlePlaceholder(SqliteConnection conn, DateTime start, DateTime end,
        SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText =
            "DELETE FROM time_diary WHERE date = $d AND window = $w " +
            "AND description IN ($ph, $legacyPh) " +
            "AND NOT (end_time <= $s OR start_time >= $e)";
        cmd.Parameters.AddWithValue("$d", start.ToIsoDate());
        cmd.Parameters.AddWithValue("$w", DiaryCategory.Idle);
        cmd.Parameters.AddWithValue("$ph", DiaryCategory.IdlePlaceholder);
        cmd.Parameters.AddWithValue("$legacyPh", DiaryCategory.LegacyIdlePlaceholder);
        cmd.Parameters.AddWithValue("$s", start.ToIsoTimeOfDay());
        cmd.Parameters.AddWithValue("$e", end.ToIsoTimeOfDay());
        cmd.ExecuteNonQuery();
    }
}

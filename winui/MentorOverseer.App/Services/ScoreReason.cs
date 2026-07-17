namespace MentorOverseer.App.Services;

/// <summary>
/// Central definition of score_ledger's "reason" tags — previously typed out as bare
/// string literals in roughly ten spots across ScoreService.cs and Database.cs's own
/// schema-migration SQL (the sl_reason_date unique index's WHERE clause, per
/// Database.cs's comment, needed a drop-and-recreate migration specifically because that
/// WHERE-clause reason list had drifted from the code once already). Same reasoning as
/// DiaryCategory: a typo in any one of these compiles fine and just silently stops
/// matching, with nothing to catch it (2026-07-18 audit finding R8-09).
/// </summary>
public static class ScoreReason
{
    public const string DailyScore = "daily_score";
    public const string OverdueAccrual = "overdue_accrual";
    public const string WeeklyComebackBonus = "weekly_comeback_bonus";
    public const string ReplanOverdue = "replan_overdue";
}

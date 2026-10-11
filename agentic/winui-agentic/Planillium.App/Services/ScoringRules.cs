namespace Planillium.App.Services;

/// <summary>
/// The one table of every `scoring` knob in config.json: its key, its default, the range
/// Settings will accept for it, and the label it's shown under.
///
/// It exists because the defaults used to be hand-typed at each call site — `("task_overdue_
/// penalty", -5)` appeared three separate times, and five more rules were compile-time consts
/// in <see cref="ScoreService"/> with no config presence at all until 2026-08-04. Adding a
/// Settings UI would have made that worse: the page needs a default to prefill each box with,
/// which is a fresh copy of every number, in a second file, free to drift from the formula it
/// is supposed to describe. This is exactly the "fix one sibling, miss the other" shape this
/// project keeps re-learning, so there is now one list and everything reads from it.
///
/// Adding a rule means adding one entry here — the Settings section builds itself from this
/// list, so it needs no separate edit.
/// </summary>
public static class ScoringRules
{
    /// <param name="Key">config.json key inside the "scoring" object.</param>
    /// <param name="Label">Shown as the input's header in Settings — kept short, since a
    /// NumberBox header doesn't wrap and silently clips when it overflows.</param>
    /// <param name="Default">Used when the key is absent, and to prefill Settings.</param>
    /// <param name="Min">Lower bound Settings accepts. Penalties are capped at 0 on the
    /// positive side deliberately: a positive "penalty" would award points for missing work,
    /// which reads as a typo rather than an intent.</param>
    public sealed record Rule(string Key, string Label, int Default, int Min, int Max);

    public static readonly IReadOnlyList<Rule> All = new[]
    {
        new Rule("task_completed", "Per task completed", 10, 0, 500),
        new Rule("multi_task_bonus_per_extra_task", "Bonus per extra task that day", 3, 0, 500),
        new Rule("task_overdue_penalty", "Per task missed", -5, -500, 0),
        new Rule("on_plan_hour", "Per hour on-plan", 3, 0, 500),
        new Rule("off_plan_hour", "Per hour off-plan", -2, -500, 0),
        new Rule("streak_bonus_per_day", "Streak bonus per day", 5, 0, 500),
        new Rule(ScoreReason.WeeklyComebackBonus, "Weekly comeback bonus", 20, 0, 500),
        new Rule("daily_floor", "Worst a single day can score", -10, -500, 0),
        new Rule("overdue_accrual_cap_days", "Overdue penalty repeats for (days)", 3, 0, 90),
        new Rule("replan_flat_fee", "Replan all overdue (one-off)", -10, -500, 0),
        new Rule("great_day_threshold", "A “great day” starts at", 20, 0, 500),
        new Rule("comeback_lookback_days", "Comeback window (days)", 7, 1, 90),
    };

    private static readonly Dictionary<string, Rule> ByKey =
        All.ToDictionary(r => r.Key, StringComparer.Ordinal);

    /// <summary>The default for <paramref name="key"/>. Throws on an unknown key rather than
    /// inventing a zero: every caller is naming a compile-time-known rule, so an unknown key
    /// means a typo, and silently scoring everything as 0 would be a very quiet way to break
    /// the whole economy.</summary>
    public static int DefaultFor(string key) => ByKey.TryGetValue(key, out var rule)
        ? rule.Default
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not a known scoring rule key.");
}

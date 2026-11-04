using System.Text.Json;

namespace Planillium.App.Services;

/// <summary>
/// Decides whether a window title (or an idle answer the user typed) counts as on-plan,
/// off-plan or neutral, by case-insensitive substring match against the keyword lists in
/// config.json — the same lists Settings' ACTIVITY KEYWORDS boxes edit and
/// <see cref="ConfigService.LearnActivityRule"/> teaches into.
///
/// Split out of <see cref="ActivityTracker"/> on 2026-08-04 (2026-07-23 audit finding #8). The
/// keyword lists are read once at construction and never mutated, and nothing here touches the
/// tracker's session/idle state — so this moved out whole, with the call sites left as
/// forwarders on the tracker so no caller had to change.
///
/// Note what deliberately did NOT move: the tracker's EffectiveClass, which downgrades off-plan
/// to "paid" while bought entertainment time is running. That reads PaidUntil, which is live
/// tracker state written from the UI thread — pulling it in here would have dragged a lock and
/// a mutable field into what is otherwise a pure function of config.
/// </summary>
internal sealed class ActivityClassifier
{
    private readonly List<string> _onPlan;
    private readonly List<string> _offPlan;
    private readonly List<string> _idleOnPlan;
    private readonly List<string> _idleOffPlan;
    private readonly List<string> _idleNeutral;

    internal ActivityClassifier(JsonElement config)
    {
        static List<string> Words(JsonElement cfg, string section, string key)
        {
            var list = new List<string>();
            if (cfg.TryGetProperty(section, out var s) && s.TryGetProperty(key, out var arr))
                foreach (var v in arr.EnumerateArray())
                    if (v.GetString() is { Length: > 0 } str) list.Add(str.ToLowerInvariant());
            return list;
        }

        _onPlan = Words(config, "activity_rules", DiaryCategory.OnPlan);
        _offPlan = Words(config, "activity_rules", DiaryCategory.OffPlan);
        _idleOnPlan = Words(config, "idle_activity_rules", DiaryCategory.OnPlan);
        _idleOffPlan = Words(config, "idle_activity_rules", DiaryCategory.OffPlan);
        _idleNeutral = Words(config, "idle_activity_rules", DiaryCategory.Neutral);
    }

    // G9b: learned combination rules (app + title words). Swapped whole, never mutated, so the
    // poll thread can read it while the UI thread replaces it after a rule change.
    private volatile RuleBook _rules = RuleBook.Empty;

    internal RuleBook Rules => _rules;
    internal void SetRules(RuleBook rules) => _rules = rules;

    /// <summary>Category plus how a learned rule took part: the most specific matching rule governs
    /// (G9b). When its evidence clears the bar it decides the category alone ("auto"); otherwise it
    /// only leaves a suggestion ("suggested") and the keyword lists still decide the category.
    /// <c>RuleState</c> is null when no rule matched. Never throws: a rule problem must not stop tracking.</summary>
    internal (string Category, string? RuleState) ClassifyDetailed(string title)
    {
        var (cat, state, _) = ClassifyRule(title);
        return (cat, state);
    }

    /// <summary>As <see cref="ClassifyDetailed"/>, plus the diary tag a confident rule carries (G9c;
    /// null for category-only rules, suggestions and no match).</summary>
    internal (string Category, string? RuleState, string? Tag) ClassifyRule(string title)
    {
        try
        {
            var hit = _rules.Match(AppNames.Group(title), title);
            if (hit is { Confident: true }) return (hit.Rule.Category, RuleStates.Auto, hit.Rule.Tag);
            if (hit != null) return (Classify(title), RuleStates.Suggested, null);
        }
        catch (Exception ex) { Log.Error("ActivityClassifier.ClassifyDetailed", ex); }
        return (Classify(title), null, null);
    }

    internal string Classify(string title)
    {
        var t = title.ToLowerInvariant();
        foreach (var kw in _onPlan) if (t.Contains(kw)) return DiaryCategory.OnPlan;
        foreach (var kw in _offPlan) if (t.Contains(kw)) return DiaryCategory.OffPlan;
        return DiaryCategory.Neutral;
    }

    /// <summary>Falls back to <see cref="DiaryCategory.Idle"/>, not neutral: an unmatched idle
    /// answer is unclassified time, and scoring treats those differently.</summary>
    internal string ClassifyIdleText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return DiaryCategory.Idle;
        var t = description.ToLowerInvariant();
        foreach (var kw in _idleOnPlan) if (t.Contains(kw)) return DiaryCategory.OnPlan;
        foreach (var kw in _idleOffPlan) if (t.Contains(kw)) return DiaryCategory.OffPlan;
        foreach (var kw in _idleNeutral) if (t.Contains(kw)) return DiaryCategory.Neutral;
        return DiaryCategory.Idle;
    }
}

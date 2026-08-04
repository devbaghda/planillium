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

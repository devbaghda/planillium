using System.Text.Json;
using System.Text.Json.Nodes;

namespace Planillium.App.Services;

/// <summary>A diary tag whose time counts as paid work, with its own monthly net rate.
/// The hourly value is the monthly figure ÷ <see cref="IncomeService.WorkingHoursPerMonth"/>
/// (21 days × 8 h, user decision 2026-10-01).</summary>
public sealed record PaidTag(string Value, string Label, double MonthlyEur)
{
    public double HourlyEur => MonthlyEur / IncomeService.WorkingHoursPerMonth;
}

/// <summary>Payable tags, stored under the top-level "paid_tags" key of config.json (kept out of
/// the "income" object, which SettingsPage.SaveIncome rewrites wholesale). A key that is absent
/// means the shipped default — Studioshoo at 1600 €/month; a key that is present, even as an empty
/// array, is exactly what the user saved. A tag added here that isn't one of the five built-in
/// diary tags becomes a real diary tag too (<see cref="DiaryTag.Options"/>).</summary>
public static class PaidTags
{
    private static readonly PaidTag[] Defaults = { new(DiaryTag.StudioShoo, "Studioshoo", 1600) };

    public static IReadOnlyList<PaidTag> All()
    {
        if (!ConfigService.Root.TryGetProperty("paid_tags", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Defaults;
        var list = new List<PaidTag>();
        foreach (var e in arr.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            var value = e.TryGetProperty("tag", out var v) ? v.GetString() : null;
            var label = e.TryGetProperty("label", out var l) ? l.GetString() : null;
            if (string.IsNullOrWhiteSpace(value)) continue;
            var eur = e.TryGetProperty("monthly_net_eur", out var m) && m.TryGetDouble(out var d) && d >= 0 ? d : 0;
            list.Add(new PaidTag(value, string.IsNullOrWhiteSpace(label) ? value : label, eur));
        }
        return list;
    }

    public static void Save(IEnumerable<PaidTag> tags) =>
        ConfigService.Mutate(cfg =>
        {
            var arr = new JsonArray();
            foreach (var t in tags)
                arr.Add(new JsonObject { ["tag"] = t.Value, ["label"] = t.Label, ["monthly_net_eur"] = t.MonthlyEur });
            cfg["paid_tags"] = arr;
        });

    /// <summary>Stored tag value for a typed name: lower-case letters and digits only, matching
    /// the built-in values ("studioshoo"). Empty when the name has none.</summary>
    public static string Slug(string label) =>
        new string(label.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

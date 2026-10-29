using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Planillium.App.Services;

/// <summary>
/// Monthly AI cost cap (G9a, 2026-10-29; DECISIONS rule 19 e). Plain C#, no WinUI types.
/// The Anthropic API does not tell the app what a call cost, so the cost is ESTIMATED: the token counts
/// the reply stream reports (or, if absent, characters / 4) times a fixed per-million-token price chosen by
/// model family. The prices are the list prices in USD used as EUR (no conversion), which slightly
/// over-states the spend: the cap stops early rather than late. Spend and cap live in config.json's "ai"
/// block ("cap_eur", "spend_month" = "yyyy-MM", "spend_eur"); a stored month other than the current one
/// counts as zero, so a new calendar month starts at zero with no reset job.
/// </summary>
public static class AiSpend
{
    public const double DefaultCapEur = 5.0;

    /// <summary>Clock, replaceable so the month boundary can be checked.</summary>
    internal static Func<DateTime> Now { get; set; } = () => DateTime.Now;

    private static readonly object Gate = new();

    public static string MonthKey(DateTime d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static double CapEur
    {
        get
        {
            if (Ai(out var ai) && ai.TryGetProperty("cap_eur", out var v) && v.ValueKind == JsonValueKind.Number &&
                v.TryGetDouble(out var d) && d > 0 && double.IsFinite(d)) return d;
            return DefaultCapEur;
        }
    }

    /// <summary>Estimated spend so far in the current calendar month (0 when the stored month is older).</summary>
    public static double SpentThisMonth => SpentIn(Now());

    public static double SpentIn(DateTime when)
    {
        if (!Ai(out var ai)) return 0;
        if (!ai.TryGetProperty("spend_month", out var m) || m.ValueKind != JsonValueKind.String ||
            m.GetString() != MonthKey(when)) return 0;
        return ai.TryGetProperty("spend_eur", out var v) && v.ValueKind == JsonValueKind.Number &&
               v.TryGetDouble(out var d) && d > 0 && double.IsFinite(d) ? d : 0;
    }

    /// <summary>Validates and saves a cap typed by the user. Returns null when saved, else the message to show.</summary>
    public static string? TrySaveCap(string? text)
    {
        var t = new string((text ?? "").Where(c => !char.IsWhiteSpace(c) && c != '€').ToArray());
        if (t.Length == 0) return "Enter a monthly cap in euro, for example 5.";
        // One decimal separator, comma or dot; nothing else.
        t = t.Replace(',', '.');
        if (!double.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var cap) || !double.IsFinite(cap))
            return "The cap must be a number, for example 5 or 7.50.";
        if (cap <= 0) return "The cap must be more than 0 (it is the most the AI may cost in a month).";
        if (cap > 10000) return "That cap is unreasonably high. Enter at most 10000.";
        cap = Math.Round(cap, 2);
        lock (Gate)
            ConfigService.Mutate(root =>
            {
                var ai = root["ai"] as JsonObject ?? new JsonObject();
                ai["cap_eur"] = cap;
                root["ai"] = ai;
            });
        return null;
    }

    /// <summary>Non-null (the message for the user) when this month's spend has reached the cap.</summary>
    public static string? CapReachedMessage()
    {
        var spent = SpentThisMonth;
        var cap = CapEur;
        if (spent < cap) return null;
        return $"The monthly AI cap is reached: about {Eur(spent)} spent this month of {Eur(cap)}. " +
               "AI is switched off until next month. To keep using it now, raise the cap in Settings → AI. " +
               "Nothing was lost and everything else in the app works as before.";
    }

    public static string Eur(double v) => "€" + v.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Estimated cost in EUR of one call; see the class comment for how it is estimated.</summary>
    public static double EstimateEur(string model, long inputTokens, long outputTokens)
    {
        var m = (model ?? "").ToLowerInvariant();
        var (pin, pout) = m.Contains("opus") ? (15.0, 75.0) : m.Contains("haiku") ? (1.0, 5.0) : (3.0, 15.0);
        return (inputTokens * pin + outputTokens * pout) / 1_000_000.0;
    }

    /// <summary>Adds one answered call to this month's spend.</summary>
    public static void Record(string model, long inputTokens, long outputTokens)
    {
        var cost = EstimateEur(model, inputTokens, outputTokens);
        if (cost <= 0 || !double.IsFinite(cost)) return;
        try
        {
            lock (Gate)
            {
                var now = Now();
                var total = SpentIn(now) + cost;
                ConfigService.Mutate(root =>
                {
                    var ai = root["ai"] as JsonObject ?? new JsonObject();
                    ai["spend_month"] = MonthKey(now);
                    ai["spend_eur"] = Math.Round(total, 6);
                    root["ai"] = ai;
                });
            }
        }
        catch (Exception ex) { Log.Error("AiSpend.Record", ex); }
    }

    private static bool Ai(out JsonElement ai)
    {
        if (ConfigService.Root.TryGetProperty("ai", out ai) && ai.ValueKind == JsonValueKind.Object) return true;
        ai = default;
        return false;
    }
}

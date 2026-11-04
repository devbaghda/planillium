using System.Globalization;
using System.Text.Json;

namespace Planillium.App.Services;

/// <summary>
/// G9a (DECISIONS rule 19 e): the monthly AI cost cap. Plain C# with no WinUI types, so the test
/// project can link it. Anthropic does not send back a price, so the cost of a call is an
/// ESTIMATE: the token counts the reply reports (<c>usage</c>) times the list-price table in
/// <see cref="AiCostEstimate"/>. The spend is "money the app thinks it used", never an invoice.
/// </summary>
public static class AiCostEstimate
{
    /// <summary>US dollars per million tokens (input, output), by model family. List prices as
    /// understood at build time. When Anthropic changes prices, edit this table only.
    /// Opus uses the older, higher price on purpose: an estimate that is too high stops sooner.</summary>
    public static readonly IReadOnlyList<(string Family, double InputUsdPerMTok, double OutputUsdPerMTok)> PriceTable =
        new (string, double, double)[]
        {
            ("haiku", 1.00, 5.00),
            ("sonnet", 3.00, 15.00),
            ("opus", 15.00, 75.00),
        };

    /// <summary>Used for a model name that matches no family: the dearest row, so an unknown
    /// model is never under-counted.</summary>
    public static readonly (double InputUsdPerMTok, double OutputUsdPerMTok) UnknownModelPrice = (15.00, 75.00);

    /// <summary>Dollar-to-euro rate used for every estimate (rounded up on purpose).</summary>
    public const double UsdToEur = 0.95;

    /// <summary>Characters per token assumed when a reply carries no <c>usage</c> block
    /// (3 is pessimistic; real text averages 3.5-4).</summary>
    public const double CharsPerTokenWhenUnknown = 3.0;

    public static (double In, double Out) PriceFor(string model)
    {
        foreach (var (family, i, o) in PriceTable)
            if (model.Contains(family, StringComparison.OrdinalIgnoreCase)) return (i, o);
        return UnknownModelPrice;
    }

    /// <summary>Estimated cost in EUR of one call with the given token counts.</summary>
    public static double Eur(string model, long inputTokens, long outputTokens)
    {
        var (pi, po) = PriceFor(model);
        return (inputTokens * pi + outputTokens * po) / 1_000_000.0 * UsdToEur;
    }

    /// <summary>Cost of a successful HTTP reply: uses the reply's <c>usage</c> counts; when they are
    /// missing, falls back to the prompt and reply lengths (<see cref="CharsPerTokenWhenUnknown"/>).</summary>
    public static double EurFromReply(string model, string rawBody, string prompt)
    {
        long inTok = -1, outTok = -1;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
            {
                if (u.TryGetProperty("input_tokens", out var a) && a.TryGetInt64(out var ai)) inTok = ai;
                if (u.TryGetProperty("output_tokens", out var b) && b.TryGetInt64(out var bo)) outTok = bo;
            }
        }
        catch (JsonException) { /* unreadable body: estimate from lengths below */ }

        if (inTok < 0) inTok = (long)Math.Ceiling(prompt.Length / CharsPerTokenWhenUnknown);
        if (outTok < 0) outTok = (long)Math.Ceiling(rawBody.Length / CharsPerTokenWhenUnknown);
        return Eur(model, inTok, outTok);
    }
}

public static class AiSpendCap
{
    public const double DefaultCapEur = 5.0;
    public const double MaxCapEur = 10000.0;

    public const string CapHelp = "Enter an amount in euros, for example 5 or 7.50.";

    /// <summary>The one refusal for a zero or negative cap (G11).</summary>
    public const string CapNotPositive = "Not saved: the monthly limit must be more than 0. " + CapHelp;

    /// <summary>Calendar-month key ("2026-10") of the given local time.</summary>
    public static string MonthKey(DateTime now) => now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>The month's spend: the stored amount if it belongs to the month of <paramref name="now"/>,
    /// otherwise zero (a new calendar month starts at zero).</summary>
    public static double SpendFor(string? storedMonth, double storedEur, DateTime now) =>
        storedMonth == MonthKey(now) && storedEur > 0 ? storedEur : 0.0;

    /// <summary>The stored amount after adding <paramref name="eur"/>: restarts from zero when the
    /// stored month is not the current one.</summary>
    public static (string Month, double Eur) Added(string? storedMonth, double storedEur, DateTime now, double eur) =>
        (MonthKey(now), SpendFor(storedMonth, storedEur, now) + Math.Max(0.0, eur));

    public static bool IsReached(double spentEur, double capEur) => spentEur >= capEur;

    /// <summary>Why the cap box value is refused, or null when <paramref name="text"/> is a valid
    /// cap (then <paramref name="capEur"/> is set). Empty, text, negative, zero and absurdly large
    /// values are refused; both "7.50" and "7,50" are read as 7.5.</summary>
    public static string? ValidateCap(string? text, out double capEur)
    {
        capEur = 0;
        var t = (text ?? "").Trim().Replace("€", "").Replace("EUR", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (t.Length == 0) return "Not saved: the monthly limit is empty. " + CapHelp;
        if (t.Contains(',') && !t.Contains('.')) t = t.Replace(',', '.');
        if (!decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var d))
            return "Not saved: the monthly limit must be a number. " + CapHelp;
        // G11: zero and negative get one wording ("-5" used to say "cannot be negative").
        if (d <= 0) return CapNotPositive;
        if (d > (decimal)MaxCapEur) return $"Not saved: the monthly limit is too large (at most {MaxCapEur:0} euros).";
        capEur = (double)Math.Round(d, 2);
        if (capEur <= 0) return "Not saved: the monthly limit must be at least 0.01 euros.";
        return null;
    }

    /// <summary>The cap as shown in a box: "5" or "7.5", always with a dot.</summary>
    public static string CapText(double capEur) => capEur.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>"€1.20 of €5.00 used this month" for Settings.</summary>
    public static string StatusLine(double spentEur, double capEur) =>
        $"This month: {EurFormatter.Format(spentEur)} of {EurFormatter.Format(capEur)} used (estimated).";

    /// <summary>The message shown when a call is refused: says the cap was reached, the amount spent
    /// and where to raise it.</summary>
    public static string RefusalMessage(double spentEur, double capEur) =>
        $"The monthly AI limit was reached: {EurFormatter.Format(spentEur)} spent this month, limit {EurFormatter.Format(capEur)}. " +
        "Nothing was sent. Raise the limit in Settings -> AI, or wait for the next month.";
}

/// <summary>
/// The app's real spend store: the cap lives in config.json (<c>ai.monthly_cap_eur</c>), the month's
/// spend in winui_state.json, both under <see cref="AppPaths.Root"/> (so a scratch run uses its own
/// folder). Called from the UI thread only, like StateService.
/// </summary>
public static class AiSpend
{
    public static double CapEur => ConfigService.AiMonthlyCapEur;

    public static double SpentThisMonth(DateTime? now = null)
    {
        var s = StateService.Load();
        return AiSpendCap.SpendFor(s.AiSpendMonth, s.AiSpendEur, now ?? DateTime.Now);
    }

    /// <summary>Null when a call may go ahead; otherwise the message to show. Checked before any request.</summary>
    public static string? RefusalOrNull()
    {
        var spent = SpentThisMonth();
        var cap = CapEur;
        return AiSpendCap.IsReached(spent, cap) ? AiSpendCap.RefusalMessage(spent, cap) : null;
    }

    /// <summary>G11: raised after every <see cref="Record"/>, on the caller's (UI) thread, so a visible
    /// spend line (Settings -> AI) refreshes after any successful AI call, wherever it was made.</summary>
    public static event Action? Changed;

    public static void Record(double eur)
    {
        var s = StateService.Load();
        var (month, total) = AiSpendCap.Added(s.AiSpendMonth, s.AiSpendEur, DateTime.Now, eur);
        s.AiSpendMonth = month;
        s.AiSpendEur = total;
        StateService.Save(s);
        Changed?.Invoke();
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Planillium.App.Services;

/// <summary>
/// Why an AI call did not produce a reply. Every category has one plain-language message
/// (<see cref="AiMessages"/>) built from the category alone, so no key, URL, HTTP body or
/// exception text can ever reach the user through this type.
/// </summary>
public enum AiFailure
{
    NoKey, Rejected401, ModelNotFound404, RateLimited429, ServerError5xx, BadRequest400,
    Timeout, Network, Cancelled, Truncated, EmptyReply, Unreadable,
}

/// <summary>Success text, or a failure category. Exactly one of the two is set.</summary>
public sealed record AiResult(string? Text, AiFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public string Message => Failure is { } f ? AiMessages.For(f) : string.Empty;

    public static AiResult Success(string text) => new(text, null);

    public static AiResult Fail(AiFailure failure) => new(null, failure);
}

/// <summary>The exact user-facing copy for each failure category (ai-layer spec, D4).</summary>
public static class AiMessages
{
    public static string For(AiFailure failure) => failure switch
    {
        AiFailure.NoKey => "No API key is saved. Add one in Settings -> AI.",
        AiFailure.Rejected401 => "Anthropic did not accept the API key. Check it in Settings -> AI and save it again.",
        AiFailure.ModelNotFound404 => "Anthropic does not know the model name. Check the Model field in Settings -> AI.",
        AiFailure.RateLimited429 => "Anthropic is limiting requests right now (too many, or no credit left on the account). Wait a minute and try again.",
        AiFailure.ServerError5xx => "Anthropic had a problem on its side. Try again in a few minutes.",
        AiFailure.BadRequest400 => "Anthropic could not accept this request — it may be too long.",
        AiFailure.Timeout => $"Claude took longer than {AiClient.TimeoutMinutesText}, so the request was stopped.",
        AiFailure.Network => "Planillium could not reach Anthropic. Check your internet connection.",
        AiFailure.Cancelled => "Stopped. Nothing was changed.",
        AiFailure.Truncated => "Claude's reply was cut off before it finished, so it was not used.",
        AiFailure.EmptyReply => "Claude sent back an empty reply.",
        AiFailure.Unreadable => "Anthropic's answer was not in a form this app understands.",
        _ => "Something went wrong.",
    };
}

/// <summary>
/// Settings the AI layer reads. The base-URL override is compiled in only for DEBUG and test
/// builds, exactly like MENTOR_ROOT in AppPaths: a Release build always talks to Anthropic, so
/// an environment variable can never redirect the user's key somewhere else.
/// </summary>
public static class AiSettings
{
    /// <summary>The one place the default model id is written down.</summary>
    public const string DefaultModel = "claude-sonnet-5-5";
    public const string ProductionBaseUrl = "https://api.anthropic.com";
    public const string BaseUrlEnvironmentVariable = "PLANILLIUM_AI_BASE_URL";
    public const string CredentialName = "anthropic_api_key";
    /// <summary>Used only while the test override is active, so QA's fake keys never touch the
    /// user's real key in Credential Manager.</summary>
    public const string TestCredentialName = "anthropic_api_key_test";
    public const string TestConnectionPrompt = "Reply with the word OK.";
    public const int TestConnectionMaxTokens = 16;

    /// <summary>True only in DEBUG/test builds with PLANILLIUM_AI_BASE_URL set.</summary>
    public static bool TestOverrideActive
    {
        get
        {
#if DEBUG || PLANILLIUM_TESTS
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable));
#else
            return false;
#endif
        }
    }

    /// <summary>True in a scratch run (MENTOR_ROOT set) without the local stub: no AI key is read.</summary>
    public static bool ScratchBlocked => AppPaths.IsScratchRun && !TestOverrideActive;
    public const string ScratchBlockedMessage =
        "AI is off in this test copy (MENTOR_ROOT is set). Set PLANILLIUM_AI_BASE_URL to a local stub to use it.";

    public static string BaseUrl()
    {
#if DEBUG || PLANILLIUM_TESTS
        var env = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
#endif
        return ProductionBaseUrl;
    }

    /// <summary>The Credential Manager name the key is stored under (see AiKey).</summary>
    public static string CredentialKey => TestOverrideActive ? TestCredentialName : CredentialName;
}

/// <summary>
/// Sends one prompt to the Anthropic Messages API and returns the reply text or a failure
/// category. Non-streaming, no system prompt, no metadata: the body carries the prompt and
/// nothing else. No WinUI types and no Credential Manager access here — the key comes in through
/// <c>keyProvider</c>, so tests can inject a fake key and a local stub server.
/// </summary>
public sealed class AiClient
{
    public const int MaxTokens = 16000;
    public const string ApiVersion = "2023-06-01";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(360);

    /// <summary>The timeout as shown to the user ("6 minutes"); follows <see cref="DefaultTimeout"/>.</summary>
    public static string TimeoutMinutesText
    {
        get
        {
            int m = (int)Math.Round(DefaultTimeout.TotalMinutes);
            return m == 1 ? "1 minute" : m + " minutes";
        }
    }

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly Func<string?> _keyProvider;
    private readonly TimeSpan _timeout;

    public AiClient(HttpClient http, string baseUrl, string model, Func<string?> keyProvider, TimeSpan timeout)
    {
        _http = http;
        _baseUrl = baseUrl;
        _model = model;
        _keyProvider = keyProvider;
        _timeout = timeout;
    }

    /// <summary>
    /// Sends <paramref name="prompt"/> as the single user message. <paramref name="maxTokens"/>
    /// is only overridden by Test connection (16); every real request uses <see cref="MaxTokens"/>.
    /// Never throws: every outcome is an <see cref="AiResult"/>.
    /// </summary>
    public async Task<AiResult> AskAsync(string prompt, CancellationToken ct, int maxTokens = MaxTokens)
    {
        var key = _keyProvider()?.Trim();
        if (string.IsNullOrEmpty(key)) return AiResult.Fail(AiFailure.NoKey);

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl.TrimEnd('/') + "/v1/messages");
            request.Headers.TryAddWithoutValidation("x-api-key", key);
            request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
            var body = new JsonObject
            {
                ["model"] = _model,
                ["max_tokens"] = maxTokens,
                ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = prompt }),
            };
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await _http.SendAsync(request, linked.Token);
            var status = (int)response.StatusCode;
            var raw = await response.Content.ReadAsStringAsync(linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                var failure = MapStatus(status);
                Log.Warn("AiClient", $"{status} ({failure})");
                return AiResult.Fail(failure);
            }
            // Stop was pressed at the same moment the reply arrived: one outcome, and it is Cancelled.
            if (ct.IsCancellationRequested) return AiResult.Fail(AiFailure.Cancelled);
            return ParseReply(raw);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested) return AiResult.Fail(AiFailure.Cancelled);
            Log.Warn("AiClient", "timeout");
            return AiResult.Fail(AiFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            Log.Warn("AiClient", "network");
            return AiResult.Fail(AiFailure.Network);
        }
        catch (Exception ex)
        {
            // Type name only: the exception message is never logged or shown.
            Log.Warn("AiClient", $"unexpected {ex.GetType().Name}");
            return AiResult.Fail(AiFailure.Unreadable);
        }
    }

    private static AiFailure MapStatus(int status) => status switch
    {
        400 or 413 => AiFailure.BadRequest400,
        401 => AiFailure.Rejected401,
        404 => AiFailure.ModelNotFound404,
        429 => AiFailure.RateLimited429,
        >= 500 and < 600 => AiFailure.ServerError5xx,
        _ => AiFailure.Unreadable,
    };

    /// <summary>Reads a 200 body. Concatenates the text blocks of <c>content</c>; a cut-off reply
    /// (<c>stop_reason == "max_tokens"</c>) is refused even if it has text, so a partial JSON is
    /// never handed to the parser as if it were complete.</summary>
    internal static AiResult ParseReply(string raw)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return AiResult.Fail(AiFailure.Unreadable);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return AiResult.Fail(AiFailure.Unreadable);
            if (root.TryGetProperty("error", out _)) return AiResult.Fail(AiFailure.Unreadable);
            if (root.TryGetProperty("stop_reason", out var stop) &&
                stop.ValueKind == JsonValueKind.String && stop.GetString() == "max_tokens")
                return AiResult.Fail(AiFailure.Truncated);
            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                return AiResult.Fail(AiFailure.Unreadable);

            var text = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object &&
                    block.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
                    type.GetString() == "text" &&
                    block.TryGetProperty("text", out var part) && part.ValueKind == JsonValueKind.String)
                    text.Append(part.GetString());
            }
            if (string.IsNullOrWhiteSpace(text.ToString())) return AiResult.Fail(AiFailure.EmptyReply);
            return AiResult.Success(text.ToString());
        }
    }
}

/// <summary>
/// The rule that "Ask Claude" never overwrites a reply the user already has in the box
/// (ai-layer spec, D2.5; user decision 2026-10-13). Plain C#, so the test project can check it.
/// </summary>
public static class AiReplyBox
{
    /// <summary>Shown in the reason line when Ask Claude is pressed over existing text. Nothing is sent.</summary>
    public const string Blocked =
        "The reply box already has text — clear it first so Claude's answer doesn't overwrite it.";

    /// <summary>Shown when text appeared in the reply box while Claude was answering: the answer is dropped.</summary>
    public const string DiscardedAfterAnswer = Blocked + " Claude's answer was discarded.";

    /// <summary>True when the reply box holds any non-whitespace text, so a new call must not run.</summary>
    public static bool BlocksAsk(string? replyText) => !string.IsNullOrWhiteSpace(replyText);
}

/// <summary>
/// The problem message under a reply box, and when it lets go of it (defect D1, found in QA
/// 2026-10-13). The message is about one specific reply text. Editing the box to anything else
/// clears it. The reply box's change event also fires for the text Ask Claude itself fills in,
/// after the problem was already shown, and that fill must not clear it. Plain C#, so the test
/// project can check it.
/// </summary>
public sealed class ReplyErrorState
{
    private string? _about;

    /// <summary>The problem to show, or null when none is showing.</summary>
    public string? Message { get; private set; }

    /// <summary>Shows <paramref name="message"/> as a problem with <paramref name="replyText"/>, the text now in the box.</summary>
    public void Show(string replyText, string message)
    {
        _about = replyText;
        Message = message;
    }

    /// <summary>Called on every change of the reply box with its current text. Clears the message
    /// only when the box no longer holds the text the message is about.</summary>
    public void OnReplyChanged(string currentText)
    {
        if (Message is not null && currentText != _about) Message = null;
    }
}

/// <summary>
/// A cheap check that a reply is at least plan-shaped: JSON with the fields the importer requires.
/// Used only to tell the user, honestly, that a reply is not usable yet. Import itself still runs
/// its full check (AddPlanDialog.TryImport), and nothing is added on this check alone. The messages
/// match TryImport's.
/// </summary>
public static class PlanReplyCheck
{
    /// <summary>Null when the reply looks like a plan, otherwise the plain-language reason.</summary>
    public static string? Problem(string replyText)
    {
        var raw = replyText.Trim();
        if (raw.Length == 0) return "Paste Claude's reply first.";

        var fence = System.Text.RegularExpressions.Regex.Match(raw,
            "```(?:json)?\\s*(\\{.*?\\})\\s*```", System.Text.RegularExpressions.RegexOptions.Singleline);
        var json = fence.Success ? fence.Groups[1].Value
                 : raw.StartsWith('{') ? raw : null;
        if (json is null)
            return "Couldn't find valid JSON — paste the whole reply including the ```json block.";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            foreach (var field in new[] { "id", "name", "phases" })
                if (!root.TryGetProperty(field, out _))
                    return $"Plan is missing the required field '{field}'.";
            return null;
        }
        catch (JsonException)
        {
            return "That JSON doesn't parse — check it's the complete reply including the ```json fence.";
        }
    }
}

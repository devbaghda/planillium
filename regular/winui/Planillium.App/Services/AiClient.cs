using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Planillium.App.Services;

public enum AiFailure { NoKey, Auth, RateLimited, Server, Network, Timeout, BadResponse, Cancelled, ScratchBlocked }

/// <summary>An AI call that didn't produce a usable reply. <see cref="Exception.Message"/> is already
/// plain language for the dialog; it never contains the API key.</summary>
public sealed class AiException(AiFailure kind, string message) : Exception(message)
{
    public AiFailure Kind { get; } = kind;
}

/// <summary>Where the time of one successful reply went (G4, 2026-10-27): until the server answered,
/// until Claude's first words arrived, and in all; plus how big the reply was. Sizes and times only —
/// never any text.</summary>
public sealed record AiTiming(TimeSpan ToHeaders, TimeSpan ToFirstText, TimeSpan Total, int ReplyChars,
    int? InputTokens, int? OutputTokens);

/// <summary>
/// Minimal Anthropic Messages API client (2026-10-13 AI layer). Plain C# — no WinUI types — so it is
/// unit-testable against a local stub server. Streams the reply (server-sent events) so a long plan
/// doesn't hit a single wall-clock limit: the call fails only when no data arrives for
/// <see cref="IdleTimeout"/>, or after <see cref="TotalCap"/> in all. The API key is only ever put in the
/// request header — never in a URL, a log line or an exception message.
/// </summary>
public sealed class AiClient
{
    public const string DefaultModel = "claude-sonnet-5-5";
    /// <summary>Environment variable that redirects the client to a local stub (tests only).</summary>
    public const string BaseUrlEnv = "PLANILLIUM_AI_BASE_URL";
    private const string ProductionBase = "https://api.anthropic.com";
    private const int MaxTokens = 16000;

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly string _apiKey;
    private readonly string _model;
    /// <summary>Null only in a scratch run with no stub address: then no call is made at all (G7).</summary>
    private readonly string? _baseUrl;

    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan TotalCap { get; init; } = TimeSpan.FromMinutes(6);

    public AiClient(string apiKey, string model, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        // The env override exists for stub-server testing only. In a Release build it is not even read,
        // so nothing in the environment can redirect the real key to another server.
        string? fromEnv = null;
#if DEBUG || PLANILLIUM_TESTS
        if (Environment.GetEnvironmentVariable(BaseUrlEnv) is { Length: > 0 } env) fromEnv = env;
#endif
        // A scratch run (test copy on a MENTOR_ROOT data folder) never falls back to the real service: it
        // talks only to the stub named by PLANILLIUM_AI_BASE_URL, or to nothing (G7, 2026-10-27). A normal
        // run keeps the production address as before.
        fromEnv ??= AppPaths.IsScratch ? null : ProductionBase;
        _baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? fromEnv : baseUrl)?.TrimEnd('/');
    }

    /// <summary>Timing of the last successful <see cref="CompleteAsync"/> on this client (null before one).</summary>
    public AiTiming? LastTiming { get; private set; }

    /// <summary>Sends <paramref name="prompt"/> as a single user message and returns Claude's full reply text.</summary>
    /// <param name="onText">Called with each piece of the reply as it streams in (G4: lets the dialog show
    /// progress). Runs on a background thread; the returned reply is the same either way.</param>
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default, Action<string>? onText = null)
    {
        if (_baseUrl is null)
            throw new AiException(AiFailure.ScratchBlocked,
                "AI is off in this test copy (scratch data folder): set " + BaseUrlEnv +
                " to a local stub server to use it. Nothing was sent.");
        if (string.IsNullOrWhiteSpace(_apiKey)) throw new AiException(AiFailure.NoKey, "No API key is set.");

        var body = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = MaxTokens,
            ["stream"] = true,
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = prompt } },
        }.ToJsonString();

        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var clock = Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/messages")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            idle.CancelAfter(IdleTimeout);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, idle.Token).ConfigureAwait(false);
            var toHeaders = clock.Elapsed;
            TimeSpan? toFirstText = null;
            int? inputTokens = null, outputTokens = null;
            if (!resp.IsSuccessStatusCode)
                throw await FailureFor(resp, idle.Token).ConfigureAwait(false);

            await using var stream = await resp.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = new StringBuilder();
            string? stopReason = null;
            string? line;
            while ((line = await reader.ReadLineAsync(idle.Token).ConfigureAwait(false)) != null)
            {
                idle.CancelAfter(IdleTimeout);
                if (clock.Elapsed > TotalCap)
                    throw new AiException(AiFailure.Timeout, "Claude took too long to answer. Nothing was changed.");
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var json = line[5..].Trim();
                if (json.Length == 0 || json == "[DONE]") continue;

                JsonNode? evt;
                try { evt = JsonNode.Parse(json); }
                catch (JsonException) { continue; }
                switch ((string?)evt?["type"])
                {
                    case "content_block_delta":
                        if (evt?["delta"]?["text"] is JsonValue t && t.TryGetValue<string>(out var piece))
                        {
                            toFirstText ??= clock.Elapsed;
                            text.Append(piece);
                            onText?.Invoke(piece);
                        }
                        break;
                    case "message_start":
                        inputTokens = TokenCount(evt?["message"]?["usage"]?["input_tokens"]) ?? inputTokens;
                        break;
                    case "message_delta":
                        stopReason = (string?)evt?["delta"]?["stop_reason"] ?? stopReason;
                        outputTokens = TokenCount(evt?["usage"]?["output_tokens"]) ?? outputTokens;
                        break;
                    case "error":
                        throw new AiException(AiFailure.Server,
                            "Claude reported an error while answering: " + ((string?)evt?["error"]?["message"] ?? "unknown") +
                            ". Nothing was changed.");
                }
            }

            if (stopReason == "max_tokens")
                throw new AiException(AiFailure.BadResponse,
                    "Claude's reply was cut off (too long). Try asking for a smaller change.");
            if (text.Length == 0)
                throw new AiException(AiFailure.BadResponse, "Claude sent an empty reply.");
            LastTiming = new AiTiming(toHeaders, toFirstText ?? clock.Elapsed, clock.Elapsed, text.Length, inputTokens, outputTokens);
            Log.Info($"AiClient: reply {text.Length} chars, tokens in/out {inputTokens?.ToString() ?? "?"}/{outputTokens?.ToString() ?? "?"}; " +
                     $"server answered {toHeaders.TotalMilliseconds:F0} ms, first text {LastTiming.ToFirstText.TotalMilliseconds:F0} ms, " +
                     $"total {clock.Elapsed.TotalMilliseconds:F0} ms");
            return text.ToString();
        }
        catch (AiException) { throw; }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested) throw new AiException(AiFailure.Cancelled, "Cancelled.");
            throw new AiException(AiFailure.Timeout, "Claude didn't answer in time. Nothing was changed.");
        }
        catch (HttpRequestException ex)
        {
            Log.Warn("AiClient", $"network failure: {ex.GetType().Name}");
            throw new AiException(AiFailure.Network, "Couldn't reach the AI service. Check your internet connection.");
        }
        catch (IOException ex)
        {
            Log.Warn("AiClient", $"stream failure: {ex.GetType().Name}");
            throw new AiException(AiFailure.Network, "The connection to the AI service dropped. Nothing was changed.");
        }
    }

    /// <summary>Hard limit for "Test connection": a one-word answer never needs the 6-minute reply cap.</summary>
    public TimeSpan TestCap { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>One tiny call (a few tokens) used by Settings → AI "Test connection". Gives up after
    /// <see cref="TestCap"/>; the caller's token (the Stop button) cancels it sooner.</summary>
    public async Task TestAsync(CancellationToken ct = default)
    {
        using var cap = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cap.CancelAfter(TestCap);
        try { _ = await CompleteAsync("Reply with the single word: ok", cap.Token).ConfigureAwait(false); }
        catch (AiException ex) when (ex.Kind == AiFailure.Cancelled && !ct.IsCancellationRequested)
        {
            throw new AiException(AiFailure.Timeout, "No answer within 30 seconds. Check your internet connection and try again.");
        }
    }

    private static int? TokenCount(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    private static async Task<AiException> FailureFor(HttpResponseMessage resp, CancellationToken ct)
    {
        string? detail = null;
        try
        {
            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            detail = (string?)JsonNode.Parse(raw)?["error"]?["message"];
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException) { }
        var code = (int)resp.StatusCode;
        Log.Warn("AiClient", $"HTTP {code}");   // status only — never the body, which may echo request text
        return resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AiException(AiFailure.Auth,
                $"The API key was rejected ({code}). Check it in Settings → AI."),
            HttpStatusCode.TooManyRequests => new AiException(AiFailure.RateLimited,
                "Too many requests or no credit left on the API account (429). Try again in a minute, or check your balance."),
            HttpStatusCode.BadRequest => new AiException(AiFailure.BadResponse,
                "Claude refused the request (400)" + (detail is { Length: > 0 } ? ": " + detail : ".")),
            _ when code >= 500 => new AiException(AiFailure.Server,
                $"Claude is unavailable right now (HTTP {code}). Try again shortly."),
            _ => new AiException(AiFailure.BadResponse, $"Unexpected answer from the AI service (HTTP {code})."),
        };
    }
}

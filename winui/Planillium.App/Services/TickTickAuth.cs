using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Planillium.App.Services;

/// <summary>
/// Full TickTick OAuth2 flow — port of ticktick/sync.py's authorize/exchange/
/// refresh, so this app no longer depends on the Python app for a token.
/// Loopback callback runs on a raw TcpListener (no URL-ACL headaches),
/// localhost only, CSRF state checked, honest result pages.
/// </summary>
public static class TickTickAuth
{
    /// <summary>Shared with TickTickService.SharedClient — both used to hardcode this
    /// same value as an independent literal, low risk today since they happened to agree,
    /// but nothing stopped a future tuning change from updating only one (round-5 audit
    /// finding #32).</summary>
    internal const int HttpTimeoutSeconds = 15;

    private const int RedirectPort = 8765;
    // How long one accepted connection gets to send its request before it's abandoned —
    // separate from the overall 120s flow timeout (2026-07-23 audit finding #11).
    private static readonly TimeSpan PerConnectionReadTimeout = TimeSpan.FromSeconds(3);
    // Built from RedirectPort rather than typed out separately — the two used to be
    // independent literals, so changing one without noticing the other would make the
    // app listen on a different port than it told TickTick to answer on, hanging
    // "Connect TickTick" for the full 2-minute timeout with no clue why (round-5 audit
    // finding #15). static readonly, not const: C#'s constant string interpolation only
    // covers string-typed holes, not an int constant needing ToString().
    // internal (not private): TickTickConnectDialog's setup instructions display this
    // same value — it used to retype the URL as its own literal, the identical
    // two-copies-of-one-fact shape this comment already warns about, just one file over
    // (2026-07-18 audit finding R10-05).
    internal static readonly string RedirectUri = $"http://localhost:{RedirectPort}/callback";
    private const string AuthUrl = "https://ticktick.com/oauth/authorize";
    private const string TokenUrl = "https://ticktick.com/oauth/token";

    // The three Credential Manager entries a successful connect can write — shared by
    // Disconnect() below and (via SettingsPage) "Clear all my data," so both delete
    // paths agree on what "disconnected" actually means (2026-07-18 audit finding
    // R10-02: previously nothing in the app could remove these once written).
    internal static readonly string[] CredentialKeys =
        { "ticktick_client_secret", "ticktick_access_token", "ticktick_refresh_token" };

    // Reused across calls rather than a new HttpClient per request — the
    // earlier "share one HttpClient" fix (TickTickService.SharedClient)
    // only touched that file, missing this one (2026-07-09 audit finding
    // #20). Low-traffic (login/refresh only), but the same socket-churn
    // reasoning applies.
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };

    public static string ClientId => ConfigService.TickTickClientId;
    public static string? ClientSecret => CredentialStore.Read("ticktick_client_secret");
    public static bool IsConfigured =>
        ClientId.Length > 0 && !string.IsNullOrEmpty(ClientSecret);

    public static bool SaveClientSecret(string secret) =>
        CredentialStore.Write("ticktick_client_secret", secret);

    /// <summary>Undoes everything "Connect TickTick" wrote: deletes the stored client
    /// secret and tokens, and clears the saved client ID from config.json. Doesn't touch
    /// the ticktick_sync database table (the plan-task ↔ TickTick-task ID links) — that's
    /// a separate, database-side concern already covered by "Clear all my data" (2026-07-18
    /// audit finding R10-02).</summary>
    public static void Disconnect()
    {
        foreach (var key in CredentialKeys) CredentialStore.Delete(key);
        ConfigService.Mutate(cfg =>
        {
            if (cfg["ticktick"] is System.Text.Json.Nodes.JsonObject tt) tt.Remove("client_id");
        });
    }

    /// <summary>
    /// Runs the whole browser flow. Returns (ok, user-facing message).
    /// Times out after 120s — if TickTick rejects the request outright it
    /// renders its own error page and never redirects back, so the timeout
    /// is the only way that failure surfaces.
    /// </summary>
    public static async Task<(bool Ok, string Message)> AuthorizeAsync()
    {
        if (!IsConfigured)
            return (false, "Enter the client ID and client secret first.");

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        // PKCE (RFC 8252's recommended hardening for loopback-redirect
        // native-app OAuth flows like this one): a random secret generated
        // here and never sent until the token exchange, so a code
        // intercepted in between is useless without it (2026-07-09 audit
        // finding #9). codeVerifier must be 43-128 chars of the unreserved
        // set — base64url of 32 random bytes (43 chars, no padding) fits.
        var codeVerifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var codeChallenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        TcpListener listener;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, RedirectPort);
            listener.Start();
        }
        catch (SocketException ex)
        {
            return (false, Log.Friendly($"Can't listen on port {RedirectPort}", ex, "close the other app using it"));
        }

        try
        {
            var url = AuthUrl + "?" + string.Join("&",
                $"client_id={Uri.EscapeDataString(ClientId)}",
                "response_type=code",
                $"redirect_uri={Uri.EscapeDataString(RedirectUri)}",
                $"scope={Uri.EscapeDataString("tasks:read tasks:write")}",
                $"state={state}",
                $"code_challenge={codeChallenge}",
                "code_challenge_method=S256");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            while (true)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(timeout.Token); }
                catch (OperationCanceledException)
                {
                    return (false, "No response from TickTick after 2 minutes — if an error " +
                                   "page appeared in your browser, fix the app settings there " +
                                   "(redirect URL must be exactly " + RedirectUri + ") and retry.");
                }

                using (client)
                {
                    var stream = client.GetStream();
                    // Pulled out of this loop to flatten its nesting (audit finding #5) — any
                    // local process can connect to this loopback listener during the auth
                    // window and send garbage, so a malformed request is treated the same as
                    // a stray one (answer, keep waiting) rather than letting a parse exception
                    // escape the whole auth flow (2026-07-09 audit finding #23).
                    //
                    // A per-connection timeout, linked to (but much shorter than) the overall
                    // 120s flow timeout: another local process could otherwise connect and
                    // simply never send a byte, tying up the accept loop's only read for the
                    // rest of the whole auth window and starving the real browser callback
                    // (2026-07-23 audit finding #11). If the overall timeout fires first, this
                    // token cancels too and the loop unwinds the same way it already did.
                    using var perConnection = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                    perConnection.CancelAfter(PerConnectionReadTimeout);
                    var query = await TryParseCallbackRequest(stream, perConnection.Token);
                    if (query is null)
                    {
                        await Respond(stream, 400, "Bad request", "Waiting for TickTick…");
                        continue;
                    }

                    if (query["code"] is null && query["error"] is null)
                    {
                        // Stray request (favicon, probe) — answer, keep waiting.
                        await Respond(stream, 404, "Not the callback", "Waiting for TickTick…");
                        continue;
                    }
                    if (query["state"] != state)
                    {
                        await Respond(stream, 400, "Authorization failed",
                            "State mismatch — close this tab and retry from the app.");
                        return (false, "State mismatch — possible CSRF; please retry.");
                    }
                    if (query["error"] is { } err)
                    {
                        var detail = query["error_description"] ?? err;
                        await Respond(stream, 200, "Authorization failed",
                            $"TickTick said: {detail} — close this tab and retry from the app.");
                        return (false, $"TickTick error: {detail}");
                    }

                    var code = query["code"]!;
                    await Respond(stream, 200, "Authorized!", $"Return to {AppInfo.DisplayName}.");
                    return await ExchangeCodeAsync(code, codeVerifier);
                }
            }
        }
        finally { listener.Stop(); }
    }

    /// <summary>Reads one HTTP request off the loopback socket and parses its query
    /// string — pulled out of AuthorizeAsync's accept loop so that method reads as one
    /// flat sequence of steps instead of a raw-socket-parsing try/catch nested inside it
    /// (audit finding #5). Null means the read/parse itself failed; the caller treats
    /// that identically to a stray, non-callback request.</summary>
    private static async Task<System.Collections.Specialized.NameValueCollection?> TryParseCallbackRequest(
        NetworkStream stream, CancellationToken ct)
    {
        try
        {
            var buffer = new byte[8192];
            var read = await stream.ReadAsync(buffer, ct);
            var requestLine = Encoding.ASCII.GetString(buffer, 0, read)
                .Split('\r', '\n').FirstOrDefault() ?? "";
            var parts = requestLine.Split(' ');
            var path = parts.Length > 1 ? parts[1] : "";
            return HttpUtility.ParseQueryString(new Uri("http://x" + path).Query);
        }
        catch (Exception ex)
        {
            Log.Warn("TickTickAuth", $"malformed callback request: {ex.Message}");
            return null;
        }
    }

    private static async Task Respond(NetworkStream stream, int status, string heading, string detail)
    {
        var body = "<html><body style='font-family:sans-serif;padding:40px'>" +
                   $"<h2>{WebUtility.HtmlEncode(heading)}</h2>" +
                   $"<p>{WebUtility.HtmlEncode(detail)}</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n" +
                   "Content-Type: text/html; charset=utf-8\r\n" +
                   $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(bytes);
    }

    private static async Task<(bool, string)> ExchangeCodeAsync(string code, string codeVerifier)
    {
        var result = await TokenRequestAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = codeVerifier,
        });
        return result is null
            ? (false, "Token exchange failed — check the client ID/secret and retry.")
            : (true, "Connected — tasks will appear on the Today page.");
    }

    /// <summary>Pulls only the standard OAuth "error"/"error_description" fields out of a
    /// token-endpoint error body — never returns the raw text, so an unvetted response
    /// can't end up in the log file (see TokenRequestAsync's catch above).</summary>
    private static (string? Error, string? Description) TryParseOAuthError(string body)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            var err = root.TryGetProperty("error", out var e) ? e.GetString() : null;
            var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
            return (err, desc);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>RFC 4648 §5 base64url, no padding — PKCE's required encoding.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Refresh the access token in place; false if that's impossible.</summary>
    public static async Task<bool> RefreshAsync()
    {
        var refresh = CredentialStore.Read("ticktick_refresh_token");
        if (string.IsNullOrEmpty(refresh) || !IsConfigured) return false;
        return await TokenRequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
        }) is not null;
    }

    private static async Task<JsonDocument?> TokenRequestAsync(Dictionary<string, string> form)
    {
        try
        {
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(form),
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", creds) },
            };
            var resp = await SharedClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                // Log only the two fields TickTick documents for an error response, never
                // the raw body — TickTick's servers could echo back anything
                // account-identifying in an error payload, and there's no reason this app
                // needs more than the documented error/error_description fields to act on
                // a failed token exchange (round-5 audit finding #12; the "Log.cs has no
                // cap" reasoning this comment used to cite stopped being true once Log.cs
                // gained a 5MB cap + rotation the same round — narrowing what's logged is
                // still correct, just for its own sake now, not because the file is unbounded).
                var (err, desc) = TryParseOAuthError(body);
                Log.Warn("TickTickAuth", $"token endpoint {(int)resp.StatusCode}: {err ?? "(unparseable)"}" +
                    (desc is null ? "" : $" — {desc}"));
                return null;
            }
            var doc = JsonDocument.Parse(body);
            var access = doc.RootElement.GetProperty("access_token").GetString() ?? "";
            var newRefresh = doc.RootElement.TryGetProperty("refresh_token", out var r)
                ? r.GetString() ?? "" : "";
            if (access.Length == 0 || !CredentialStore.Write("ticktick_access_token", access))
                return null;
            if (newRefresh.Length > 0) CredentialStore.Write("ticktick_refresh_token", newRefresh);
            return doc;
        }
        catch (Exception ex)
        {
            Log.Error("TickTickAuth.TokenRequest", ex);
            return null;
        }
    }
}

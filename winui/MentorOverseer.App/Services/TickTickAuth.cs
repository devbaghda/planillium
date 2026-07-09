using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace MentorOverseer.App.Services;

/// <summary>
/// Full TickTick OAuth2 flow — port of ticktick/sync.py's authorize/exchange/
/// refresh, so this app no longer depends on the Python app for a token.
/// Loopback callback runs on a raw TcpListener (no URL-ACL headaches),
/// localhost only, CSRF state checked, honest result pages.
/// </summary>
public static class TickTickAuth
{
    private const int RedirectPort = 8765;
    private const string RedirectUri = $"http://localhost:8765/callback";
    private const string AuthUrl = "https://ticktick.com/oauth/authorize";
    private const string TokenUrl = "https://ticktick.com/oauth/token";

    // Reused across calls rather than a new HttpClient per request — the
    // earlier "share one HttpClient" fix (TickTickService.SharedClient)
    // only touched that file, missing this one (2026-07-09 audit finding
    // #20). Low-traffic (login/refresh only), but the same socket-churn
    // reasoning applies.
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string ClientId => ConfigService.TickTickClientId;
    public static string? ClientSecret => CredentialStore.Read("ticktick_client_secret");
    public static bool IsConfigured =>
        ClientId.Length > 0 && !string.IsNullOrEmpty(ClientSecret);

    public static bool SaveClientSecret(string secret) =>
        CredentialStore.Write("ticktick_client_secret", secret);

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
            return (false, $"Can't listen on port {RedirectPort} — close the other app using it. ({ex.Message})");
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
                    var buffer = new byte[8192];
                    var read = await stream.ReadAsync(buffer, timeout.Token);
                    var requestLine = Encoding.ASCII.GetString(buffer, 0, read)
                        .Split('\r', '\n').FirstOrDefault() ?? "";
                    var parts = requestLine.Split(' ');
                    var path = parts.Length > 1 ? parts[1] : "";
                    var query = HttpUtility.ParseQueryString(new Uri("http://x" + path).Query);

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
                    await Respond(stream, 200, "Authorized!", "Return to Mentor Overseer.");
                    return await ExchangeCodeAsync(code, codeVerifier);
                }
            }
        }
        finally { listener.Stop(); }
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
                Log.Warn("TickTickAuth", $"token endpoint {(int)resp.StatusCode}: {body[..Math.Min(body.Length, 200)]}");
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

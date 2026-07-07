using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MentorOverseer.App.Services;

/// <summary>
/// TickTick API client (pull + complete). Tokens live in Windows Credential
/// Manager via CredentialStore (shared with python-keyring's format). On a
/// 401 the access token is refreshed in place via TickTickAuth — this app is
/// fully self-sufficient; the Python app is no longer needed for auth.
/// </summary>
public sealed class TickTickService
{
    private const string ApiBase = "https://api.ticktick.com/open/v1";
    private const string MirrorProjectName = "Netherlands Plan";

    public record TtTask(string Id, string ProjectId, string ProjectName, string Title);

    public static string? AccessToken => CredentialStore.Read("ticktick_access_token");
    public static bool IsAuthorized => !string.IsNullOrEmpty(AccessToken);

    // ── API core (with one refresh-and-retry on 401) ─────────────────────

    private static async Task<HttpResponseMessage> SendAsync(
        Func<HttpClient, Task<HttpResponseMessage>> call)
    {
        var token = AccessToken ?? throw new InvalidOperationException("No TickTick token.");
        using (var client = Client(token))
        {
            var resp = await call(client);
            if (resp.StatusCode != HttpStatusCode.Unauthorized) return resp;
            resp.Dispose();
        }
        if (!await TickTickAuth.RefreshAsync())
            throw new InvalidOperationException(
                "TickTick token expired and refresh failed — reconnect in Settings.");
        using var retryClient = Client(AccessToken!);
        return await call(retryClient);
    }

    private static HttpClient Client(string token)
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>Local calendar date a task is due (TickTick sends UTC), or null.</summary>
    internal static DateOnly? TaskLocalDate(JsonElement task)
    {
        if (!task.TryGetProperty("dueDate", out var v) || v.GetString() is not { Length: > 0 } due)
            return null;
        return DateTimeOffset.TryParse(due, out var dto)
            ? DateOnly.FromDateTime(dto.ToLocalTime().Date)
            : null;
    }

    /// <summary>Open personal tasks due today, across all projects except the old mirror.</summary>
    public static async Task<List<TtTask>> TasksDueTodayAsync()
    {
        using var projResp = await SendAsync(c => c.GetAsync($"{ApiBase}/project"));
        projResp.EnsureSuccessStatusCode();
        using var projects = JsonDocument.Parse(await projResp.Content.ReadAsStringAsync());

        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<TtTask>();
        foreach (var p in projects.RootElement.EnumerateArray())
        {
            var pid = p.GetProperty("id").GetString() ?? "";
            var pname = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (pid.Length == 0 || pname == MirrorProjectName) continue;

            using var dataResp = await SendAsync(c => c.GetAsync($"{ApiBase}/project/{pid}/data"));
            if (!dataResp.IsSuccessStatusCode) continue;
            using var data = JsonDocument.Parse(await dataResp.Content.ReadAsStringAsync());
            if (!data.RootElement.TryGetProperty("tasks", out var tasks)) continue;

            foreach (var t in tasks.EnumerateArray())
            {
                var status = t.TryGetProperty("status", out var s) ? s.GetInt32() : 0;
                if (status != 0 || TaskLocalDate(t) != today) continue;
                result.Add(new TtTask(
                    t.GetProperty("id").GetString() ?? "",
                    pid, pname,
                    t.TryGetProperty("title", out var ti) ? ti.GetString() ?? "Untitled" : "Untitled"));
            }
        }
        return result;
    }

    public static async Task CompleteTaskAsync(string projectId, string taskId)
    {
        using var resp = await SendAsync(c => c.PostAsync(
            $"{ApiBase}/project/{projectId}/task/{taskId}/complete", null));
        resp.EnsureSuccessStatusCode();
    }
}

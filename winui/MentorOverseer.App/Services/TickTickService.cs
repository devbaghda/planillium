using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MentorOverseer.App.Services;

/// <summary>
/// Pull-only TickTick client. Reuses the OAuth access token the Python app
/// stored in Windows Credential Manager (python-keyring writes generic
/// credentials named "&lt;key&gt;@MentorOverseer") — no second OAuth flow.
/// If the token is missing/expired, the UI points at the Python app's
/// Connect button; the WinUI app gets its own flow in a later phase.
/// </summary>
public sealed class TickTickService
{
    private const string ApiBase = "https://api.ticktick.com/open/v1";
    private const string MirrorProjectName = "Netherlands Plan";

    public record TtTask(string Id, string ProjectId, string ProjectName, string Title);

    // ── Credential Manager (read-only) ──────────────────────────────────

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags; public uint Type; public string TargetName; public string Comment;
        public long LastWritten; public uint CredentialBlobSize; public IntPtr CredentialBlob;
        public uint Persist; public uint AttributeCount; public IntPtr Attributes;
        public string TargetAlias; public string UserName;
    }

    internal static string? ReadCredential(string key)
    {
        // python-keyring's WinVault backend stores TargetName = "user@service".
        foreach (var target in new[] { $"{key}@MentorOverseer", "MentorOverseer", key })
        {
            if (!CredRead(target, 1 /* CRED_TYPE_GENERIC */, 0, out var ptr)) continue;
            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                if (cred.CredentialBlobSize == 0) continue;
                var bytes = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
                // keyring writes UTF-16LE; tolerate UTF-8 too.
                var text = System.Text.Encoding.Unicode.GetString(bytes);
                if (text.Contains('�') || text.Any(char.IsControl))
                    text = System.Text.Encoding.UTF8.GetString(bytes);
                text = text.Trim('\0').Trim();
                if (text.Length > 0) return text;
            }
            finally { CredFree(ptr); }
        }
        return null;
    }

    public static string? AccessToken => ReadCredential("ticktick_access_token");
    public static bool IsAuthorized => !string.IsNullOrEmpty(AccessToken);

    // ── API ──────────────────────────────────────────────────────────────

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
        var token = AccessToken ?? throw new InvalidOperationException("No TickTick token.");
        using var client = Client(token);

        var projResp = await client.GetAsync($"{ApiBase}/project");
        projResp.EnsureSuccessStatusCode();
        using var projects = JsonDocument.Parse(await projResp.Content.ReadAsStringAsync());

        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<TtTask>();
        foreach (var p in projects.RootElement.EnumerateArray())
        {
            var pid = p.GetProperty("id").GetString() ?? "";
            var pname = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (pid.Length == 0 || pname == MirrorProjectName) continue;

            var dataResp = await client.GetAsync($"{ApiBase}/project/{pid}/data");
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
        var token = AccessToken ?? throw new InvalidOperationException("No TickTick token.");
        using var client = Client(token);
        var resp = await client.PostAsync(
            $"{ApiBase}/project/{projectId}/task/{taskId}/complete", null);
        resp.EnsureSuccessStatusCode();
    }
}

using System.Diagnostics;

namespace Planillium.App.Services;

/// <summary>
/// Mac version: secrets live in the macOS Keychain (login keychain), service "Planillium", via the
/// system `security` tool. The secret is sent on its stdin, never on the command line.
/// A scratch run (MENTOR_ROOT set) never touches the Keychain - same rule as Windows.
/// </summary>
public static class CredentialStore
{
    private const string Service = AppInfo.DisplayName;
    private static readonly Dictionary<string, string> ScratchSecrets = new();

    public static void SeedScratch(string key, string value)
    {
        if (!AppPaths.ScratchRequested || string.IsNullOrEmpty(value)) return;
        lock (ScratchSecrets) ScratchSecrets.TryAdd(key, value);
    }

    private static (int Code, string Out) Security(string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = stdin != null, UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        if (stdin != null) { p.StandardInput.Write(stdin); p.StandardInput.Close(); }
        var o = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, o);
    }

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static string? Read(string key)
    {
        if (AppPaths.ScratchRequested)
            lock (ScratchSecrets) return ScratchSecrets.TryGetValue(key, out var v) ? v : null;
        try
        {
            var (code, o) = Security(null, "find-generic-password", "-s", Service, "-a", key, "-w");
            return code == 0 ? o.TrimEnd('\n') : null;
        }
        catch (Exception ex) { Log.Error("CredentialStore.Read", ex); return null; }
    }

    public static bool Write(string key, string value)
    {
        if (AppPaths.ScratchRequested)
        {
            lock (ScratchSecrets) ScratchSecrets[key] = value;
            return true;
        }
        try
        {
            var cmd = $"add-generic-password -U -s {Quote(Service)} -a {Quote(key)} -w {Quote(value)}\n";
            return Security(cmd, "-i").Code == 0;
        }
        catch (Exception ex) { Log.Error("CredentialStore.Write", ex); return false; }
    }

    public static void Delete(string key)
    {
        if (AppPaths.ScratchRequested) { lock (ScratchSecrets) ScratchSecrets.Remove(key); return; }
        try { Security(null, "delete-generic-password", "-s", Service, "-a", key); }
        catch (Exception ex) { Log.Error("CredentialStore.Delete", ex); }
    }
}

using System.Runtime.InteropServices;

namespace Planillium.App.Services;

/// <summary>
/// Windows Credential Manager access, read AND write, byte-compatible with
/// python-keyring's WinVault backend (TargetName "&lt;key&gt;@ServiceName",
/// generic type, UTF-16LE blob) — so tokens written by either app are read
/// by both. Secrets never touch disk.
/// </summary>
public static class CredentialStore
{
    private const string Service = AppInfo.DisplayName;
    // Reads still check the pre-rename target so an existing TickTick token
    // (stored under "...@MentorOverseer" before this app was Planillium)
    // isn't orphaned — new writes only ever use the current Service name.
    private const string LegacyService = AppInfo.LegacyStartupRegistryValue;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW(ref CREDENTIAL credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags; public uint Type; public string TargetName; public string? Comment;
        public long LastWritten; public uint CredentialBlobSize; public IntPtr CredentialBlob;
        public uint Persist; public uint AttributeCount; public IntPtr Attributes;
        public string? TargetAlias; public string? UserName;
    }

    public static string? Read(string key)
    {
        // Only the two forms actually documented above: current name, and the
        // pre-rename name for a token an older build already wrote. Two more —
        // the bare service name and the bare key — used to be checked too with
        // no comment explaining why; since Windows credentials aren't scoped per
        // requesting app, that meant this app would happily read (and treat as
        // its own secret) any unrelated credential on the machine that happened
        // to be saved under one of those two generic names (round-5 audit
        // finding #23).
        foreach (var target in new[] { $"{key}@{Service}", $"{key}@{LegacyService}" })
        {
            if (!CredRead(target, 1 /* CRED_TYPE_GENERIC */, 0, out var ptr)) continue;
            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                if (cred.CredentialBlobSize == 0) continue;
                var bytes = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
                // keyring writes UTF-16LE (sometimes with a BOM); tolerate UTF-8 too.
                var text = System.Text.Encoding.Unicode.GetString(bytes);
                if (text.Contains('�') || text.Any(char.IsControl))
                    text = System.Text.Encoding.UTF8.GetString(bytes);
                text = text.Trim('\0').Trim('﻿').Trim();
                if (text.Length > 0) return text;
            }
            finally { CredFree(ptr); }
        }
        return null;
    }

    public static bool Write(string key, string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var blobBytes = System.Text.Encoding.Unicode.GetBytes(value);  // UTF-16LE, no BOM
        var blob = Marshal.AllocCoTaskMem(blobBytes.Length);
        try
        {
            Marshal.Copy(blobBytes, 0, blob, blobBytes.Length);
            // Defense-in-depth (2026-07-18 audit finding R11-14): blobBytes is only
            // needed for the Marshal.Copy just above — zero it now instead of leaving
            // the plaintext secret sitting in this array until the GC reclaims it.
            // Real-world exposure is low (this needs process-memory read access, which
            // already implies a worse compromise), but the cost of clearing is free.
            Array.Clear(blobBytes);
            var cred = new CREDENTIAL
            {
                Type = 1,                       // CRED_TYPE_GENERIC
                TargetName = $"{key}@{Service}",
                UserName = key,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)blobBytes.Length,
                Persist = 2,                    // CRED_PERSIST_LOCAL_MACHINE
            };
            if (!CredWriteW(ref cred, 0))
            {
                Log.Warn("CredentialStore.Write",
                    $"CredWriteW({key}) failed, error {Marshal.GetLastWin32Error()}");
                return false;
            }
            // Trust nothing: confirm the value actually round-trips before
            // any caller treats the secret as saved (lesson from audit #6).
            return Read(key) == value;
        }
        finally
        {
            // Same reasoning as the Array.Clear above — CredWriteW has already made its
            // own internal copy by this point, so there's no reason to leave the
            // plaintext secret in freed-but-unoverwritten unmanaged heap memory.
            var zero = new byte[blobBytes.Length];
            Marshal.Copy(zero, 0, blob, zero.Length);
            Marshal.FreeCoTaskMem(blob);
        }
    }

    private const int ErrorNotFound = 1168;

    /// <summary>Removes a stored credential (both the current-service and legacy-service
    /// target names, mirroring Read's dual-target lookup) — added so a "disconnect"/
    /// "clear my data" action can actually undo what Write saved (2026-07-18 audit
    /// finding R10-02: no delete path existed at all before this). CredDeleteW returning
    /// false with ERROR_NOT_FOUND just means that particular target name was never
    /// written — the common case for whichever of the two doesn't apply — not worth
    /// surfacing. A false return with any OTHER error previously left no trace at all: if
    /// deletion genuinely failed, the app (and user) would have no way to know the secret
    /// was still sitting in Credential Manager after "disconnecting" (2026-07-18 audit
    /// finding R11-13) — logged now, matching the pattern Write already uses on failure.</summary>
    public static void Delete(string key)
    {
        foreach (var target in new[] { $"{key}@{Service}", $"{key}@{LegacyService}" })
        {
            if (CredDeleteW(target, 1 /* CRED_TYPE_GENERIC */, 0)) continue;
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
                Log.Warn("CredentialStore.Delete", $"CredDeleteW({target}) failed, error {error}");
        }
    }
}

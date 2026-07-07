using System.Runtime.InteropServices;

namespace MentorOverseer.App.Services;

/// <summary>
/// Windows Credential Manager access, read AND write, byte-compatible with
/// python-keyring's WinVault backend (TargetName "&lt;key&gt;@MentorOverseer",
/// generic type, UTF-16LE blob) — so tokens written by either app are read
/// by both. Secrets never touch disk.
/// </summary>
public static class CredentialStore
{
    private const string Service = "MentorOverseer";

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW(ref CREDENTIAL credential, uint flags);
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
        foreach (var target in new[] { $"{key}@{Service}", Service, key })
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
        finally { Marshal.FreeCoTaskMem(blob); }
    }
}

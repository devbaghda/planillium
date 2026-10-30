namespace Planillium.App.Services;

/// <summary>
/// The Anthropic API key, kept in Windows Credential Manager through CredentialStore (the same
/// vault the TickTick token uses). Never written to config.json, state, the database, plans or
/// logs. Which credential name is used comes from <see cref="AiSettings.CredentialKey"/>.
/// In a scratch run (MENTOR_ROOT set) the key lives only in this process instead (G11): seeded once
/// from <see cref="AiSettings.ScratchKeyEnvironmentVariable"/> (Debug/test builds only), replaced by
/// Save, cleared by Remove, never written anywhere. CredentialStore itself refuses every name in a
/// scratch run, so nothing here can reach the real vault.
/// </summary>
public static class AiKey
{
    // One shared client for every AI call, as TickTick does. No overall timeout here: AiClient
    // applies its own limit (AiClient.DefaultTimeout) so that a user Stop and a timeout can be told apart.
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>The scratch run's in-memory key (null outside a scratch run or when none was given).</summary>
    private static string? _scratchKey = ScratchKeyFromEnvironment();

    /// <summary>True when the key is kept in memory for this run, not in Credential Manager.</summary>
    public static bool InMemory => AppPaths.IsScratchRun;

    public static bool Has() => !string.IsNullOrEmpty(Read());

    /// <summary>The saved key. In a scratch run: the in-memory key, and only while the local stub
    /// is set (<see cref="AiSettings.ScratchBlocked"/>), so a scratch key never goes to Anthropic.</summary>
    public static string? Read()
    {
        if (InMemory) return AiSettings.ScratchBlocked ? null : _scratchKey;
        return CredentialStore.Read(AiSettings.CredentialKey);
    }

    /// <summary>Trims surrounding whitespace/newlines, then stores. Returns false (and writes
    /// nothing) for an empty key, or when Credential Manager refuses the write.</summary>
    public static bool Save(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 0) return false;
        if (InMemory)
        {
            if (AiSettings.ScratchBlocked) return false;
            _scratchKey = trimmed;
            return true;
        }
        return CredentialStore.Write(AiSettings.CredentialKey, trimmed);
    }

    public static void Remove()
    {
        if (InMemory) { _scratchKey = null; return; }
        CredentialStore.Delete(AiSettings.CredentialKey);
    }

    /// <summary>A client wired to the saved key, the chosen model and the active base URL.</summary>
    public static AiClient CreateClient(string model) =>
        new(Http, AiSettings.BaseUrl(), model, Read, AiClient.DefaultTimeout,
            capCheck: AiSpend.RefusalOrNull, recordSpend: AiSpend.Record);   // G9a: every real AI call is capped

    /// <summary>The scratch key from the environment: Debug/test builds, scratch runs only.</summary>
    private static string? ScratchKeyFromEnvironment()
    {
#if DEBUG || PLANILLIUM_TESTS
        if (!AppPaths.IsScratchRun) return null;
        var value = Environment.GetEnvironmentVariable(AiSettings.ScratchKeyEnvironmentVariable)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
#else
        return null;
#endif
    }
}

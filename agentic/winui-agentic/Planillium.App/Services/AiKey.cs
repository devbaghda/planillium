namespace Planillium.App.Services;

/// <summary>
/// The Anthropic API key, kept in Windows Credential Manager through CredentialStore (the same
/// vault the TickTick token uses). Never written to config.json, state, the database, plans or
/// logs. Which credential name is used comes from <see cref="AiSettings.CredentialKey"/>.
/// </summary>
public static class AiKey
{
    // One shared client for every AI call, as TickTick does. No overall timeout here: AiClient
    // applies its own 120 s limit so that a user Stop and a timeout can be told apart.
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static bool Has() => !string.IsNullOrEmpty(Read());

    public static string? Read() => CredentialStore.Read(AiSettings.CredentialKey);

    /// <summary>Trims surrounding whitespace/newlines, then stores. Returns false (and writes
    /// nothing) for an empty key, or when Credential Manager refuses the write.</summary>
    public static bool Save(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 0) return false;
        return CredentialStore.Write(AiSettings.CredentialKey, trimmed);
    }

    public static void Remove() => CredentialStore.Delete(AiSettings.CredentialKey);

    /// <summary>A client wired to the saved key, the chosen model and the active base URL.</summary>
    public static AiClient CreateClient(string model) =>
        new(Http, AiSettings.BaseUrl(), model, Read, AiClient.DefaultTimeout);
}

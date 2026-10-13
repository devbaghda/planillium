using System.Text.Json;

namespace Planillium.App.Services;

/// <summary>
/// Where the AI layer's settings live (2026-10-13). The API key is a secret, so it goes in Windows
/// Credential Manager (<see cref="CredentialStore"/>) — never in config.json, the database, plan files,
/// logs, the repo or the installer. The model name and the first-use notice flag are not secret and sit
/// in config.json's "ai" block.
/// </summary>
public static class AiSettings
{
    private const string KeyName = "anthropic_api_key";

    public static bool HasKey => !string.IsNullOrEmpty(CredentialStore.Read(KeyName));

    /// <returns>false when Windows refused to store the key (it didn't round-trip).</returns>
    public static bool SaveKey(string key) => CredentialStore.Write(KeyName, key.Trim());

    public static void RemoveKey() => CredentialStore.Delete(KeyName);

    public static string Model =>
        Root("model") is { Length: > 0 } m ? m : AiClient.DefaultModel;

    public static void SaveModel(string model) =>
        ConfigService.Mutate(root =>
        {
            var ai = root["ai"] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
            ai["model"] = string.IsNullOrWhiteSpace(model) ? AiClient.DefaultModel : model.Trim();
            root["ai"] = ai;
        });

    /// <summary>True once the user accepted the one-time "this is what gets sent" notice.</summary>
    public static bool NoticeAccepted =>
        ConfigService.Root.TryGetProperty("ai", out var ai) &&
        ai.TryGetProperty("notice_accepted", out var v) && v.ValueKind == JsonValueKind.True;

    public static void AcceptNotice() =>
        ConfigService.Mutate(root =>
        {
            var ai = root["ai"] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
            ai["notice_accepted"] = true;
            root["ai"] = ai;
        });

    /// <summary>A ready client from the stored key and model, or an <see cref="AiException"/> (NoKey).</summary>
    public static AiClient CreateClient()
    {
        var key = CredentialStore.Read(KeyName);
        if (string.IsNullOrEmpty(key)) throw new AiException(AiFailure.NoKey, "No API key is set.");
        return new AiClient(key, Model);
    }

    private static string? Root(string name) =>
        ConfigService.Root.TryGetProperty("ai", out var ai) &&
        ai.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MentorOverseer.App.Services;

/// <summary>
/// WinUI-app-only state (kickoff/review markers, theme override) in
/// data/winui_state.json — a NEW file, so the Python app's files stay
/// untouched by anything that isn't part of the shared contract.
/// </summary>
public class AppState
{
    [JsonPropertyName("last_kickoff")] public string LastKickoff { get; set; } = "";
    [JsonPropertyName("last_review")] public string LastReview { get; set; } = "";
    [JsonPropertyName("theme")] public string Theme { get; set; } = "default"; // default|light|dark
}

public static class StateService
{
    private static string PathOf => System.IO.Path.Combine(AppPaths.Root, "data", "winui_state.json");
    private static AppState? _cached;

    public static AppState Load()
    {
        if (_cached != null) return _cached;
        try
        {
            _cached = JsonSerializer.Deserialize<AppState>(File.ReadAllText(PathOf));
        }
        catch { /* missing/corrupt → defaults */ }
        return _cached ??= new AppState();
    }

    public static void Save(AppState s)
    {
        _cached = s;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathOf)!);
        File.WriteAllText(PathOf, JsonSerializer.Serialize(s,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

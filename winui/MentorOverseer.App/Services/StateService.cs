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
    [JsonPropertyName("opacity")] public int Opacity { get; set; } = 100;      // window opacity, 40–100 %
    [JsonPropertyName("name_asked")] public bool NameAsked { get; set; } = false;
    [JsonPropertyName("window_width")] public int WindowWidth { get; set; } = 1180;
    [JsonPropertyName("window_height")] public int WindowHeight { get; set; } = 780;
    // How many toast notifications have fired since the window was last brought to the
    // foreground — backs the tray icon's unread dot (2026-07-20 request). Persisted (not
    // just in-memory) so a notification that fires while the app is closed, or right before
    // it's quit, still shows as unread the next time it's launched.
    [JsonPropertyName("unread_notifications")] public int UnreadNotifications { get; set; } = 0;
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
        catch (FileNotFoundException) { /* first run — defaults are correct */ }
        catch (Exception ex) { Log.Error("StateService.Load (falling back to defaults)", ex); }
        return _cached ??= new AppState();
    }

    public static void Save(AppState s)
    {
        _cached = s;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathOf)!);
        // Same write-to-temp-then-swap as every other saved file (JsonFileIO,
        // ConfigService) — this one was writing directly, the exact corruption
        // window that helper exists to close, and this file is saved at the
        // moment the app closes, i.e. the most likely time to also be killed
        // (audit finding #2).
        JsonFileIO.WriteAllTextAtomic(PathOf, JsonSerializer.Serialize(s,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

using Microsoft.UI.Xaml;

namespace MentorOverseer.App.Services;

/// <summary>
/// Makes `Application.Current.Resources["...Brush"]` — the pattern this
/// app's C# code-behind uses everywhere to build UI dynamically — agree
/// with the window's own theme override.
///
/// `{ThemeResource ...}` bindings in XAML correctly resolve per-element via
/// ActualTheme, so native control chrome and styled text always track
/// MainWindow's `root.RequestedTheme` correctly. But a bare
/// `Application.Current.Resources[key]` indexer call has no per-element
/// context to resolve a ThemeDictionaries-scoped key against, so the
/// framework falls back to `Application.RequestedTheme` — and that property
/// turns out to be unsettable at runtime for this app model (verified: it
/// throws a COMException even called before any window exists). Left alone,
/// C#-built brushes silently track whatever Windows itself is set to,
/// independent of the app's own Light/Dark choice — e.g. choosing "Light"
/// on a Dark-mode machine draws dark-theme (near-white) text on the
/// correctly-light background, unreadable rather than just low-contrast.
///
/// The fix: copy the CORRECT theme's resolved values for the small, fixed
/// set of keys this app actually uses dynamically, in as plain top-level
/// entries on Application.Current.Resources. A flat top-level entry always
/// wins over anything found via ThemeDictionaries/MergedDictionaries, and —
/// unlike Application.RequestedTheme — can be reassigned at any time, so
/// this also correctly handles a live theme change from Settings and (via
/// the ActualThemeChanged subscription in MainWindow) a live OS theme
/// change while "Follow Windows" is selected.
/// </summary>
public static class ThemeSync
{
    private static readonly string[] Keys =
    {
        "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "TextFillColorTertiaryBrush",
        "CardBackgroundFillColorDefaultBrush", "CardStrokeColorDefaultBrush",
        "DividerStrokeColorDefaultBrush", "SubtleFillColorSecondaryBrush",
        "SystemFillColorCriticalBrush", "SystemFillColorSuccessBrush",
        "SystemFillColorCautionBrush", "SystemFillColorCriticalBackgroundBrush",
        "AccentTextFillColorPrimaryBrush", "TextOnAccentFillColorPrimaryBrush",
        "AccentFillColorDefaultBrush",
    };

    public static void Apply(ElementTheme actualTheme)
    {
        var themeName = actualTheme == ElementTheme.Dark ? "Dark" : "Light";
        var app = Application.Current.Resources;
        try
        {
            foreach (var key in Keys)
                if (FindThemed(app, themeName, key) is { } value)
                    app[key] = value;
        }
        catch (Exception ex)
        {
            Log.Error("ThemeSync.Apply", ex);
        }
    }

    private static object? FindThemed(ResourceDictionary dict, string themeName, string key)
    {
        if (dict.ThemeDictionaries.TryGetValue(themeName, out var themedObj) &&
            themedObj is ResourceDictionary themed && themed.TryGetValue(key, out var value))
            return value;
        foreach (var merged in dict.MergedDictionaries)
            if (FindThemed(merged, themeName, key) is { } found)
                return found;
        return null;
    }
}

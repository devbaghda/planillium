using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

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
/// The fix: copy the CORRECT theme's resolved COLOR for the small, fixed set
/// of keys this app actually uses dynamically, as plain top-level entries on
/// Application.Current.Resources. A flat top-level entry always wins over
/// anything found via ThemeDictionaries/MergedDictionaries, and — unlike
/// Application.RequestedTheme — can be reassigned at any time, so this also
/// handles a live theme change from Settings and (via the ActualThemeChanged
/// subscription in MainWindow) a live OS theme change while "Follow Windows"
/// is selected.
///
/// Critically, on a LIVE switch we must not replace the dictionary entry with
/// a brand-new Brush object: every element already on screen captured its
/// Foreground/Background by direct object reference when it was built
/// (`(Brush)Application.Current.Resources[key]` is a one-time read, not a
/// live binding), so swapping in a different Brush instance only affects
/// elements built *after* the swap — the currently-visible page stays on the
/// old colors until something forces it to rebuild, which is what made this
/// look like it "needed a restart" to take effect. Mutating each brush's
/// .Color in place, and only ever handing out that same cached instance,
/// makes every existing reference repaint immediately.
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

    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public static void Apply(ElementTheme actualTheme)
    {
        var themeName = actualTheme == ElementTheme.Dark ? "Dark" : "Light";
        Log.Info($"ThemeSync.Apply actualTheme={actualTheme} -> {themeName}");
        var app = Application.Current.Resources;
        try
        {
            foreach (var key in Keys)
            {
                if (FindThemed(app, themeName, key) is not SolidColorBrush themed) continue;
                if (Cache.TryGetValue(key, out var brush))
                    brush.Color = themed.Color;
                else
                    Cache[key] = brush = new SolidColorBrush(themed.Color);
                app[key] = brush;
            }
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

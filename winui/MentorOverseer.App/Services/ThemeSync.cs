using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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
/// turns out to be unsettable at runtime for this app model. Left alone,
/// C#-built brushes silently track whatever Windows itself is set to,
/// independent of the app's own Light/Dark choice.
///
/// The first fix attempt walked Application.Current.Resources'
/// ThemeDictionaries/MergedDictionaries by hand to find each theme's real
/// value. That turned out to be unreliable: logging every lookup showed it
/// returning null for every single key, on every single run, whether called
/// at MainWindow construction or deferred to root.Loaded — the WinUI 3
/// control-resources dictionary just isn't reliably walkable that way from
/// application code. Whatever looked "fixed" before was residual state left
/// over from whichever earlier call happened to catch resources in a state
/// where the ambient Application.RequestedTheme (OS theme, unchangeable)
/// coincidentally matched what we wanted — not this code actually working.
///
/// The real fix: stop trying to discover these values at runtime. The
/// Fluent color tokens for a fixed, small set of brushes are stable,
/// versioned constants — copied here directly from this project's pinned
/// Microsoft.WindowsAppSDK 1.5.240627000 package
/// (lib/uap10.0/Microsoft.UI/Themes/generic.xaml, Light block ~line 7487,
/// Dark block ~line 1966). Two keys (AccentFillColorDefaultBrush,
/// AccentTextFillColorPrimaryBrush) derive from the user's OS accent color
/// instead of a fixed constant; those alias SystemAccentColorLight2/3 or
/// SystemAccentColorDark1/2, which — unlike the ThemeDictionaries-scoped
/// brushes above — ARE flat, non-theme-scoped resources the ambient indexer
/// resolves correctly on its own.
///
/// Every value is applied by mutating one cached, reused SolidColorBrush's
/// .Color per key rather than replacing the dictionary entry with a new
/// Brush object: every element already on screen captured its
/// Foreground/Background by direct object reference when it was built
/// (`(Brush)Application.Current.Resources[key]` is a one-time read, not a
/// live binding), so swapping in a different Brush instance only affects
/// elements built *after* the swap. Mutating the same instance in place
/// makes every existing reference repaint immediately, on a live theme
/// change from Settings or (via the ActualThemeChanged subscription in
/// MainWindow) a live OS theme change while "Follow Windows" is selected.
/// </summary>
public static class ThemeSync
{
    // Light-theme override for TextFillColorTertiaryBrush: the WinUI stock
    // value (#72000000, ~45% opacity) is ~3.35:1 contrast on white, below
    // WCAG AA's 4.5:1 for the small captions/labels this app uses it for.
    // Bumped to ~56% opacity for ~4.9:1.
    private static readonly Dictionary<string, (Color Light, Color Dark)> Fixed = new()
    {
        ["TextFillColorPrimaryBrush"] = (Color.FromArgb(0xE4, 0, 0, 0), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
        ["TextFillColorSecondaryBrush"] = (Color.FromArgb(0x9E, 0, 0, 0), Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF)),
        ["TextFillColorTertiaryBrush"] = (Color.FromArgb(0x8F, 0, 0, 0), Color.FromArgb(0x87, 0xFF, 0xFF, 0xFF)),
        ["CardBackgroundFillColorDefaultBrush"] = (Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
        ["CardStrokeColorDefaultBrush"] = (Color.FromArgb(0x0F, 0, 0, 0), Color.FromArgb(0x19, 0, 0, 0)),
        ["DividerStrokeColorDefaultBrush"] = (Color.FromArgb(0x0F, 0, 0, 0), Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
        ["SubtleFillColorSecondaryBrush"] = (Color.FromArgb(0x09, 0, 0, 0), Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
        ["SystemFillColorCriticalBrush"] = (Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C), Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4)),
        ["SystemFillColorSuccessBrush"] = (Color.FromArgb(0xFF, 0x0F, 0x7B, 0x0F), Color.FromArgb(0xFF, 0x6C, 0xCB, 0x5F)),
        ["SystemFillColorCautionBrush"] = (Color.FromArgb(0xFF, 0x9D, 0x5D, 0x00), Color.FromArgb(0xFF, 0xFC, 0xE1, 0x00)),
        ["SystemFillColorCriticalBackgroundBrush"] = (Color.FromArgb(0xFF, 0xFD, 0xE7, 0xE9), Color.FromArgb(0xFF, 0x44, 0x27, 0x26)),
        ["TextOnAccentFillColorPrimaryBrush"] = (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0, 0, 0)),
    };

    // These two derive from the user's OS accent color, not a fixed
    // constant, so they alias the (non-theme-scoped) accent tint resources
    // instead of a hardcoded value.
    private static readonly Dictionary<string, (string Light, string Dark)> AccentAliases = new()
    {
        ["AccentFillColorDefaultBrush"] = ("SystemAccentColorDark1", "SystemAccentColorLight2"),
        ["AccentTextFillColorPrimaryBrush"] = ("SystemAccentColorDark2", "SystemAccentColorLight3"),
    };

    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public static void Apply(ElementTheme actualTheme)
    {
        var dark = actualTheme == ElementTheme.Dark;
        var app = Application.Current.Resources;
        try
        {
            foreach (var (key, (light, darkColor)) in Fixed)
                SetBrush(app, key, dark ? darkColor : light);

            foreach (var (key, (lightAlias, darkAlias)) in AccentAliases)
            {
                var alias = dark ? darkAlias : lightAlias;
                var resolved = app[alias];
                var color = resolved switch
                {
                    Color c => c,
                    SolidColorBrush b => b.Color,
                    _ => (Color?)null,
                };
                if (color is { } accent)
                    SetBrush(app, key, accent);
                else
                    Log.Info($"ThemeSync.Apply {key}: alias {alias} resolved to {resolved?.GetType().FullName ?? "null"}, skipped");
            }
        }
        catch (Exception ex)
        {
            Log.Error("ThemeSync.Apply", ex);
        }
    }

    private static void SetBrush(ResourceDictionary app, string key, Color color)
    {
        if (Cache.TryGetValue(key, out var brush))
            brush.Color = color;
        else
            Cache[key] = brush = new SolidColorBrush(color);
        app[key] = brush;
    }
}

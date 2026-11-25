using Avalonia.Media;

namespace Planillium.App.Services;

/// <summary>Mac version: the app's named brushes, one cached mutable SolidColorBrush per key, recoloured in place
/// when the theme changes so every element already on screen repaints (same mechanism as Windows).
/// Values copied from the Windows table (light cards, stronger strokes).</summary>
public static class ThemeSync
{
    private static readonly Dictionary<string, (Color Light, Color Dark)> Fixed = new()
    {
        ["TextFillColorPrimaryBrush"] = (Color.FromArgb(0xE4, 0, 0, 0), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
        ["TextFillColorSecondaryBrush"] = (Color.FromArgb(0x9E, 0, 0, 0), Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF)),
        ["TextFillColorTertiaryBrush"] = (Color.FromArgb(0x8F, 0, 0, 0), Color.FromArgb(0x87, 0xFF, 0xFF, 0xFF)),
        ["CardBackgroundFillColorDefaultBrush"] = (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
        ["CardBackgroundFillColorSecondaryBrush"] = (Color.FromArgb(0xFF, 0xEE, 0xF2, 0xF7), Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
        ["CardStrokeColorDefaultBrush"] = (Color.FromArgb(0x29, 0, 0, 0), Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
        ["DividerStrokeColorDefaultBrush"] = (Color.FromArgb(0x1F, 0, 0, 0), Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
        ["SubtleFillColorSecondaryBrush"] = (Color.FromArgb(0x12, 0, 0, 0), Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
        ["ControlFillColorSecondaryBrush"] = (Color.FromArgb(0x80, 0xF9, 0xF9, 0xF9), Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
        ["SystemFillColorCriticalBrush"] = (Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C), Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4)),
        ["SystemFillColorSuccessBrush"] = (Color.FromArgb(0xFF, 0x0F, 0x7B, 0x0F), Color.FromArgb(0xFF, 0x6C, 0xCB, 0x5F)),
        ["SystemFillColorCautionBrush"] = (Color.FromArgb(0xFF, 0x9D, 0x5D, 0x00), Color.FromArgb(0xFF, 0xFC, 0xE1, 0x00)),
        ["SystemFillColorCriticalBackgroundBrush"] = (Color.FromArgb(0xFF, 0xFD, 0xE7, 0xE9), Color.FromArgb(0xFF, 0x44, 0x27, 0x26)),
        ["SystemFillColorCautionBackgroundBrush"] = (Color.FromArgb(0xFF, 0xFF, 0xF4, 0xCE), Color.FromArgb(0xFF, 0x43, 0x35, 0x19)),
        ["PaidWorkBrush"] = (Color.FromArgb(0xFF, 0x1F, 0x7A, 0x3A), Color.FromArgb(0xFF, 0xA3, 0xE6, 0xA1)),
        ["TextOnAccentFillColorPrimaryBrush"] = (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0, 0, 0)),
        ["AccentFillColorDefaultBrush"] = (Color.FromArgb(0xFF, 0x00, 0x67, 0xC0), Color.FromArgb(0xFF, 0x60, 0xCD, 0xFF)),
        ["AccentTextFillColorPrimaryBrush"] = (Color.FromArgb(0xFF, 0x00, 0x3E, 0x92), Color.FromArgb(0xFF, 0x99, 0xEB, 0xFF)),
        ["AppGroundBrush"] = (Color.FromArgb(0xFF, 0xE9, 0xED, 0xF3), Color.FromArgb(0xFF, 0x20, 0x20, 0x20)),
    };

    private static readonly Dictionary<string, SolidColorBrush> Cache = new();
    private static bool _dark = true;
    public static bool IsDark => _dark;

    /// <summary>The cached brush for a key (created on first use in the current theme).</summary>
    public static SolidColorBrush Brush(string key)
    {
        if (Cache.TryGetValue(key, out var b)) return b;
        var (l, d) = Fixed.TryGetValue(key, out var v) ? v : Fixed["TextFillColorSecondaryBrush"];
        return Cache[key] = new SolidColorBrush(_dark ? d : l);
    }

    public static void Apply(bool dark)
    {
        _dark = dark;
        foreach (var (key, (l, d)) in Fixed)
            Brush(key).Color = dark ? d : l;
    }
}

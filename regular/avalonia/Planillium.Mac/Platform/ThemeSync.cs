using Avalonia.Media;

namespace Planillium.App.Services;

/// <summary>Mac version: the app's named brushes, one cached mutable SolidColorBrush per key, recoloured in place
/// when the theme changes so every element already on screen repaints (same mechanism as Windows).
/// Two colour themes, Mint and Muted blue, each with a light and a dark set. Values were computed (OKLCH, measured
/// contrast: text 12:1, secondary text and links 4.5:1, button text 7:1) in the approved themes artifact.</summary>
public static class ThemeSync
{
    public const string Mint = "mint", MutedBlue = "blue";

    private static readonly Dictionary<string, (Color Light, Color Dark)> MintSet = new()
    {
        ["TextFillColorPrimaryBrush"] = (Color.FromArgb(0xFF,0x0D,0x19,0x14), Color.FromArgb(0xFF,0xEF,0xF7,0xF3)),
        ["TextFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0x52,0x5A,0x56), Color.FromArgb(0xFF,0xAF,0xBA,0xB5)),
        ["TextFillColorTertiaryBrush"] = (Color.FromArgb(0xFF,0x61,0x6C,0x67), Color.FromArgb(0xFF,0x98,0xA0,0x9C)),
        ["CardBackgroundFillColorDefaultBrush"] = (Color.FromArgb(0xFF,0xF3,0xFD,0xF8), Color.FromArgb(0xFF,0x13,0x1C,0x18)),
        ["CardBackgroundFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0xD5,0xDF,0xDA), Color.FromArgb(0xFF,0x20,0x29,0x25)),
        ["CardStrokeColorDefaultBrush"] = (Color.FromArgb(0xFF,0xC5,0xCF,0xCA), Color.FromArgb(0xFF,0x3F,0x48,0x44)),
        ["DividerStrokeColorDefaultBrush"] = (Color.FromArgb(0xFF,0xCF,0xD9,0xD4), Color.FromArgb(0xFF,0x25,0x2D,0x29)),
        ["SubtleFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0xCE,0xD8,0xD3), Color.FromArgb(0xFF,0x17,0x1D,0x1A)),
        ["ControlFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0xF3,0xFD,0xF8), Color.FromArgb(0xFF,0x20,0x29,0x25)),
        ["SystemFillColorCriticalBrush"] = (Color.FromArgb(0xFF,0xA8,0x37,0x2E), Color.FromArgb(0xFF,0xFE,0x8B,0x7D)),
        ["SystemFillColorSuccessBrush"] = (Color.FromArgb(0xFF,0x05,0x6F,0x4F), Color.FromArgb(0xFF,0x73,0xE4,0xB6)),
        ["SystemFillColorCautionBrush"] = (Color.FromArgb(0xFF,0x7B,0x5B,0x02), Color.FromArgb(0xFF,0xEE,0xC4,0x69)),
        ["SystemFillColorCriticalBackgroundBrush"] = (Color.FromArgb(0xFF,0xE9,0xE1,0xDC), Color.FromArgb(0xFF,0x34,0x2C,0x26)),
        ["SystemFillColorCautionBackgroundBrush"] = (Color.FromArgb(0xFF,0xF9,0xEA,0xC6), Color.FromArgb(0xFF,0x31,0x25,0x04)),
        ["PaidWorkBrush"] = (Color.FromArgb(0xFF,0x05,0x6F,0x4F), Color.FromArgb(0xFF,0x73,0xE4,0xB6)),
        ["TextOnAccentFillColorPrimaryBrush"] = (Color.FromArgb(0xFF,0x0D,0x19,0x14), Color.FromArgb(0xFF,0x09,0x14,0x0F)),
        ["AccentFillColorDefaultBrush"] = (Color.FromArgb(0xFF,0x8D,0xDF,0xBB), Color.FromArgb(0xFF,0x8D,0xDF,0xBB)),
        ["AccentTextFillColorPrimaryBrush"] = (Color.FromArgb(0xFF,0x05,0x6F,0x4F), Color.FromArgb(0xFF,0x73,0xE4,0xB6)),
        ["AppGroundBrush"] = (Color.FromArgb(0xFF,0xE9,0xF3,0xEE), Color.FromArgb(0xFF,0x09,0x11,0x0E)),
    };

    private static readonly Dictionary<string, (Color Light, Color Dark)> BlueSet = new()
    {
        ["TextFillColorPrimaryBrush"] = (Color.FromArgb(0xFF,0x0D,0x18,0x1E), Color.FromArgb(0xFF,0xEF,0xF6,0xFB)),
        ["TextFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0x53,0x59,0x5D), Color.FromArgb(0xFF,0xB0,0xB9,0xBE)),
        ["TextFillColorTertiaryBrush"] = (Color.FromArgb(0xFF,0x61,0x6A,0x70), Color.FromArgb(0xFF,0x98,0x9F,0xA3)),
        ["CardBackgroundFillColorDefaultBrush"] = (Color.FromArgb(0xFF,0xF6,0xFB,0xFE), Color.FromArgb(0xFF,0x14,0x1A,0x1E)),
        ["CardBackgroundFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0xD6,0xDE,0xE2), Color.FromArgb(0xFF,0x21,0x27,0x2C)),
        ["CardStrokeColorDefaultBrush"] = (Color.FromArgb(0xFF,0xC7,0xCE,0xD1), Color.FromArgb(0xFF,0x40,0x46,0x4A)),
        ["DividerStrokeColorDefaultBrush"] = (Color.FromArgb(0xFF,0xCF,0xD7,0xDC), Color.FromArgb(0xFF,0x25,0x2C,0x30)),
        ["SubtleFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0xCF,0xD6,0xDC), Color.FromArgb(0xFF,0x17,0x1C,0x1F)),
        ["ControlFillColorSecondaryBrush"] = (Color.FromArgb(0xFF,0xF6,0xFB,0xFE), Color.FromArgb(0xFF,0x21,0x27,0x2C)),
        ["SystemFillColorCriticalBrush"] = (Color.FromArgb(0xFF,0xA8,0x37,0x2E), Color.FromArgb(0xFF,0xFE,0x8B,0x7D)),
        ["SystemFillColorSuccessBrush"] = (Color.FromArgb(0xFF,0x07,0x70,0x45), Color.FromArgb(0xFF,0x83,0xDA,0xA9)),
        ["SystemFillColorCautionBrush"] = (Color.FromArgb(0xFF,0x7B,0x5B,0x02), Color.FromArgb(0xFF,0xEE,0xC4,0x69)),
        ["SystemFillColorCriticalBackgroundBrush"] = (Color.FromArgb(0xFF,0xEB,0xE0,0xE1), Color.FromArgb(0xFF,0x35,0x2A,0x2B)),
        ["SystemFillColorCautionBackgroundBrush"] = (Color.FromArgb(0xFF,0xF9,0xEA,0xC6), Color.FromArgb(0xFF,0x31,0x25,0x04)),
        ["PaidWorkBrush"] = (Color.FromArgb(0xFF,0x07,0x70,0x45), Color.FromArgb(0xFF,0x83,0xDA,0xA9)),
        ["TextOnAccentFillColorPrimaryBrush"] = (Color.FromArgb(0xFF,0x0D,0x18,0x1E), Color.FromArgb(0xFF,0x09,0x13,0x19)),
        ["AccentFillColorDefaultBrush"] = (Color.FromArgb(0xFF,0xA5,0xD2,0xED), Color.FromArgb(0xFF,0xA5,0xD2,0xED)),
        ["AccentTextFillColorPrimaryBrush"] = (Color.FromArgb(0xFF,0x2D,0x67,0x86), Color.FromArgb(0xFF,0x98,0xD3,0xF6)),
        ["AppGroundBrush"] = (Color.FromArgb(0xFF,0xEA,0xF1,0xF6), Color.FromArgb(0xFF,0x0A,0x10,0x14)),
    };

    private static Dictionary<string, (Color Light, Color Dark)> Fixed = MintSet;
    private static string _palette = Mint;
    public static string Palette => _palette;

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

    private static string PalettePath => Path.Combine(AppPaths.Root, "data", "mac_colour_theme.txt");

    /// <summary>The saved colour theme (Mac-only file, so the shared state file stays as on Windows).</summary>
    public static string LoadPalette()
    {
        try { return File.ReadAllText(PalettePath).Trim() == MutedBlue ? MutedBlue : Mint; }
        catch { return Mint; }
    }

    public static void SavePalette(string palette)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PalettePath)!);
            File.WriteAllText(PalettePath, palette == MutedBlue ? MutedBlue : Mint);
        }
        catch { /* a failed save only means the choice is not remembered */ }
    }

    public static void Apply(bool dark) => Apply(dark, _palette);

    public static void Apply(bool dark, string? palette)
    {
        _dark = dark;
        _palette = palette == MutedBlue ? MutedBlue : Mint;
        Fixed = _palette == MutedBlue ? BlueSet : MintSet;
        foreach (var (key, (l, d)) in Fixed)
            Brush(key).Color = dark ? d : l;
        ApplyFluentAccent(dark);
    }

    // Controls Fluent draws itself (checkbox, radio, slider, accent buttons) take the theme's accent
    // instead of the system blue. Control-edge accents were measured at 3:1 against the surfaces.
    private static void ApplyFluentAccent(bool dark)
    {
        if (Avalonia.Application.Current?.Resources is not { } res) return;
        var (l, d, on) = _palette == MutedBlue
            ? (Color.Parse("#598DAC"), Color.Parse("#4E83A3"), Color.Parse("#FFFFFF"))
            : (Color.Parse("#2F9B75"), Color.Parse("#2B8F6C"), Color.Parse("#FFFFFF"));
        var c = dark ? d : l;
        res["SystemAccentColor"] = c;
        res["SystemAccentColorLight1"] = c; res["SystemAccentColorLight2"] = c; res["SystemAccentColorLight3"] = c;
        res["SystemAccentColorDark1"] = c; res["SystemAccentColorDark2"] = c; res["SystemAccentColorDark3"] = c;
        res["AccentButtonForeground"] = new SolidColorBrush(dark ? on : Colors.White);
        res["AccentButtonForegroundPointerOver"] = res["AccentButtonForeground"];
        res["AccentButtonForegroundPressed"] = res["AccentButtonForeground"];
    }
}

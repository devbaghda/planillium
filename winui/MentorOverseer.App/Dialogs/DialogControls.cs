using Microsoft.UI.Xaml.Controls;

namespace MentorOverseer.App.Dialogs;

/// <summary>Small shared control factories for the multi-row split editors
/// (IdleReturnDialog, SplitDiaryEntryDialog).</summary>
internal static class DialogControls
{
    /// <summary>A duration-in-minutes NumberBox that's wide enough to actually
    /// read while typing, with no spin-button overlay (Compact placement floats
    /// its arrows above the box and, in a tightly-packed multi-row list, that
    /// overlapped the row above it — hard to notice until you're mid-type and
    /// can't see what you typed). NumberFormatter is pinned to en-US so typed
    /// digits parse the same way regardless of the OS's current culture: on a
    /// non-English locale, NumberBox's default formatter can interpret keystrokes
    /// using that locale's grouping/decimal separators instead of plain digits,
    /// which is exactly the kind of culture-dependent-parsing bug this app has
    /// already hit once with date formatting.</summary>
    public static NumberBox MinutesBox(double? value)
    {
        var box = new NumberBox
        {
            Value = value ?? double.NaN,
            Minimum = 1,
            Width = 130,
            PlaceholderText = "min",
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            NumberFormatter = new Windows.Globalization.NumberFormatting.DecimalFormatter(
                new[] { "en-US" }, "US") { FractionDigits = 0 },
        };
        // No visible Header (would repeat on every row in a tightly-packed
        // multi-row list), but still needs a name for screen readers —
        // PlaceholderText alone isn't exposed as one (2026-07-09 audit
        // finding #15).
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, "Minutes");
        return box;
    }
}

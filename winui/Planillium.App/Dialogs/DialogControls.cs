using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Planillium.App.Dialogs;

/// <summary>Small shared control factories for the multi-row split editors
/// (IdleReturnDialog, SplitDiaryEntryDialog).</summary>
internal static class DialogControls
{
    /// <summary>Shared ContentDialog shell — every dialog in this app used to build its own
    /// `new ContentDialog { ... }` by hand, ~26 near-identical copies across 19 files
    /// (2026-07-23 audit finding #6). Not a correctness fix (none of those were wrong), but a
    /// future app-wide dialog tweak (a consistent min-width, a shared close affordance) now
    /// only needs to change this one method instead of every call site. Optional parameters
    /// default to whichever value the majority of existing dialogs already used, so most call
    /// sites only need to pass the handful of things that actually vary for them.</summary>
    public static ContentDialog Build(
        XamlRoot xamlRoot,
        string title,
        object content,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.Close,
        bool isPrimaryButtonEnabled = true,
        Style? secondaryButtonStyle = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            XamlRoot = xamlRoot,
            DefaultButton = defaultButton,
            IsPrimaryButtonEnabled = isPrimaryButtonEnabled,
        };
        if (primaryButtonText is not null) dialog.PrimaryButtonText = primaryButtonText;
        if (secondaryButtonText is not null) dialog.SecondaryButtonText = secondaryButtonText;
        if (closeButtonText is not null) dialog.CloseButtonText = closeButtonText;
        if (secondaryButtonStyle is not null) dialog.SecondaryButtonStyle = secondaryButtonStyle;
        return dialog;
    }

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
                new[] { "en-US" }, "US")
            { FractionDigits = 0 },
        };
        // No visible Header (would repeat on every row in a tightly-packed
        // multi-row list), but still needs a name for screen readers —
        // PlaceholderText alone isn't exposed as one (2026-07-09 audit
        // finding #15).
        AutomationProperties.SetName(box, "Minutes");
        return box;
    }
}
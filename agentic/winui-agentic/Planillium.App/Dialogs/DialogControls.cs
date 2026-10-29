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
        // G8: a dialog whose content holds a text box gets the blank-click focus protection (Controls/FocusShield.cs):
        // releases claimed on its own scrollers and the template's ContentScrollViewer, and a blank press parks the cursor
        // on an invisible park. No focus guard: the dialog's opening focus is unchanged. String content and box-less
        // dialogs are left exactly as before.
        Grid? shield = null;
        Button? park = null;
        if (content is UIElement element)
        {
            var scrollers = new List<ScrollViewer>();
            if (Controls.FocusShield.HasTextEntry(element, scrollers))
            {
                park = Controls.FocusShield.CreatePark();
                shield = new Grid();
                shield.Children.Add(park);
                shield.Children.Add(element);
                foreach (var sv in scrollers) Controls.FocusShield.ClaimReleases(sv);
                content = shield;
            }
        }
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
        if (shield is not null && park is not null)
        {
            var host = shield;
            dialog.Loaded += (_, _) =>
            {
                if (Controls.FocusShield.NearestScroller(host) is { } templateScroller)
                    Controls.FocusShield.ClaimReleases(templateScroller);
            };
            Controls.FocusShield.Attach(dialog, park, new Controls.FocusShield.AttachOptions { Guard = false });
        }
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

    /// <summary>Wires an AutoSuggestBox to filter and offer <paramref name="frequent"/> as
    /// the user types, and to open the suggestion list immediately on focus if the box is
    /// still empty — the "show my most commonly-used past entries" behavior added to
    /// EditDiaryEntryDialog and SplitDiaryEntryDialog's description fields (2026-07-28
    /// request), previously hand-typed identically in both instead of shared once here.</summary>
    public static void WireFrequentSuggestions(AutoSuggestBox box, List<string> frequent)
    {
        box.ItemsSource = frequent;
        box.TextChanged += (sender, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            sender.ItemsSource = sender.Text.Length == 0
                ? frequent
                : frequent.Where(d => d.Contains(sender.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        };
        box.GotFocus += (_, _) =>
        {
            if (box.Text.Length == 0 && frequent.Count > 0) box.IsSuggestionListOpen = true;
        };
    }
}
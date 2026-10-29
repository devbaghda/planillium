using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;
using Windows.System;

namespace Planillium.App.Controls;

/// <summary>G8 (2026-10-29): the three layers of the Settings -> Income focus fix (B1f/B1d) in reusable form, for every
/// other scroller that holds a text box (Settings' other sections, Today, Schedule, Reports, and every dialog with a box).
/// Settings -> Income keeps its own code; the helper is attached beside it and skips IncomePanel.
///
/// 1. <see cref="ClaimReleases"/>: a ScrollViewer that is not a tab stop answers an unclaimed left-button release by
///    focusing its first focusable child (B1f root cause). Marking every release handled at the scroller's template root
///    stops that step for the whole scroller area: padding, scrollbar strip and the space below short content included.
/// 2. <see cref="Attach"/> press park: a press on blank space while a text box in the scope has the cursor moves focus to
///    an invisible 1x1 park button, so the cursor is in no box.
/// 3. Focus guard (pages only): a box in the scope takes focus only from the user's own mouse/touch/pen/keyboard, a Tab,
///    a restore of the element that last had focus there (window switch), or <see cref="RequestFocus"/>. Any other move
///    (WinUI's fallback after a rebuild or a collapsed box) is cancelled and focus parked.
/// Decisions use GettingFocus (synchronous) and the settled focused element after a dispatcher turn, never
/// GotFocus/LostFocus (delivered late).</summary>
public static class FocusShield
{
    public sealed class AttachOptions
    {
        /// <summary>Boxes inside any of these are left alone (Settings passes IncomePanel, which has its own rules).</summary>
        public List<DependencyObject> Skip { get; } = new();

        /// <summary>Takes focus if the park refuses it.</summary>
        public Control? Fallback { get; init; }

        /// <summary>Also install the focus guard (pages yes, dialogs no).</summary>
        public bool Guard { get; init; }
    }

    private sealed class State
    {
        public required FrameworkElement Scope { get; init; }
        public required Button Park { get; init; }
        public required AttachOptions Options { get; init; }
        /// <summary>The element in the scope that last received focus (weak: a cached page releasing its tree must not be
        /// held by this).</summary>
        public WeakReference<DependencyObject>? Last { get; set; }
        public long LastTabAt { get; set; } = long.MinValue / 2;
        public bool Logged { get; set; }
    }

    /// <summary>The box a caller is focusing through <see cref="RequestFocus"/>; set only for the duration of that
    /// synchronous Focus call (GettingFocus is raised inside it).</summary>
    private static DependencyObject? _requested;

    private static readonly ConditionalWeakTable<ScrollViewer, object> Claimed = new();
    private static readonly object Marker = new();

    // ---- layer 1: release claim ------------------------------------------------------------------------------------

    /// <summary>Marks every PointerReleased that reaches <paramref name="sv"/>'s template root handled, so the scroller
    /// never runs its focus-first-child step. Controls inside still claim their own releases first, so clicks, toggles
    /// and caret placement are unchanged (same as SettingsPage.SectionContent_PointerReleased). Idempotent. If the
    /// template is not there yet, attaches on the scroller's first Loaded.</summary>
    public static void ClaimReleases(ScrollViewer sv)
    {
        if (Claimed.TryGetValue(sv, out _)) return;
        Claimed.Add(sv, Marker);
        if (!TryClaimRoot(sv)) sv.Loaded += Scroller_Loaded;
    }

    private static void Scroller_Loaded(object sender, RoutedEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        sv.Loaded -= Scroller_Loaded;
        if (!TryClaimRoot(sv)) Log.Warn("FocusShield.ClaimReleases", "scroller has no template root after Loaded; releases not claimed");
    }

    private static bool TryClaimRoot(ScrollViewer sv)
    {
        if (VisualTreeHelper.GetChildrenCount(sv) == 0 || VisualTreeHelper.GetChild(sv, 0) is not UIElement root) return false;
        root.PointerReleased += ClaimRelease;
        return true;
    }

    private static void ClaimRelease(object sender, PointerRoutedEventArgs e) => e.Handled = true;

    // ---- park --------------------------------------------------------------------------------------------------------

    /// <summary>An invisible 1x1 button that holds focus so no box does (same attributes as Settings' FocusPark). It is a
    /// tab stop only while it holds focus (a Button that is not a tab stop refuses programmatic focus), never scrolls the
    /// page when focused, and is hidden from Narrator.</summary>
    public static Button CreatePark()
    {
        var park = new Button
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            IsTabStop = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        AutomationProperties.SetAccessibilityView(park, AccessibilityView.Raw);
        PreparePark(park);
        return park;
    }

    /// <summary>Wires a park declared in XAML (Today/Schedule/Reports FocusPark): drops its tab stop when it loses focus
    /// and never lets focusing it scroll the page. Settings' own FocusPark is wired by SettingsPage and not passed here.</summary>
    public static void PreparePark(Button park)
    {
        park.LostFocus += (_, _) => park.IsTabStop = false;
        park.BringIntoViewRequested += (_, e) => e.Handled = true;
    }

    private static void ParkNow(State st, string caller)
    {
        var park = st.Park;
        if (park.XamlRoot == null) return;
        if (ReferenceEquals(FocusManager.GetFocusedElement(park.XamlRoot), park)) return;
        park.IsTabStop = true;
        if (park.Focus(FocusState.Programmatic)) return;
        park.IsTabStop = false;
        if (st.Options.Fallback?.Focus(FocusState.Programmatic) == true) return;
        if (st.Logged) return;
        st.Logged = true;
        Log.Warn(caller, "focus park (and fallback) refused focus; focus stays where it was");
    }

    // ---- layer 2 (+3): attach ----------------------------------------------------------------------------------------

    /// <summary>Press park for <paramref name="scope"/>, plus the focus guard when <see cref="AttachOptions.Guard"/>.
    /// The press handler sees presses other controls already handled (handledEventsToo).</summary>
    public static void Attach(FrameworkElement scope, Button park, AttachOptions options)
    {
        var st = new State { Scope = scope, Park = park, Options = options };
        scope.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) => OnPressed(st, e)), handledEventsToo: true);
        if (!options.Guard) return;
        scope.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key == VirtualKey.Tab) st.LastTabAt = Environment.TickCount64;
        }), handledEventsToo: true);
        scope.GettingFocus += (_, args) => OnGettingFocus(st, args);
        // The park may sit outside the scope (Settings): a park arrival means no box is the one to restore.
        park.GettingFocus += (_, _) => st.Last = null;
    }

    private static void OnPressed(State st, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        // Presses inside a popup (suggestion list, NumberBox popup, flyout) are left alone.
        if (!ReachesScopeWithoutPopup(source, st.Scope)) return;
        // A press on a control that takes focus (button, check box, another box) lets that control take it.
        if (TakesFocus(source, st.Scope)) return;
        st.Scope.DispatcherQueue.TryEnqueue(() =>
        {
            if (FocusedBox(st) is not { } focused) return;
            // The press was on the focused box itself (or its NumberBox/AutoSuggestBox/ComboBox: spin and query buttons).
            if (IsWithin(source, Owner(focused, st.Scope))) return;
            st.Scope.DispatcherQueue.TryEnqueue(() =>
            {
                if (FocusedBox(st) == null) return;
                ParkNow(st, "FocusShield.BlankPress");
            });
        });
    }

    // ---- layer 3: guard ----------------------------------------------------------------------------------------------

    /// <summary>Lets <paramref name="box"/> take focus programmatically past a page's focus guard (a user-requested edit
    /// box, e.g. TaskNoteView's "Edit note").</summary>
    public static bool RequestFocus(Control box)
    {
        _requested = box;
        try { return box.Focus(FocusState.Programmatic); }
        finally { _requested = null; }
    }

    private static void OnGettingFocus(State st, GettingFocusEventArgs args)
    {
        var target = args.NewFocusedElement;
        if (target == null || !IsWithin(target, st.Scope)) return;
        if (IsTextEntry(target) && !IsSkipped(st, target))
        {
            var allowed = args.InputDevice is FocusInputDeviceKind.Mouse or FocusInputDeviceKind.Touch
                              or FocusInputDeviceKind.Pen or FocusInputDeviceKind.Keyboard
                          || Environment.TickCount64 - st.LastTabAt < 600
                          || (st.Last != null && st.Last.TryGetTarget(out var last) && ReferenceEquals(last, target))
                          || (_requested != null && IsWithin(target, _requested));
            if (!allowed)
            {
                var cancelled = args.TryCancel();
                Log.Info($"FocusShield guard: blocked a {args.InputDevice} focus move into a box " +
                         $"({(target as FrameworkElement)?.Name ?? target.GetType().Name}) on {st.Scope.GetType().Name}; cancelled={cancelled}");
                st.Scope.DispatcherQueue.TryEnqueue(() =>
                {
                    if (st.Scope.XamlRoot == null) return;
                    var now = FocusManager.GetFocusedElement(st.Scope.XamlRoot) as DependencyObject;
                    if (now == null || FocusedBox(st) != null) ParkNow(st, "FocusShield.Guard");
                });
                return;
            }
        }
        st.Last = new WeakReference<DependencyObject>(target);
    }

    // ---- dialogs -----------------------------------------------------------------------------------------------------

    /// <summary>Logical walk of dialog content (Panel children, Border child, ContentControl content incl. ScrollViewer,
    /// ItemsControl items that are elements). True if a text box is found; every ScrollViewer met is added to
    /// <paramref name="scrollers"/>. Box templates are not descended into.</summary>
    public static bool HasTextEntry(object? node, List<ScrollViewer> scrollers)
    {
        switch (node)
        {
            case TextBox or PasswordBox or RichEditBox or NumberBox or AutoSuggestBox:
                return true;
            case ScrollViewer sv:
                scrollers.Add(sv);
                return HasTextEntry(sv.Content, scrollers);
            case Panel p:
            {
                var found = false;
                foreach (var child in p.Children) found |= HasTextEntry(child, scrollers);
                return found;
            }
            case Border b:
                return HasTextEntry(b.Child, scrollers);
            case ItemsControl ic:
            {
                var found = false;
                foreach (var item in ic.Items) if (item is UIElement) found |= HasTextEntry(item, scrollers);
                return found;
            }
            case ContentControl cc:
                return HasTextEntry(cc.Content, scrollers);
            case UserControl uc:
                return HasTextEntry(uc.Content, scrollers);
            default:
                return false;
        }
    }

    /// <summary>The nearest ScrollViewer above <paramref name="element"/> in the visual tree (a dialog's template
    /// ContentScrollViewer), or null.</summary>
    public static ScrollViewer? NearestScroller(DependencyObject element)
    {
        for (var d = VisualTreeHelper.GetParent(element); d != null; d = VisualTreeHelper.GetParent(d))
            if (d is ScrollViewer sv) return sv;
        return null;
    }

    // ---- helpers -----------------------------------------------------------------------------------------------------

    private static bool IsTextEntry(DependencyObject d) => d is TextBox or PasswordBox or RichEditBox;

    private static bool IsSkipped(State st, DependencyObject d)
    {
        foreach (var s in st.Options.Skip) if (IsWithin(d, s)) return true;
        return false;
    }

    /// <summary>The focused text box in the scope (not in a skipped area), or null.</summary>
    private static DependencyObject? FocusedBox(State st)
    {
        if (st.Scope.XamlRoot == null) return null;
        if (FocusManager.GetFocusedElement(st.Scope.XamlRoot) is not DependencyObject f) return null;
        if (!IsTextEntry(f) || !IsWithin(f, st.Scope) || IsSkipped(st, f)) return null;
        return f;
    }

    /// <summary>The control that owns a focused text box: its NumberBox / AutoSuggestBox / ComboBox ancestor, else the
    /// box itself.</summary>
    private static DependencyObject Owner(DependencyObject box, DependencyObject scope)
    {
        for (var d = VisualTreeHelper.GetParent(box); d != null && !ReferenceEquals(d, scope); d = VisualTreeHelper.GetParent(d))
            if (d is NumberBox or AutoSuggestBox or ComboBox) return d;
        return box;
    }

    private static bool ReachesScopeWithoutPopup(DependencyObject source, DependencyObject scope)
    {
        for (var d = source; d != null; d = VisualTreeHelper.GetParent(d))
        {
            if (ReferenceEquals(d, scope)) return true;
            if (d is Popup) return false;
        }
        return false;
    }

    /// <summary>True when the nearest control around the press (scrollers excluded: their chrome is blank space) is an
    /// enabled tab stop, i.e. it will take focus itself.</summary>
    private static bool TakesFocus(DependencyObject source, DependencyObject scope)
    {
        for (var d = source; d != null && !ReferenceEquals(d, scope); d = VisualTreeHelper.GetParent(d))
        {
            if (d is not Control c || c is ScrollViewer or ScrollBar) continue;
            return c.IsTabStop && c.IsEnabled;
        }
        return false;
    }

    private static bool IsWithin(DependencyObject? element, DependencyObject ancestor)
    {
        for (var d = element; d != null; d = VisualTreeHelper.GetParent(d))
            if (ReferenceEquals(d, ancestor)) return true;
        return false;
    }
}

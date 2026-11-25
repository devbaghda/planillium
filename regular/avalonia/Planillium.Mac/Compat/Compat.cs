global using System.Collections.Generic;
global using Avalonia;
global using Avalonia.Controls;
global using Avalonia.Layout;
global using Avalonia.Media;
global using Avalonia.Input;
global using Avalonia.Interactivity;
global using Avalonia.VisualTree;
global using Avalonia.Controls.Primitives;
global using Planillium.App.Compat;
global using XamlRoot = Avalonia.Controls.TopLevel;
global using UIElement = Avalonia.Controls.Control;
global using FrameworkElement = Avalonia.Controls.Control;
global using Brush = Avalonia.Media.IBrush;
global using TextBox = Planillium.App.Compat.HTextBox;
global using ComboBox = Planillium.App.Compat.HComboBox;
global using CalendarDatePicker = Planillium.App.Compat.HDatePicker;
global using TextBlock = Planillium.App.Compat.HTextBlock;
global using PasswordBox = Planillium.App.Compat.PasswordBox;
global using Avalonia.Automation;
global using Avalonia.Controls.Documents;
global using FontStyle = Avalonia.Media.FontStyle;
global using Page = Planillium.App.Compat.PageBase;
global using DispatcherTimer = Avalonia.Threading.DispatcherTimer;
global using AutoSuggestBox = Planillium.App.Compat.HAutoSuggestBox;
global using DependencyObject = Avalonia.AvaloniaObject;
global using Shape = Avalonia.Controls.Shapes.Shape;
global using Planillium.App.Services;
global using MenuFlyoutItem = Planillium.App.Compat.MenuFlyoutItem;
global using Slider = Planillium.App.Compat.Slider;
global using ToggleSwitch = Planillium.App.Compat.ToggleSwitch;
global using ScrollViewer = Planillium.App.Compat.HScrollViewer;
using Avalonia.Threading;

namespace Planillium.App.Compat;

public static class FontWeights
{
    public static FontWeight Normal => FontWeight.Normal;
    public static FontWeight SemiBold => FontWeight.SemiBold;
    public static FontWeight Bold => FontWeight.Bold;
}

/// <summary>Brush lookup + the handful of text styles the Windows code took from Application resources.</summary>
public static class Ui
{
    public static IBrush Brush(string key) => Services.ThemeSync.Brush(key);
    public static IBrush Muted => Brush("TextFillColorTertiaryBrush");
    public static IBrush Secondary => Brush("TextFillColorSecondaryBrush");

    public static TextBlock Title(string text) => new() { Text = text, FontSize = 28, FontWeight = FontWeight.SemiBold };
    public static void Subtitle(TextBlock t) { t.FontSize = 20; t.FontWeight = FontWeight.SemiBold; }
    public static void Caption(TextBlock t) { t.FontSize = 12; }
    public static void BodyStrong(TextBlock t) { t.FontWeight = FontWeight.SemiBold; }

    public static AvaloniaObject? Parent(AvaloniaObject? d) => d switch
    {
        Visual v => v.GetVisualParent() ?? (v as Control)?.Parent,
        _ => null,
    };
    public static AvaloniaObject? Focused(TopLevel? t) => t?.FocusManager?.GetFocusedElement() as AvaloniaObject;
    public static void CopyText(string text) { var c = TopLevel.GetTopLevel(MainWindow.Instance)?.Clipboard; if (c != null) _ = c.SetTextAsync(text); }
    public static void RunUi(Action a) => Dispatcher.UIThread.Post(a);

    public static Border Card(Control child, double radius = 8) => new()
    {
        Background = Brush("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Brush("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Child = child,
    };

    public static void Danger(Button b) { b.Background = Brush("SystemFillColorCriticalBrush"); b.Foreground = Brushes.White; }
    public static void Preview(Control c, EventHandler<KeyEventArgs> h) => c.AddHandler(InputElement.KeyDownEvent, h, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    public static void SetTip(Control c, string? tip) => ToolTip.SetTip(c, tip);
    public static void Name(Control c, string name) => Avalonia.Automation.AutomationProperties.SetName(c, name);
}

/// <summary>Page shell: cached instance, OnNavigatedTo on every visit, owner window for dialogs.</summary>
public class PageBase : UserControl
{
    public XamlRoot XamlRoot => TopLevel.GetTopLevel(this) ?? MainWindow.Instance;
    protected virtual void OnNavigatedTo(NavigationEventArgs e) { }
    protected virtual void OnNavigatedFrom(NavigationEventArgs e) { }
    internal void NavigatedFrom() => OnNavigatedFrom(new NavigationEventArgs());
    protected DispatcherQueueShim DispatcherQueue { get; } = new();
    internal void NavigatedTo() => OnNavigatedTo(new NavigationEventArgs());
}

public enum InfoBarSeverity { Informational, Success, Warning, Error }

/// <summary>Minimal stand-in for WinUI's InfoBar: coloured strip with optional title, message, action button, close.</summary>
public sealed class InfoBar : Border
{
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, IsVisible = false };
    private readonly TextBlock _msg = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _close = new() { Content = "✕", Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Top, IsVisible = false };
    private readonly ContentControl _action = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
    private bool _open;

    public InfoBar()
    {
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(14, 10);
        BorderThickness = new Thickness(1);
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(_title);
        texts.Children.Add(_msg);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        g.Children.Add(texts);
        Grid.SetColumn(_action, 1); g.Children.Add(_action);
        Grid.SetColumn(_close, 2); g.Children.Add(_close);
        Child = g;
        _close.Click += (_, _) => { IsOpen = false; CloseButtonClick?.Invoke(this, null); };
        IsVisible = false;
        Severity = InfoBarSeverity.Informational;
    }

    public event Action<InfoBar, object?>? CloseButtonClick;
    public bool IsOpen { get => _open; set { _open = value; IsVisible = value; } }
    public bool IsClosable { get => _close.IsVisible; set => _close.IsVisible = value; }
    public string Title { get => _title.Text ?? ""; set { _title.Text = value; _title.IsVisible = !string.IsNullOrEmpty(value); } }
    public string Message { get => _msg.Text ?? ""; set => _msg.Text = value; }
    public Control? ActionButton { get => (Control?)_action.Content; set => _action.Content = value; }
    private InfoBarSeverity _sev;
    public InfoBarSeverity Severity
    {
        get => _sev;
        set
        {
            _sev = value;
            (Background, BorderBrush) = value switch
            {
                InfoBarSeverity.Error => (Services.ThemeSync.Brush("SystemFillColorCriticalBackgroundBrush"), Services.ThemeSync.Brush("SystemFillColorCriticalBrush")),
                InfoBarSeverity.Warning => (Services.ThemeSync.Brush("SystemFillColorCautionBackgroundBrush"), Services.ThemeSync.Brush("SystemFillColorCautionBrush")),
                InfoBarSeverity.Success => (Services.ThemeSync.Brush("CardBackgroundFillColorSecondaryBrush"), Services.ThemeSync.Brush("SystemFillColorSuccessBrush")),
                _ => (Services.ThemeSync.Brush("CardBackgroundFillColorSecondaryBrush"), Services.ThemeSync.Brush("CardStrokeColorDefaultBrush")),
            };
        }
    }
}

public enum ContentDialogResult { None, Primary, Secondary }
public enum ContentDialogButton { None, Primary, Secondary, Close }

public sealed class Deferral { public void Complete() { } }
public sealed class ContentDialogButtonClickEventArgs : EventArgs { public bool Cancel { get; set; } public Deferral GetDeferral() => new(); }
public sealed class ContentDialogClosingEventArgs : EventArgs { public bool Cancel { get; set; } public ContentDialogResult Result { get; init; } }

/// <summary>Stand-in for WinUI's ContentDialog: a modal window with title, content and up to three buttons.</summary>
public sealed class ContentDialog
{
    public object? Title { get; set; }
    public object? Content { get; set; }
    public string? PrimaryButtonText { get; set; }
    public string? SecondaryButtonText { get; set; }
    public string? CloseButtonText { get; set; }
    public ContentDialogButton DefaultButton { get; set; } = ContentDialogButton.Close;
    public bool IsPrimaryButtonEnabled { get => _primaryEnabled; set { _primaryEnabled = value; if (_primary != null) _primary.IsEnabled = value; } }
    public bool IsSecondaryButtonEnabled { get; set; } = true;
    public XamlRoot? XamlRoot { get; set; }
    public bool DangerSecondary { get; set; }
    public Dictionary<string, object> Resources { get; } = new();
    public Style? SecondaryButtonStyle { get => DangerSecondary ? new Style() : null; set => DangerSecondary = value != null; }
    public double MinContentWidth { get; set; } = 420;
    public event Action<ContentDialog, ContentDialogButtonClickEventArgs>? PrimaryButtonClick;
    public event Action<ContentDialog, ContentDialogButtonClickEventArgs>? SecondaryButtonClick;
    public event Action<ContentDialog, ContentDialogClosingEventArgs>? Closing;

    private bool _primaryEnabled = true;
    private Button? _primary;
    private Window? _win;
    private ContentDialogResult _result = ContentDialogResult.None;

    public void Hide() { _win?.Close(); }

    public Task<ContentDialogResult> ShowAsync()
    {
        var tcs = new TaskCompletionSource<ContentDialogResult>();
        var win = _win = new Window
        {
            Title = Title as string ?? "",
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = MinContentWidth, MaxWidth = 860, MaxHeight = 820,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            // Without this the dialog window shows Fluent's own near-black, which the Windows app never had.
            Background = Services.ThemeSync.Brush("AppGroundBrush"),
            RequestedThemeVariant = Application.Current?.RequestedThemeVariant,
        };
        var root = new DockPanel { Margin = new Thickness(24, 20) };
        if (Title is string ts && ts.Length > 0)
        {
            var t = new TextBlock { Text = ts, FontSize = 20, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(t, Dock.Top); root.Children.Add(t);
        }
        else if (Title is Control tc) { DockPanel.SetDock(tc, Dock.Top); root.Children.Add(tc); }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        Button Make(string text, bool accent)
        {
            var b = new Button { Content = text, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (accent) b.Classes.Add("accent");
            return b;
        }
        Button? close = null, secondary = null;
        if (CloseButtonText is { Length: > 0 } c) { close = Make(c, DefaultButton == ContentDialogButton.Close); }
        if (SecondaryButtonText is { Length: > 0 } s) { secondary = Make(s, DefaultButton == ContentDialogButton.Secondary); secondary.IsEnabled = IsSecondaryButtonEnabled; }
        if (PrimaryButtonText is { Length: > 0 } p) { _primary = Make(p, DefaultButton == ContentDialogButton.Primary); _primary.IsEnabled = _primaryEnabled; }
        foreach (var b in new[] { _primary, secondary, close }) if (b != null) buttons.Children.Add(b);

        void Finish(ContentDialogResult r)
        {
            var args = new ContentDialogClosingEventArgs { Result = r };
            Closing?.Invoke(this, args);
            if (args.Cancel) return;
            _result = r; win.Close();
        }
        if (_primary != null) _primary.Click += (_, _) =>
        {
            var a = new ContentDialogButtonClickEventArgs(); PrimaryButtonClick?.Invoke(this, a);
            if (!a.Cancel) Finish(ContentDialogResult.Primary);
        };
        if (secondary != null) secondary.Click += (_, _) =>
        {
            var a = new ContentDialogButtonClickEventArgs(); SecondaryButtonClick?.Invoke(this, a);
            if (!a.Cancel) Finish(ContentDialogResult.Secondary);
        };
        if (close != null) close.Click += (_, _) => Finish(ContentDialogResult.None);
        if (buttons.Children.Count > 0) root.Children.Add(buttons);

        var body = Content as Control ?? new TextBlock { Text = Content?.ToString() ?? "", TextWrapping = TextWrapping.Wrap };
        root.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, MaxHeight = 640 });
        win.Content = root;
        win.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (close != null) Finish(ContentDialogResult.None); else win.Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && e.Source is not TextBox { AcceptsReturn: true })
            {
                var def = DefaultButton switch { ContentDialogButton.Primary => _primary, ContentDialogButton.Secondary => secondary, _ => close };
                if (def is { IsEnabled: true }) { def.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
            }
        };
        win.Closed += (_, _) => { _win = null; tcs.TrySetResult(_result); };
        var owner = XamlRoot as Window ?? MainWindow.Instance;
        if (owner != null && owner.IsVisible) _ = win.ShowDialog(owner); else win.Show();
        win.Activate();
        return tcs.Task;
    }
}

public sealed class DispatcherQueueShim
{
    public bool TryEnqueue(Action a) { Dispatcher.UIThread.Post(a); return true; }
    public bool TryEnqueue(DispatcherQueuePriority _, Action a) { Dispatcher.UIThread.Post(a); return true; }
}
public enum DispatcherQueuePriority { Low, Normal, High }
public enum FocusState { Unfocused, Programmatic, Keyboard, Pointer }
public static class ControlExt
{
    public static bool Focus(this Control c, FocusState _) => c.Focus();
}

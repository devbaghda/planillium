using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Pages;

// Small shared visual helpers used across the other ReportsPage.*.cs files —
// see ReportsPage.xaml.cs for the file split.
public sealed partial class ReportsPage
{
    // Mirrors the same great/bad/warning split as ReportExport.cs's exported
    // HTML (raw hex there, since that file has to render correctly outside
    // this app/theme entirely) — if this threshold or color intent is ever
    // retuned, update both (round-4 audit finding).
    private static Brush ScoreBrush(int score) =>
        (Brush)Application.Current.Resources[
            score >= ScoreService.GreatDayThreshold ? "SystemFillColorSuccessBrush"
            : score < 0 ? "SystemFillColorCriticalBrush"
            : "SystemFillColorCautionBrush"];

    /// <summary>Thin alias kept so every call site in this file doesn't need renaming —
    /// see CategoryStyle.BrushKey (Services/CategoryStyle.cs) for the actual single
    /// source of truth, now shared with MainWindow.Tracker's tray pill too.</summary>
    private static string CategoryBrushKey(string category) => CategoryStyle.BrushKey(category);

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 60,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        Margin = new Thickness(2, 22, 0, 8),
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 50,
        Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
    };

    private static TextBlock Dim(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private static Border Card(UIElement child) => new()
    {
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18, 14, 18, 14),
        Child = child,
    };
}

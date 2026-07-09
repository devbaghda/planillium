using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MentorOverseer.App.Models;

namespace MentorOverseer.App.Views;

/// <summary>
/// Shared task-row presentation logic for Today and Schedule, so the same
/// task state (done / overdue / normal) always reads the same color on both
/// pages. Before this, each page independently decided the title color —
/// same intent, different code shapes (an if/else-if chain vs. a ternary
/// chain) — which is exactly the kind of duplication that quietly drifts
/// out of sync over time (2026-07-09 audit finding #6).
/// </summary>
public static class TaskRowStyle
{
    public static Brush TitleForeground(AssignedTask t) =>
        (Brush)Application.Current.Resources[
            t.Completed ? "TextFillColorTertiaryBrush"
            : t.Overdue ? "SystemFillColorCriticalBrush"
            : "TextFillColorPrimaryBrush"];
}

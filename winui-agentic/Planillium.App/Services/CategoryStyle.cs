namespace Planillium.App.Services;

/// <summary>
/// Single source of truth for an activity category's color — the Reports page's Diary
/// list and Time-by-App legend/bars used to each pick their own colors independently and
/// had drifted out of agreement (Paid and Neutral meant opposite colors between the two
/// views on the very same page — round-5 audit finding #9). The tray status pill
/// (MainWindow.Tracker.UpdatePill) kept its own third, independent copy even after that
/// fix — round-7 audit finding #3, "the fix applied to one function but a structural
/// sibling was left inconsistent" pattern this project keeps re-learning. All three now
/// read from this one table.
///
/// "dayoff" only ever comes from the tray pill (time_diary rows have no such category) —
/// deliberately mapped to a different gray than the neutral/default fallback so the two
/// aren't pixel-identical in the pill (the tray's original reasoning for keeping them
/// visually distinct; ReportsPage never encounters "dayoff" so this branch is inert there).
/// </summary>
public static class CategoryStyle
{
    public static string BrushKey(string category) => category switch
    {
        DiaryCategory.OnPlan => "SystemFillColorSuccessBrush",
        DiaryCategory.OffPlan => "SystemFillColorCriticalBrush",
        DiaryCategory.Idle => "SystemFillColorCautionBrush",
        DiaryCategory.Paid => "AccentTextFillColorPrimaryBrush",
        DiaryCategory.DayOff => "TextFillColorTertiaryBrush",
        _ => "TextFillColorSecondaryBrush", // neutral
    };
}

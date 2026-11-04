namespace Planillium.App.Services;

public static class AppInfo
{
    public const string DisplayName = "Planillium";
    public const string SingleInstanceMutex = "PlanilliumWinUI_SingleInstance";
    public const string StartupRegistryValue = "Planillium";
    public const string LegacyStartupRegistryValue = "MentorOverseer";

    /// <summary>Single source of truth for the "how many plans can be active at once"
    /// rule — was previously hand-typed as the literal 2 in five separate spots
    /// (AddPlanDialog.cs and PlansPage.xaml.cs), risking drift if the limit ever changes.
    /// Raised 2 → 3 (2026-08-13 request) — every consumer already reads this constant
    /// dynamically (dialog title/tooltip text, the >= gate) and every plan-list layout is a
    /// plain vertical StackPanel/foreach, not a fixed 2-column grid or hardcoded index, so
    /// this was confirmed to be the only line that needed to change in code.</summary>
    public const int MaxActivePlans = 3;
}

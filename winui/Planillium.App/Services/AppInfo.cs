namespace Planillium.App.Services;

public static class AppInfo
{
    public const string DisplayName = "Planillium";
    public const string SingleInstanceMutex = "PlanilliumWinUI_SingleInstance";
    public const string StartupRegistryValue = "Planillium";
    public const string LegacyStartupRegistryValue = "MentorOverseer";

    /// <summary>Single source of truth for the "how many plans can be active at once"
    /// rule — was previously hand-typed as the literal 2 in five separate spots
    /// (AddPlanDialog.cs and PlansPage.xaml.cs), risking drift if the limit ever changes.</summary>
    public const int MaxActivePlans = 2;
}

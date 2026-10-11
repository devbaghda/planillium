namespace Planillium.App.Services;

/// <summary>
/// Single source of truth for which apps count as "messengers" — AppNames (Reports'
/// app-grouping, matched against normalized window-title text) and ActivityTracker
/// (live window-title decoration, matched against the process exe name) used to each
/// keep their own independent list, held in sync only by a code comment reminding
/// whoever edits one to also edit the other. That already failed once for real (Teams
/// recognized in one list but not the other — round-5 audit finding #20); one shared
/// list removes the need to remember at all (round-7 audit finding #20).
/// </summary>
public static class MessengerApps
{
    public sealed record App(string ExeName, string DisplayName);

    public static readonly IReadOnlyList<App> All = new[]
    {
        new App("telegram.exe", "Telegram"),
        new App("whatsapp.exe", "WhatsApp"),
        new App("slack.exe", "Slack"),
        new App("discord.exe", "Discord"),
        new App("signal.exe", "Signal"),
        new App("viber.exe", "Viber"),
        new App("skype.exe", "Skype"),
        // Two exe names for the same app: classic desktop client and the newer client.
        new App("teams.exe", "Microsoft Teams"),
        new App("ms-teams.exe", "Microsoft Teams"),
    };

    /// <summary>Exe filename → display name, for ActivityTracker's live decoration.</summary>
    public static readonly IReadOnlyDictionary<string, string> ByExeName =
        All.ToDictionary(a => a.ExeName, a => a.DisplayName);

    /// <summary>Display names AppNames matches against normalized window-title text,
    /// lowercase. Includes the bare "teams" form some window titles use alongside the
    /// full "Microsoft Teams" display name.</summary>
    public static readonly IReadOnlySet<string> DisplayNames =
        All.Select(a => a.DisplayName.ToLowerInvariant())
           .Append("teams")
           .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

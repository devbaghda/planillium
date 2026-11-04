using System.Reflection;

namespace Planillium.App.Services;

/// <summary>
/// Single source of truth is the &lt;Version&gt; in the csproj — this just
/// reads back what the build stamped, so the log header and Settings page
/// always agree with what shipped ("what version?" is the first support
/// question).
/// </summary>
public static class AppVersion
{
    public static readonly string Current =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";
}

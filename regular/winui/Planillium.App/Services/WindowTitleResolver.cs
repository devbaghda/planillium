using System.Diagnostics;

namespace Planillium.App.Services;

/// <summary>
/// Turns "whatever Windows says is in front right now" into the one string the diary stores —
/// friendly app name appended, messenger unread badges stripped, process name as a last resort.
///
/// Split out of <see cref="ActivityTracker"/> on 2026-08-04 (2026-07-23 audit finding #8). This
/// is an instance, not a static helper, because it owns real state — a pid→app-name cache and
/// two log-once flags — that nothing else in the tracker ever reads or writes. That is exactly
/// the test for a safe extraction and the reason this piece came out while the poll loop's own
/// session/idle fields stayed put: those genuinely interlock, these never did.
/// </summary>
internal sealed class WindowTitleResolver
{
    // Shared with AppNames.Messengers via MessengerApps.ByExeName — see its doc comment
    // (round-5 audit finding #20; centralized round-7 to stop the two lists needing to
    // be kept in sync by hand).
    private static readonly IReadOnlyDictionary<string, string> ExeAppNames = MessengerApps.ByExeName;

    private readonly Dictionary<uint, string> _pidAppCache = new();
    private bool _pidLookupErrorLogged;
    private bool _titleDecorationErrorLogged;

    private static bool IsBadgeNumber(string s)
    {
        var cleaned = new string(s.Where(c => c != ',' && c != '.' && c != ' '
                                           && c != ' ' && c != ' ' && c != '\'').ToArray());
        return cleaned.Length > 0 && cleaned.All(char.IsDigit);
    }

    /// <summary>Removes a messenger's unread-count badge from either end of a window title, so
    /// "(3) Telegram" and "Telegram" don't become two different diary entries. Also called by
    /// <see cref="AppNames"/> when splitting a stored title back into app/page.</summary>
    internal static string StripUnreadBadge(string title)
    {
        var t = title.Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            if (t.EndsWith(')'))
            {
                var open = t.LastIndexOf('(');
                if (open != -1 && IsBadgeNumber(t[(open + 1)..^1]))
                {
                    t = t[..open].TrimEnd(' ', '–', '—', '-').Trim();
                    changed = true;
                    continue;
                }
            }
            if (t.StartsWith('(') && t.Contains(')'))
            {
                var inner = t[1..t.IndexOf(')')];
                if (IsBadgeNumber(inner))
                {
                    t = t[(t.IndexOf(')') + 1)..].TrimStart(' ', '–', '—', '-').Trim();
                    changed = true;
                }
            }
        }
        return t;
    }

    internal string ActiveWindowTitle()
    {
        var (title, pid) = NativeInput.Foreground();

        try
        {
            string app = "";
            string processName = "";
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
                var exe = (processName + ".exe").ToLowerInvariant();
                ExeAppNames.TryGetValue(exe, out app!);
                app ??= "";
            }
            catch (Exception ex)
            {
                // Expected to happen occasionally (process exits between
                // GetForegroundWindow and GetProcessById) — deliberately not
                // logged every time to avoid spamming the log on this
                // per-minute poll. But a *persistent* failure here would
                // otherwise be invisible forever, so log once per run
                // (2026-07-09 audit finding #26).
                if (!_pidLookupErrorLogged)
                {
                    _pidLookupErrorLogged = true;
                    Log.Warn("WindowTitleResolver.ActiveWindowTitle.PidLookup",
                        $"first occurrence (further ones this run are suppressed): {ex.Message}");
                }
            }

            if (app.Length > 0)
            {
                // PIDs get reused constantly on a machine that's up for
                // weeks — an unbounded cache would grow for the life of the
                // process. A full clear past a generous cap is simplest;
                // a cache miss just re-resolves via OpenProcess next poll,
                // the same fallback path a cold cache already takes.
                if (_pidAppCache.Count > 500) _pidAppCache.Clear();
                _pidAppCache[pid] = app;
            }
            else _pidAppCache.TryGetValue(pid, out app!);
            app ??= "";

            if (app.Length > 0)
            {
                var clean = StripUnreadBadge(
                    title.Replace("‎", "").Replace("‏", "").Trim());
                title = !clean.Equals(app, StringComparison.OrdinalIgnoreCase)
                    ? (clean.Length > 0 ? $"{clean} – {app}" : app)
                    : app;
            }
            // A window with a genuinely empty title bar (most commonly the desktop
            // itself, briefly focused between switching apps) and no ExeAppNames
            // entry used to fall through to an empty string here — recorded and
            // shown as a bare "-" with no way to tell what it actually was
            // (2026-07-20 request). The process name is real information already
            // in hand at this point; use it instead of leaving the diary blank.
            else if (title.Length == 0 && processName.Length > 0)
                title = processName;
        }
        catch (Exception ex)
        {
            // Same reasoning as the PID-lookup catch above — log once per
            // run, not every poll (2026-07-09 audit finding #26).
            if (!_titleDecorationErrorLogged)
            {
                _titleDecorationErrorLogged = true;
                Log.Warn("WindowTitleResolver.ActiveWindowTitle.Decoration",
                    $"first occurrence (further ones this run are suppressed): {ex.Message}");
            }
        }

        return title;
    }
}

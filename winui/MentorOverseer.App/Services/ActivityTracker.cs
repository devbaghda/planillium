using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MentorOverseer.App.Services;

/// <summary>
/// C# port of tracker/activity.py — same polling cadence, classification
/// rules, diary-session semantics, idle/sleep detection, and focus-alert
/// escalation. Writes the same activity_log / time_diary rows so reports
/// stay identical whichever app produced the data.
/// IMPORTANT: run only one tracker at a time — close the Python app (or its
/// tray icon) when this one is tracking, or sessions will be double-logged.
/// </summary>
public sealed class ActivityTracker : IDisposable
{
    public const int PollSeconds = 60;

    // ── Win32 ────────────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO lii);
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    private static readonly Dictionary<string, string> ExeAppNames = new()
    {
        ["telegram.exe"] = "Telegram",
        ["whatsapp.exe"] = "WhatsApp",
        ["slack.exe"] = "Slack",
        ["discord.exe"] = "Discord",
        ["signal.exe"] = "Signal",
        ["viber.exe"] = "Viber",
        ["skype.exe"] = "Skype",
    };

    private readonly Dictionary<uint, string> _pidAppCache = new();

    // config-derived rules (same keys as the Python app)
    private readonly List<string> _onPlan;
    private readonly List<string> _offPlan;
    private readonly List<string> _idleOnPlan;
    private readonly List<string> _idleOffPlan;
    private readonly List<string> _idleNeutral;
    private readonly TimeOnly _workStart, _workEnd;
    private static readonly TimeOnly DiaryStart = new(6, 0);
    private static readonly TimeOnly DiaryEnd = new(20, 0);
    private readonly int _graceMin, _repeatMin, _idleThresholdMin;

    // state (poll thread only, except PaidUntil/status reads)
    private string _currentClass = "neutral";
    private string _currentWindow = "";
    private DateTime? _offSince;
    private DateTime? _lastAlert;
    private DateTime? _sessionStart;
    private string? _sessionApp;
    private string? _sessionClass;
    private bool _idleNotified;
    private DateTime? _idleSince;
    private DateTime? _lastPollAt;

    public volatile bool Running;
    public DateTime? PaidUntil;  // set by UI thread when entertainment time is bought

    /// <summary>(title, message) — focus-alert toast.</summary>
    public event Action<string, string>? OnAlert;
    /// <summary>(idleMinutes, idleStart) — user returned from idle/sleep.</summary>
    public event Action<int, DateTime>? OnIdleReturn;
    /// <summary>(cls, window) — after each poll, for the status pill.</summary>
    public event Action<string, string>? OnStatus;

    private Timer? _timer;

    public ActivityTracker(System.Text.Json.JsonElement config)
    {
        static List<string> Words(System.Text.Json.JsonElement cfg, string section, string key)
        {
            var list = new List<string>();
            if (cfg.TryGetProperty(section, out var s) && s.TryGetProperty(key, out var arr))
                foreach (var v in arr.EnumerateArray())
                    if (v.GetString() is { Length: > 0 } str) list.Add(str.ToLowerInvariant());
            return list;
        }
        static int Num(System.Text.Json.JsonElement cfg, string key, int fallback) =>
            cfg.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : fallback;
        static TimeOnly T(System.Text.Json.JsonElement cfg, string key, string fallback)
        {
            var s = fallback;
            if (cfg.TryGetProperty("working_hours", out var wh) &&
                wh.TryGetProperty(key, out var v) && v.GetString() is { Length: > 0 } str) s = str;
            return TimeOnly.TryParse(s, out var t) ? t : TimeOnly.Parse(fallback);
        }

        _onPlan = Words(config, "activity_rules", "on_plan");
        _offPlan = Words(config, "activity_rules", "off_plan");
        _idleOnPlan = Words(config, "idle_activity_rules", "on_plan");
        _idleOffPlan = Words(config, "idle_activity_rules", "off_plan");
        _idleNeutral = Words(config, "idle_activity_rules", "neutral");
        _workStart = T(config, "start", "08:00");
        _workEnd = T(config, "end", "20:00");
        _graceMin = Num(config, "reminder_grace_minutes", 15);
        _repeatMin = Num(config, "reminder_interval_minutes", 5);
        _idleThresholdMin = Num(config, "idle_threshold_minutes", 10);
    }

    public (string Cls, string Window) Status => (_currentClass, _currentWindow);

    public int OffPlanMinutes =>
        _offSince is DateTime s && _currentClass == "off_plan"
            ? (int)(DateTime.Now - s).TotalMinutes : 0;

    public void Start()
    {
        Running = true;
        // One-shot + re-arm: a slow poll (locked DB, hung WinAPI call) must
        // never overlap the next tick — overlapping polls race the session
        // state and write duplicate diary rows.
        _timer = new Timer(_ =>
        {
            if (!Running) return;
            try { PollOnce(); }
            catch (Exception ex) { Log.Error("ActivityTracker.PollOnce", ex); }
            finally
            {
                if (Running)
                    _timer?.Change(TimeSpan.FromSeconds(PollSeconds), Timeout.InfiniteTimeSpan);
            }
        }, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        Running = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    // ── classification (port of classify / classify_idle_text) ──────────

    public string Classify(string title)
    {
        var t = title.ToLowerInvariant();
        foreach (var kw in _onPlan) if (t.Contains(kw)) return "on_plan";
        foreach (var kw in _offPlan) if (t.Contains(kw)) return "off_plan";
        return "neutral";
    }

    public string ClassifyIdleText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "idle";
        var t = description.ToLowerInvariant();
        foreach (var kw in _idleOnPlan) if (t.Contains(kw)) return "on_plan";
        foreach (var kw in _idleOffPlan) if (t.Contains(kw)) return "off_plan";
        foreach (var kw in _idleNeutral) if (t.Contains(kw)) return "neutral";
        return "idle";
    }

    private string EffectiveClass(string cls) =>
        cls == "off_plan" && PaidUntil is DateTime p && DateTime.Now < p ? "paid" : cls;

    // ── window title (port of _active_window_title incl. messenger fixups) ──

    private static bool IsBadgeNumber(string s)
    {
        var cleaned = new string(s.Where(c => c != ',' && c != '.' && c != ' '
                                           && c != ' ' && c != ' ' && c != '\'').ToArray());
        return cleaned.Length > 0 && cleaned.All(char.IsDigit);
    }

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

    private string ActiveWindowTitle()
    {
        var hwnd = GetForegroundWindow();
        var len = GetWindowTextLength(hwnd);
        var sb = new StringBuilder(len + 1);
        if (len > 0) GetWindowText(hwnd, sb, len + 1);
        var title = sb.ToString();

        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            string app = "";
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                var exe = (proc.ProcessName + ".exe").ToLowerInvariant();
                ExeAppNames.TryGetValue(exe, out app!);
                app ??= "";
            }
            catch { /* transient — fall back to the sticky cache below */ }

            if (app.Length > 0) _pidAppCache[pid] = app;
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
        }
        catch { /* never let title decoration break the poll */ }

        return title;
    }

    private static double IdleSeconds()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        return (Environment.TickCount - (int)lii.dwTime) / 1000.0;
    }

    // ── database (same rows as the Python tracker) ───────────────────────

    private static void LogActivity(SqliteConnection conn, string window, string cls)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO activity_log (logged_at, window, class) VALUES ($t, $w, $c)";
        cmd.Parameters.AddWithValue("$t",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$w", window.Length > 240 ? window[..240] : window);
        cmd.Parameters.AddWithValue("$c", cls);
        cmd.ExecuteNonQuery();
    }

    private static void LogDiarySession(SqliteConnection conn, DateTime start, DateTime end,
        string category, string window, string? description = null)
    {
        var duration = Math.Max(1, (int)(end - start).TotalMinutes);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary " +
            "(date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, $m, $c, $w, $x)";
        cmd.Parameters.AddWithValue("$d", start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$s", start.ToString("HH:mm", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$e", end.ToString("HH:mm", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$m", duration);
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$w", window.Length > 240 ? window[..240] : window);
        cmd.Parameters.AddWithValue("$x", (object?)description ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Port of log_idle_answer — called by the idle-return dialog.</summary>
    public void LogIdleAnswer(DateTime idleStart, int idleMinutes, string description)
    {
        var end = idleStart.AddMinutes(idleMinutes);
        var category = ClassifyIdleText(description);
        using var conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        conn.Open();
        LogDiarySession(conn, idleStart, end, category, "idle", description);
    }

    // ── alert escalation (port of _check_alert) ──────────────────────────

    private bool InWorkingHours()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return _workStart <= now && now <= _workEnd;
    }

    private static bool InDiaryHours()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return DiaryStart <= now && now <= DiaryEnd;
    }

    private void CheckAlert(string cls)
    {
        var now = DateTime.Now;
        if (cls != "off_plan" || !InWorkingHours())
        {
            _offSince = null;
            _lastAlert = null;
            return;
        }
        _offSince ??= now;
        var offMin = (now - _offSince.Value).TotalMinutes;
        if (offMin < _graceMin) return;
        if (_lastAlert is null)
        {
            _lastAlert = now;
            OnAlert?.Invoke("Focus check",
                $"You've been off-plan for {(int)offMin} min. Back to the plan!");
        }
        else if ((now - _lastAlert.Value).TotalMinutes >= _repeatMin)
        {
            _lastAlert = now;
            OnAlert?.Invoke("Still off-plan",
                $"{(int)offMin} min off-plan. Return to your work, the user.");
        }
    }

    // ── poll (port of _poll_once) ─────────────────────────────────────────

    private void PollOnce()
    {
        // The Python app runs the same tracker against the same database.
        // Checked every poll (not just at launch) so whichever order the two
        // apps start in, only one ever writes the diary at a time.
        if (Process.GetProcessesByName("MentorOverseer").Length > 0)
        {
            if (_sessionStart is DateTime openStart && _sessionApp != null)
            {
                using var flushConn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
                flushConn.Open();
                LogDiarySession(flushConn, openStart, DateTime.Now, _sessionClass!, _sessionApp);
                _sessionStart = null; _sessionApp = null; _sessionClass = null;
            }
            _currentClass = "paused";
            OnStatus?.Invoke("paused", "");
            return;
        }

        var now = DateTime.Now;
        var title = ActiveWindowTitle();
        var idleS = IdleSeconds();
        var cls = EffectiveClass(Classify(title));
        var diaryEndToday = now.Date + DiaryEnd.ToTimeSpan();

        using var conn = new SqliteConnection($"Data Source={AppPaths.DbPath}");
        conn.Open();

        // Sleep detection: wall-clock gap far beyond the poll interval.
        if (_lastPollAt is DateTime last)
        {
            var sleepS = (now - last).TotalSeconds - PollSeconds;
            if (sleepS >= _idleThresholdMin * 60 && !_idleNotified)
            {
                if (_sessionStart is DateTime ss && _sessionApp != null)
                {
                    LogDiarySession(conn, ss, last, _sessionClass!, _sessionApp);
                    _sessionStart = null; _sessionApp = null; _sessionClass = null;
                }
                _idleSince = last;
                _idleNotified = true;
            }
        }
        _lastPollAt = now;

        _currentWindow = title;
        _currentClass = cls;
        LogActivity(conn, title, cls);
        CheckAlert(cls);
        OnStatus?.Invoke(cls, title);

        if (_idleNotified && idleS < _idleThresholdMin * 60)
        {
            // Returned from idle/sleep.
            var idleEnd = now < diaryEndToday ? now : diaryEndToday;
            var idleStart = _idleSince ?? now.AddSeconds(-idleS);
            var diaryStartToday = now.Date + DiaryStart.ToTimeSpan();
            if (idleStart < diaryStartToday) idleStart = diaryStartToday;

            if (idleStart < idleEnd)
            {
                var actualMin = Math.Max(1, (int)(idleEnd - idleStart).TotalMinutes);
                if (InDiaryHours() && OnIdleReturn != null)
                    OnIdleReturn.Invoke(actualMin, idleStart);
                else if (idleStart < diaryEndToday)
                    LogDiarySession(conn, idleStart, idleEnd, "idle", "idle");
            }
            _idleNotified = false;
            _idleSince = null;
            if (InDiaryHours())
            {
                _sessionStart = now; _sessionApp = title; _sessionClass = cls;
            }
        }
        else if (InDiaryHours())
        {
            if (idleS >= _idleThresholdMin * 60)
            {
                if (!_idleNotified)
                {
                    if (_sessionStart is DateTime ss && _sessionApp != null)
                    {
                        LogDiarySession(conn, ss, now, _sessionClass!, _sessionApp);
                        _sessionStart = null; _sessionApp = null; _sessionClass = null;
                    }
                    _idleSince = now.AddSeconds(-idleS);
                    _idleNotified = true;
                }
            }
            else if (_sessionStart is null)
            {
                _sessionStart = now; _sessionApp = title; _sessionClass = cls;
            }
            else if (title != _sessionApp)
            {
                LogDiarySession(conn, _sessionStart.Value, now, _sessionClass!, _sessionApp!);
                _sessionStart = now; _sessionApp = title; _sessionClass = cls;
            }
        }
        else if (_sessionStart is DateTime open && _sessionApp != null)
        {
            var end = now < diaryEndToday ? now : diaryEndToday;
            if (end > open)
                LogDiarySession(conn, open, end, _sessionClass!, _sessionApp);
            _sessionStart = null; _sessionApp = null; _sessionClass = null;
        }
    }
}

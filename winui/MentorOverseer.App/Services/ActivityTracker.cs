using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MentorOverseer.App.Services;

/// <summary>
/// Polls the foreground window, classifies it on_plan/off_plan/neutral,
/// and writes time_diary rows accordingly, with idle/sleep detection and
/// focus-alert escalation.
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

    // Keep this in sync with AppNames.Messengers below — that set (used to group
    // Reports rows) already recognized Teams; this one (used to decorate the live
    // window title while tracking) didn't, so Teams was classified as a messenger
    // after the fact but not while it was actually happening (round-5 audit
    // finding #20). Both Teams exe names covered: classic desktop client and the
    // newer "ms-teams.exe" client.
    private static readonly Dictionary<string, string> ExeAppNames = new()
    {
        ["telegram.exe"] = "Telegram",
        ["whatsapp.exe"] = "WhatsApp",
        ["slack.exe"] = "Slack",
        ["discord.exe"] = "Discord",
        ["signal.exe"] = "Signal",
        ["viber.exe"] = "Viber",
        ["skype.exe"] = "Skype",
        ["teams.exe"] = "Microsoft Teams",
        ["ms-teams.exe"] = "Microsoft Teams",
    };

    private readonly Dictionary<uint, string> _pidAppCache = new();
    private bool _pidLookupErrorLogged;
    private bool _titleDecorationErrorLogged;

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

    // state (poll thread only — PaidUntil and the rest-day/accounted-until
    // fields below are lock-protected, and _lockPending is volatile,
    // precisely because those are the fields also touched from the UI
    // thread; everything else here is never touched outside PollOnce)
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

    // Rest-day status and the evening-review gap-sweep high-water mark are
    // no longer poll-thread-only: ReviewDialog reads/writes both directly
    // from the UI thread (PendingDayGap/MarkAccountedThrough), concurrently
    // with PollOnce on the timer thread. Lock-protected for the same reason
    // PaidUntil is above — an unguarded read here could see a torn write
    // and, worst case, show a flickering "Day off" pill for one poll or
    // re-ask about a stretch the review already accounted for.
    private readonly object _dayStateLock = new();

    // Rest-day (recurring day off) status, resolved from the plans once per
    // calendar day rather than on every poll — the plan files barely change.
    private DateOnly? _restCheckDate;
    private bool _restDayToday;

    // High-water mark of time already written to the diary by an out-of-band
    // reconcile (the evening review's gap sweep). The return-from-idle handler
    // clamps against it so it never re-asks about, or re-logs, a stretch the
    // sweep already covered.
    private DateTime? _accountedUntil;

    public volatile bool Running;

    // Written by the UI thread (SpendDialog) when entertainment time is
    // bought, read every poll from the background timer thread. `volatile`
    // isn't legal on DateTime? (not one of the types C# allows it on), so
    // this needs an actual lock, not just a keyword (2026-07-09 audit
    // finding #19 — the fix suggested at the time, "mark it volatile,"
    // would not have compiled).
    private readonly object _paidUntilLock = new();
    private DateTime? _paidUntil;
    public DateTime? PaidUntil
    {
        get { lock (_paidUntilLock) return _paidUntil; }
        set { lock (_paidUntilLock) _paidUntil = value; }
    }

    // Set (UI thread, via MainWindow's WM_WTSSESSION_CHANGE hook) the
    // instant Windows reports the session locked; cleared by the next poll.
    // A plain bool (unlike PaidUntil above) IS a legal volatile type, and a
    // few seconds/up to one poll interval of imprecision in exactly when
    // the lock is noticed is an acceptable tradeoff for the simplicity of
    // not needing a lock here too — see NotifySessionLocked's doc comment.
    private volatile bool _lockPending;

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

    /// <param name="lastDiaryEnd">End of the most recent time_diary row, if any.
    /// Seeds the sleep/idle-gap check so the very first poll after a cold
    /// start (app was fully closed, or the machine was off) treats the time
    /// since then the same way a mid-session sleep gap is treated — asked
    /// about via OnIdleReturn — instead of silently vanishing because
    /// _lastPollAt had no prior poll to compare against.</param>
    public void Start(DateTime? lastDiaryEnd = null)
    {
        Running = true;
        _lastPollAt = lastDiaryEnd;
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
                    Log.Warn("ActivityTracker.ActiveWindowTitle.PidLookup",
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
        }
        catch (Exception ex)
        {
            // Same reasoning as the PID-lookup catch above — log once per
            // run, not every poll (2026-07-09 audit finding #26).
            if (!_titleDecorationErrorLogged)
            {
                _titleDecorationErrorLogged = true;
                Log.Warn("ActivityTracker.ActiveWindowTitle.Decoration",
                    $"first occurrence (further ones this run are suppressed): {ex.Message}");
            }
        }

        return title;
    }

    private static double IdleSeconds()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        return (Environment.TickCount - (int)lii.dwTime) / 1000.0;
    }

    // ── database (same rows as the Python tracker) ───────────────────────

    private static void LogDiarySession(SqliteConnection conn, DateTime start, DateTime end,
        string category, string window, string? description = null)
    {
        var duration = Math.Max(1, (int)(end - start).TotalMinutes);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO time_diary " +
            "(date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, $m, $c, $w, $x)";
        cmd.Parameters.AddWithValue("$d", start.ToIsoDate());
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
        using var conn = AppPaths.OpenConnection();
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

    /// <summary>
    /// Whether today is a recurring rest day (all active plans exclude this
    /// weekday). Cached per calendar day so the plan files are read once a
    /// day, not on every 60-second poll.
    /// </summary>
    private bool IsRestDayToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        lock (_dayStateLock)
        {
            if (_restCheckDate != today)
            {
                _restCheckDate = today;
                try { _restDayToday = PlanStore.AllPlansExclude(today); }
                catch (Exception ex) { Log.Error("ActivityTracker.AllPlansExclude", ex); _restDayToday = false; }
            }
            return _restDayToday;
        }
    }

    /// <summary>
    /// If the user was active earlier today but then stopped well before the
    /// tracked day ends, there's a stretch between their last logged activity
    /// and now (capped at the day's diary end) that was never asked about.
    /// Returns that gap as (minutes, start) when it's longer than the idle
    /// threshold, so the evening review can ask "where have you been?" and
    /// close the day out honestly. Null when the day is already accounted for,
    /// when today had no activity at all, or on a rest day.
    /// </summary>
    public (int Minutes, DateTime Start)? PendingDayGap(Database db)
    {
        if (IsRestDayToday()) return null;
        var now = DateTime.Now;
        var diaryStartToday = now.Date + DiaryStart.ToTimeSpan();
        var diaryEndToday = now.Date + DiaryEnd.ToTimeSpan();
        var gapEnd = now < diaryEndToday ? now : diaryEndToday;
        if (gapEnd <= diaryStartToday) return null;

        // Only reconcile a day the user actually worked: the last diary row
        // must be from today. A last row from an earlier day means they simply
        // weren't at the machine today — not a "finished early" gap to ask about.
        if (db.LastDiaryEnd() is not DateTime lastEnd || lastEnd.Date != now.Date) return null;

        var gapStart = lastEnd > diaryStartToday ? lastEnd : diaryStartToday;
        if (gapStart >= gapEnd) return null;

        var mins = (int)(gapEnd - gapStart).TotalMinutes;
        return mins >= _idleThresholdMin ? (mins, gapStart) : null;
    }

    /// <summary>
    /// Records that the diary is now filled through <paramref name="end"/> by
    /// an out-of-band reconcile (the review's gap sweep), so the poll loop's
    /// return-from-idle path won't ask about or re-log that same stretch.
    /// </summary>
    public void MarkAccountedThrough(DateTime end)
    {
        lock (_dayStateLock)
        {
            if (_accountedUntil is not DateTime cur || end > cur) _accountedUntil = end;
        }
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
            var name = ConfigService.UserName;
            var suffix = string.IsNullOrEmpty(name) ? "" : $", {name}";
            OnAlert?.Invoke("Still off-plan",
                $"{(int)offMin} min off-plan. Return to your work{suffix}.");
        }
    }

    // ── poll (port of _poll_once) ─────────────────────────────────────────

    /// <summary>
    /// Called from the UI thread when Windows reports the session locked
    /// (Win+L, screen-saver lock) — see MainWindow's WM_WTSSESSION_CHANGE
    /// handler. Previously there was no lock detection at all: the tracker
    /// only noticed the user was away once GetLastInputInfo's idle time
    /// crossed the configured threshold (10 min default), so locking the
    /// screen and stepping away kept attributing elapsed time to whatever
    /// app was in the foreground for up to that whole threshold
    /// (2026-07-09 audit finding #11). This doesn't touch tracker state
    /// directly — state is poll-thread-only (see the field comments above)
    /// — it just flags the next poll to close out the current session
    /// immediately, the same way the existing sleep/idle-threshold paths
    /// already do.
    /// </summary>
    public void NotifySessionLocked() => _lockPending = true;

    /// <summary>
    /// Runs every <see cref="PollSeconds"/>. Kept as a short dispatcher —
    /// rest-day short-circuit, then each of the four mutually-exclusive
    /// concerns (session-lock, sleep-gap, idle-return, normal session
    /// bookkeeping) lives in its own method below so a fix to one doesn't
    /// require re-reading all the others (round-4 audit finding: this used
    /// to be one ~130-line function tangling all five together).
    /// </summary>
    private void PollOnce()
    {
        var now = DateTime.Now;

        // Rest day (a recurring day off): hold no tracking at all. Behave as
        // if fully outside the tracked hours — drop any open session without
        // logging it (that time is the user's own), clear alert/idle state so
        // nothing fires, and show a "Day off" pill. Nothing is written to the
        // diary, so days off stay blank instead of full of idle rows.
        if (IsRestDayToday())
        {
            _sessionStart = null; _sessionApp = null; _sessionClass = null;
            _offSince = null; _lastAlert = null;
            _idleNotified = false; _idleSince = null;
            _lastPollAt = now;
            _currentClass = "dayoff"; _currentWindow = "";
            OnStatus?.Invoke("dayoff", "");
            return;
        }

        var title = ActiveWindowTitle();
        var idleS = IdleSeconds();
        var cls = EffectiveClass(Classify(title));
        var diaryEndToday = now.Date + DiaryEnd.ToTimeSpan();
        var idleThresholdSec = _idleThresholdMin * 60;

        using var conn = AppPaths.OpenConnection();

        HandleSessionLock(conn, now);
        HandleSleepGap(conn, now, idleThresholdSec);
        _lastPollAt = now;

        _currentWindow = title;
        _currentClass = cls;
        CheckAlert(cls);
        OnStatus?.Invoke(cls, title);

        if (_idleNotified && idleS < idleThresholdSec)
            HandleIdleReturn(conn, now, title, cls, diaryEndToday, idleS);
        else if (InDiaryHours())
            HandleActiveSession(conn, now, title, cls, idleS, idleThresholdSec);
        else if (_sessionStart is DateTime open && _sessionApp != null)
            HandleOutsideDiaryHours(conn, now, diaryEndToday, open);
    }

    /// <summary>Windows session lock/unlock (Win+L, screen-saver): close out
    /// the current session immediately instead of waiting out the full idle
    /// threshold, since <see cref="NotifySessionLocked"/> already told us the
    /// user is definitely gone.</summary>
    private void HandleSessionLock(SqliteConnection conn, DateTime now)
    {
        if (_lockPending && !_idleNotified)
        {
            _lockPending = false;
            if (_sessionStart is DateTime lockedSs && _sessionApp != null)
            {
                LogDiarySession(conn, lockedSs, now, _sessionClass!, _sessionApp);
                _sessionStart = null; _sessionApp = null; _sessionClass = null;
            }
            _idleSince = now;
            _idleNotified = true;
        }
        else
        {
            _lockPending = false;
        }
    }

    /// <summary>Sleep detection: a wall-clock gap far beyond one poll
    /// interval means the machine was asleep, not that the user sat idle for
    /// that whole stretch — close out the session as of the last poll we
    /// actually saw, not "now."</summary>
    private void HandleSleepGap(SqliteConnection conn, DateTime now, int idleThresholdSec)
    {
        if (_lastPollAt is not DateTime last) return;
        var sleepS = (now - last).TotalSeconds - PollSeconds;
        if (sleepS < idleThresholdSec || _idleNotified) return;

        if (_sessionStart is DateTime ss && _sessionApp != null)
        {
            LogDiarySession(conn, ss, last, _sessionClass!, _sessionApp);
            _sessionStart = null; _sessionApp = null; _sessionClass = null;
        }
        _idleSince = last;
        _idleNotified = true;
    }

    /// <summary>Returned from idle/sleep: log the gap (or ask where the user
    /// was, if a UI handler is wired up), then resume a fresh session.</summary>
    private void HandleIdleReturn(SqliteConnection conn, DateTime now, string title, string cls,
        DateTime diaryEndToday, double idleS)
    {
        var idleEnd = now < diaryEndToday ? now : diaryEndToday;
        var idleStart = _idleSince ?? now.AddSeconds(-idleS);
        var diaryStartToday = now.Date + DiaryStart.ToTimeSpan();
        if (idleStart < diaryStartToday) idleStart = diaryStartToday;
        // Don't re-cover time the evening-review gap sweep already logged.
        DateTime? accountedUntil;
        lock (_dayStateLock) { accountedUntil = _accountedUntil; }
        if (accountedUntil is DateTime acc && idleStart < acc) idleStart = acc;

        if (idleStart < idleEnd)
        {
            var actualMin = Math.Max(1, (int)(idleEnd - idleStart).TotalMinutes);
            // Ask on return from idle at ANY hour, not only during diary
            // hours — someone who finishes and steps away in the evening
            // should still be asked where they were. idleStart/idleEnd are
            // already clamped to the diary window just above, so a purely
            // night-time gap collapses to nothing and never reaches here.
            if (OnIdleReturn != null)
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

    /// <summary>Normal in-hours bookkeeping: notice fresh idling, start the
    /// first session of the day, or roll over to a new session when the
    /// foreground app changes.</summary>
    private void HandleActiveSession(SqliteConnection conn, DateTime now, string title, string cls,
        double idleS, int idleThresholdSec)
    {
        if (idleS >= idleThresholdSec)
        {
            if (_idleNotified) return;
            if (_sessionStart is DateTime ss && _sessionApp != null)
            {
                LogDiarySession(conn, ss, now, _sessionClass!, _sessionApp);
                _sessionStart = null; _sessionApp = null; _sessionClass = null;
            }
            _idleSince = now.AddSeconds(-idleS);
            _idleNotified = true;
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

    /// <summary>Outside diary hours with a session still open (crossed the
    /// end-of-diary boundary mid-session): close it out at the boundary, not
    /// at "now" — nothing gets logged past <see cref="DiaryEnd"/>.</summary>
    private void HandleOutsideDiaryHours(SqliteConnection conn, DateTime now, DateTime diaryEndToday,
        DateTime open)
    {
        var end = now < diaryEndToday ? now : diaryEndToday;
        if (end > open)
            LogDiarySession(conn, open, end, _sessionClass!, _sessionApp!);
        _sessionStart = null; _sessionApp = null; _sessionClass = null;
    }
}

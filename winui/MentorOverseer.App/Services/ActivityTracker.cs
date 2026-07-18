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

    // Shared with AppNames.Messengers via MessengerApps.ByExeName — see its doc comment
    // (round-5 audit finding #20; centralized round-7 to stop the two lists needing to
    // be kept in sync by hand).
    private static readonly IReadOnlyDictionary<string, string> ExeAppNames = MessengerApps.ByExeName;

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
    private string _currentClass = DiaryCategory.Neutral;
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

    // "Every active plan off today" — recurring exclusion OR a manually-marked day off —
    // resolved once per calendar day like _restDayToday above. Deliberately separate: a
    // recurring rest day already skips tracking entirely (see PollOnce), but a manual day
    // off should keep tracking normally (2026-07-17 request — "I want to keep the track
    // on those days"), it just shouldn't nag with the off-plan alert.
    private DateOnly? _fullyOffCheckDate;
    private bool _fullyOffToday;

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
            // InvariantCulture on both: these are always fixed HH:mm strings (config.json's
            // "working_hours", validated by SettingsPage's TryParseExact) — reading them
            // back with the current culture could silently fall back on a locale where ':'
            // isn't the time separator (2026-07-18 audit finding R11-03).
            return TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var t)
                ? t : TimeOnly.Parse(fallback, CultureInfo.InvariantCulture);
        }

        _onPlan = Words(config, "activity_rules", DiaryCategory.OnPlan);
        _offPlan = Words(config, "activity_rules", DiaryCategory.OffPlan);
        _idleOnPlan = Words(config, "idle_activity_rules", DiaryCategory.OnPlan);
        _idleOffPlan = Words(config, "idle_activity_rules", DiaryCategory.OffPlan);
        _idleNeutral = Words(config, "idle_activity_rules", DiaryCategory.Neutral);
        _workStart = T(config, "start", "08:00");
        _workEnd = T(config, "end", "20:00");
        _graceMin = Num(config, "reminder_grace_minutes", 15);
        _repeatMin = Num(config, "reminder_interval_minutes", 5);
        _idleThresholdMin = Num(config, "idle_threshold_minutes", 10);
    }

    public (string Cls, string Window) Status => (_currentClass, _currentWindow);

    public int OffPlanMinutes =>
        _offSince is DateTime s && _currentClass == DiaryCategory.OffPlan
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
        foreach (var kw in _onPlan) if (t.Contains(kw)) return DiaryCategory.OnPlan;
        foreach (var kw in _offPlan) if (t.Contains(kw)) return DiaryCategory.OffPlan;
        return DiaryCategory.Neutral;
    }

    public string ClassifyIdleText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return DiaryCategory.Idle;
        var t = description.ToLowerInvariant();
        foreach (var kw in _idleOnPlan) if (t.Contains(kw)) return DiaryCategory.OnPlan;
        foreach (var kw in _idleOffPlan) if (t.Contains(kw)) return DiaryCategory.OffPlan;
        foreach (var kw in _idleNeutral) if (t.Contains(kw)) return DiaryCategory.Neutral;
        return DiaryCategory.Idle;
    }

    private string EffectiveClass(string cls) =>
        cls == DiaryCategory.OffPlan && PaidUntil is DateTime p && DateTime.Now < p ? DiaryCategory.Paid : cls;

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
        string category, string window, string? description = null, SqliteTransaction? tx = null)
    {
        var duration = Math.Max(1, (int)(end - start).TotalMinutes);
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO time_diary " +
            "(date, start_time, end_time, duration_min, category, window, description) " +
            "VALUES ($d, $s, $e, $m, $c, $w, $x)";
        cmd.Parameters.AddWithValue("$d", start.ToIsoDate());
        cmd.Parameters.AddWithValue("$s", start.ToIsoTimeOfDay());
        cmd.Parameters.AddWithValue("$e", end.ToIsoTimeOfDay());
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
        // DiaryCategory.Idle doubles as the "window" placeholder here — no real app was
        // in the foreground, so the diary row's window field is the same sentinel value
        // ReportData.cs checks for when deciding whether to show the description instead.
        LogDiarySession(conn, idleStart, end, category, DiaryCategory.Idle, description);
    }

    /// <summary>
    /// Batch form of LogIdleAnswer for "split into several activities" — the
    /// idle-return dialog's split mode used to call LogIdleAnswer once per
    /// segment, each opening its own connection with no shared transaction,
    /// so a failure partway through a multi-segment split could leave some
    /// segments logged and the rest silently missing with no error shown
    /// (2026-07-14 round-6 audit finding #5). One connection, one
    /// all-or-nothing transaction for the whole split.
    /// </summary>
    public void LogIdleAnswers(IEnumerable<(DateTime Start, int Minutes, string Description)> segments)
    {
        using var conn = AppPaths.OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var (start, minutes, description) in segments)
            {
                var end = start.AddMinutes(minutes);
                var category = ClassifyIdleText(description);
                LogDiarySession(conn, start, end, category, DiaryCategory.Idle, description, tx);
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
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
    /// Whether every active plan is off today — recurring exclusion OR a manually-marked
    /// day off (plan_days_off). Only gates the off-plan nag alert (CheckAlert) — unlike
    /// IsRestDayToday, this does NOT stop tracking; a manually-off day still logs its
    /// diary normally (2026-07-17 request). Cached per calendar day like IsRestDayToday,
    /// but needs a plan_days_off read, so it takes PollOnce's already-open connection
    /// rather than opening a second one just for this.
    /// </summary>
    private bool IsFullyOffToday(SqliteConnection conn)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        lock (_dayStateLock)
        {
            if (_fullyOffCheckDate != today)
            {
                _fullyOffCheckDate = today;
                try
                {
                    var plans = PlanStore.LoadActivePlans();
                    // Same rule ScoreService's scoring exemption uses (Plan.IsOffOn,
                    // 2026-07-18 audit finding R8-04) — just backed by a single-row
                    // check on this poll thread's own connection instead of
                    // ScoreService's per-instance cached set.
                    _fullyOffToday = plans.Count > 0 && plans.All(p => p.IsOffOn(today, (planId, day) =>
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1 FROM plan_days_off WHERE plan_id=$pid AND day=$day";
                        cmd.Parameters.AddWithValue("$pid", planId);
                        cmd.Parameters.AddWithValue("$day", day);
                        return cmd.ExecuteScalar() != null;
                    }));
                }
                catch (Exception ex) { Log.Error("ActivityTracker.IsFullyOffToday", ex); _fullyOffToday = false; }
            }
            return _fullyOffToday;
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

    private void CheckAlert(string cls, SqliteConnection conn)
    {
        var now = DateTime.Now;
        // A manually-marked day off still logs its diary normally, but shouldn't nag you
        // about being off-plan — you're not supposed to be on-plan today at all
        // (2026-07-17 request).
        if (cls != DiaryCategory.OffPlan || !InWorkingHours() || IsFullyOffToday(conn))
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
            _currentClass = DiaryCategory.DayOff; _currentWindow = "";
            OnStatus?.Invoke(DiaryCategory.DayOff, "");
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
        CheckAlert(cls, conn);
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

        // 2026-07-15: a real overnight gap (PC idle since 20:07, resumed at
        // 10:04) produced no "where were you" prompt and no idle diary row —
        // logic tracing said it should have fired, so this and the log line
        // in HandleIdleReturn below are diagnostic only, to catch the actual
        // runtime values next time instead of re-guessing statically.
        Log.Info($"ActivityTracker.HandleSleepGap: gap detected, last={last:o} now={now:o} sleepS={sleepS:F0}");

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

        // Diagnostic (see HandleSleepGap above) — records exactly why a
        // return-from-idle did or didn't produce a prompt, since the
        // 2026-07-15 report of a silent gap couldn't be reproduced by
        // reading the code alone.
        Log.Info($"ActivityTracker.HandleIdleReturn: idleStart={idleStart:o} idleEnd={idleEnd:o} " +
                 $"accountedUntil={accountedUntil:o} hasHandler={OnIdleReturn != null} " +
                 $"willFire={idleStart < idleEnd}");

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
                LogDiarySession(conn, idleStart, idleEnd, DiaryCategory.Idle, DiaryCategory.Idle);
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
            // The poll that first notices idleS crossing the threshold runs
            // up to idleThresholdSec (10 min default) AFTER the user actually
            // stopped — closing the outgoing session through `now` instead
            // of back-computing to the real idle-start moment logged it as
            // still on/off-plan for that whole stretch, which then also
            // OVERLAPPED the idle segment HandleIdleReturn logs starting
            // from that same real idle-start point. Confirmed live
            // (2026-07-15): an on-plan VS Code row and the idle "Break" row
            // that followed it covered the same ~10 minutes twice, each
            // separately counted toward the day's on-plan/idle totals and
            // thus the score. HandleSleepGap already got this right (uses
            // `last`, not `now`, for both); this mirrors that.
            var idleStartPoint = now.AddSeconds(-idleS);
            if (_sessionStart is DateTime ss && _sessionApp != null)
            {
                LogDiarySession(conn, ss, idleStartPoint, _sessionClass!, _sessionApp);
                _sessionStart = null; _sessionApp = null; _sessionClass = null;
            }
            _idleSince = idleStartPoint;
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

using Microsoft.Data.Sqlite;

namespace Planillium.App.Services;

/// <summary>
/// Polls the foreground window, classifies it on_plan/off_plan/neutral,
/// and writes time_diary rows accordingly, with idle/sleep detection and
/// focus-alert escalation.
///
/// What remains here after the 2026-08-04 split (2026-07-23 audit finding #8) is the part that
/// couldn't safely leave: the poll loop and the state machine it drives. Everything below the
/// fields is one interlocking set — an open session, whether we're idle and since when, whether
/// an alert is escalating, how far the evening review has already accounted for — mutated from
/// the poll thread and read from the UI thread under the locks documented on each field. That
/// interlock is exactly why this file has been behind most of this app's real bugs, and exactly
/// why splitting it further along different lines would be risky rather than tidy.
///
/// What did leave, each because it owned state nothing else here touched (or no state at all):
///   <see cref="NativeInput"/>          — the Win32 P/Invoke (foreground window, idle time)
///   <see cref="WindowTitleResolver"/>  — title decoration + the pid→app cache
///   <see cref="ActivityClassifier"/>   — the config keyword lists and matching
///   <see cref="DiaryWriter"/>          — the two time_diary SQL statements
/// The public surface is unchanged; Classify/ClassifyIdleText/StripUnreadBadge stay here as
/// forwarders so no caller had to move with them.
/// </summary>
public sealed class ActivityTracker : IDisposable
{
    public const int PollSeconds = 60;

    private readonly ActivityClassifier _classifier;
    private readonly WindowTitleResolver _titles = new();

    // The working day — and, since 2026-08-04, the diary logging window too: the same hours by
    // definition, not two settings that happen to match. The diary window used to be a pair of
    // hardcoded 06:00/20:00 statics with no relation to working hours and no way to reach them,
    // so moving the working day to 08:00 still left 06:00-08:00 logged and back-filled as
    // "unaccounted time" every morning (the 2026-08-04 report). It was briefly given its own
    // config block and Settings pair; the user's call the same day was that one pair of hours is
    // the whole idea, and a second pair is just another thing that can fall out of sync.
    private readonly TimeOnly _workStart, _workEnd;
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
        _classifier = new ActivityClassifier(config);
        // Scalar timing/threshold defaults now come from ConfigService's shared methods
        // rather than a second, independently-hardcoded copy of the same fallbacks —
        // this constructor is always called with ConfigService.Root itself (see
        // MainWindow.Tracker.cs), so behavior is unchanged, just no longer duplicated.
        _workStart = TimeOnly.FromTimeSpan(ConfigService.WorkStartTime());
        _workEnd = TimeOnly.FromTimeSpan(ConfigService.WorkEndTime());
        _graceMin = ConfigService.ReminderGraceMinutes();
        _repeatMin = ConfigService.ReminderIntervalMinutes();
        _idleThresholdMin = ConfigService.IdleThresholdMinutes();
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

    // ── classification (delegated to ActivityClassifier) ─────────────────

    public string Classify(string title) => _classifier.Classify(title);

    public string ClassifyIdleText(string? description) => _classifier.ClassifyIdleText(description);

    /// <summary>Deliberately still here rather than in <see cref="ActivityClassifier"/>: it
    /// reads PaidUntil, live tracker state written from the UI thread when entertainment time
    /// is bought. Moving it would have dragged a lock and a mutable field into what is
    /// otherwise a pure function of config.</summary>
    private string EffectiveClass(string cls) =>
        cls == DiaryCategory.OffPlan && PaidUntil is DateTime p && DateTime.Now < p ? DiaryCategory.Paid : cls;

    // -- window title (delegated to WindowTitleResolver / NativeInput) ----

    /// <summary>Forwarder kept for <see cref="AppNames"/>, which splits a stored diary title
    /// back into app/page and so needs the same badge rule the title was written with.</summary>
    internal static string StripUnreadBadge(string title) => WindowTitleResolver.StripUnreadBadge(title);

    private string ActiveWindowTitle() => _titles.ActiveWindowTitle();

    private static double IdleSeconds() => NativeInput.IdleSeconds();
    // -- database (delegated to DiaryWriter) -----------------------------
    /// <summary>Port of log_idle_answer — called by the idle-return dialog.</summary>
    public void LogIdleAnswer(DateTime idleStart, int idleMinutes, string description)
    {
        var end = idleStart.AddMinutes(idleMinutes);
        var category = ClassifyIdleText(description);
        using var conn = AppPaths.OpenConnection();
        DiaryWriter.ClearIdlePlaceholder(conn, idleStart, end);
        // DiaryCategory.Idle doubles as the "window" placeholder here — no real app was
        // in the foreground, so the diary row's window field is the same sentinel value
        // ReportData.cs checks for when deciding whether to show the description instead.
        DiaryWriter.LogSession(conn, idleStart, end, category, DiaryCategory.Idle, description);
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
                // Same placeholder-replacement reasoning as LogIdleAnswer above — the split
                // dialog's segments collectively cover the same original placeholder range,
                // so this clears whatever's left of it as each segment is written.
                DiaryWriter.ClearIdlePlaceholder(conn, start, end, tx);
                DiaryWriter.LogSession(conn, start, end, category, DiaryCategory.Idle, description, tx);
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

    /// <summary>Inside the configured working day. This gates two things that used to be
    /// separate windows: whether the off-plan nag may fire, and whether activity is written to
    /// the diary at all. They were merged on 2026-08-04 (see the _workStart field comment) —
    /// one method rather than two identical ones, so they cannot drift into disagreeing about
    /// what "the working day" means.</summary>
    private bool InWorkingHours()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return _workStart <= now && now <= _workEnd;
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
        var diaryStartToday = now.Date + _workStart.ToTimeSpan();
        var diaryEndToday = now.Date + _workEnd.ToTimeSpan();
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
        var diaryEndToday = now.Date + _workEnd.ToTimeSpan();
        var idleThresholdSec = _idleThresholdMin * 60;

        using var conn = AppPaths.OpenConnection();

        // Order matters here (2026-07-21 fix, confirmed via the 2026-07-20 diagnostics below):
        // HandleSleepGap must run BEFORE HandleSessionLock. Both can set _idleSince/_idleNotified
        // for the same poll, but HandleSleepGap anchors to `last` (the true last-known-good poll
        // time, however long ago that was), while HandleSessionLock anchors to `now` (whenever
        // this poll happens to be running, which can be far later than the actual lock — e.g. if
        // Windows throttled this app's background timer for hours while it sat hidden in the
        // tray, or the WM_WTSSESSION_CHANGE message itself was queued during sleep). With the old
        // order, a poll that resumes after a long gap AND has a pending lock notification let
        // HandleSessionLock go first, stamping _idleSince = now and _idleNotified = true — which
        // then made HandleSleepGap's own `|| _idleNotified` guard skip it entirely one line later
        // in the very same call, silently discarding the real gap. Confirmed live: 2026-07-21,
        // tracking picked up at 10:35 instead of the configured 06:00 diary start, with
        // HandleSessionLock's line logging idleSince=now and the immediately-following
        // HandleIdleReturn logging a zero-length idleStart==idleEnd — and no HandleSleepGap line
        // at all despite the process having been running continuously since the previous evening.
        // Running HandleSleepGap first means it claims the gap (correctly, using `last`) before
        // HandleSessionLock gets a chance to overwrite it with the much-less-accurate `now`; if
        // there's no sleep gap, HandleSleepGap is a no-op and HandleSessionLock runs exactly as
        // before.
        HandleSleepGap(conn, now, idleThresholdSec);
        HandleSessionLock(conn, now);
        _lastPollAt = now;

        _currentWindow = title;
        _currentClass = cls;
        CheckAlert(cls, conn);
        OnStatus?.Invoke(cls, title);

        if (_idleNotified && idleS < idleThresholdSec)
            HandleIdleReturn(conn, now, title, cls, diaryEndToday, idleS);
        else if (InWorkingHours())
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
                DiaryWriter.LogSession(conn, lockedSs, now, _sessionClass!, _sessionApp);
                _sessionStart = null; _sessionApp = null; _sessionClass = null;
            }
            _idleSince = now;
            _idleNotified = true;
            // Diagnostic (2026-07-20): a HandleIdleReturn was twice observed the
            // same day with idleStart landing only ~1 poll interval before idleEnd
            // instead of the true, much longer preceding gap — meaning _idleSince
            // got reset somewhere between the real idle-start and the eventual
            // return, despite every _idleSince writer being gated on !_idleNotified
            // (which should make that impossible once _idleNotified is already
            // true). This site is one of the two writers that don't already log —
            // logging here confirms whether a session-lock event was in fact what
            // set (or reset) idleSince right before the next occurrence.
            Log.Info($"ActivityTracker.HandleSessionLock: idleSince set to {now:o}");
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
            DiaryWriter.LogSession(conn, ss, last, _sessionClass!, _sessionApp);
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
        var diaryStartToday = now.Date + _workStart.ToTimeSpan();
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
            // Always log a placeholder immediately, regardless of whether a UI handler is
            // wired up — matches the pre-toast Python dialog's guarantee that a missed or
            // unanswered check-in still ends up in the diary as "dismissed" (now
            // "unaccounted time"), rather than silently vanishing if the toast is never
            // clicked (2026-07-27: a missed morning return-from-sleep toast was leaving
            // that whole stretch out of the diary entirely, with no reconciliation short of
            // waiting for the evening review — rejected as a fix; the user wants the old
            // "always logs something" guarantee back). LogIdleAnswer/LogIdleAnswers replace
            // this same placeholder row in place if the user does go on to answer, instead
            // of inserting a second, overlapping one — see their own comments.
            if (idleStart < diaryEndToday)
                DiaryWriter.LogSession(conn, idleStart, idleEnd, DiaryCategory.Idle, DiaryCategory.Idle,
                    DiaryCategory.IdlePlaceholder);
            // Ask on return from idle at ANY hour, not only during diary
            // hours — someone who finishes and steps away in the evening
            // should still be asked where they were. idleStart/idleEnd are
            // already clamped to the diary window just above, so a purely
            // night-time gap collapses to nothing and never reaches here.
            OnIdleReturn?.Invoke(actualMin, idleStart);
        }
        _idleNotified = false;
        _idleSince = null;
        if (InWorkingHours())
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
                DiaryWriter.LogSession(conn, ss, idleStartPoint, _sessionClass!, _sessionApp);
                _sessionStart = null; _sessionApp = null; _sessionClass = null;
            }
            _idleSince = idleStartPoint;
            _idleNotified = true;
            // Diagnostic (2026-07-20, same investigation as HandleSessionLock's
            // new log line above) — this is the other silent _idleSince writer.
            // Confirmed live: a 198-minute neutral "File Explorer" block (08:04-
            // 11:22) should have set idleSince here to ~08:04 (idleStartPoint,
            // back-computed from idleS the same way the 2026-07-15 fix above
            // already gets right), but the eventual HandleIdleReturn at 11:22
            // logged idleStart=11:22:34 instead — ~1 poll interval before
            // idleEnd, not ~08:04. Logging the value set here lets the next
            // occurrence show whether it's actually written correctly at this
            // site and only goes wrong later, or is already wrong on arrival.
            Log.Info($"ActivityTracker.HandleActiveSession: idleSince set to {idleStartPoint:o} (idleS={idleS:F0}s)");
        }
        else if (_sessionStart is null)
        {
            _sessionStart = now; _sessionApp = title; _sessionClass = cls;
        }
        else if (title != _sessionApp)
        {
            DiaryWriter.LogSession(conn, _sessionStart.Value, now, _sessionClass!, _sessionApp!);
            _sessionStart = now; _sessionApp = title; _sessionClass = cls;
        }
    }

    /// <summary>Outside working hours with a session still open (crossed the end-of-day
    /// boundary mid-session): close it out at the boundary, not at "now" — nothing gets logged
    /// past the configured work end (<see cref="ConfigService.WorkEndTime"/>).</summary>
    private void HandleOutsideDiaryHours(SqliteConnection conn, DateTime now, DateTime diaryEndToday,
        DateTime open)
    {
        var end = now < diaryEndToday ? now : diaryEndToday;
        if (end > open)
            DiaryWriter.LogSession(conn, open, end, _sessionClass!, _sessionApp!);
        _sessionStart = null; _sessionApp = null; _sessionClass = null;
    }
}

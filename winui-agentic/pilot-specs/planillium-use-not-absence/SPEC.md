# SPEC: working inside Planillium is logged as an absence

## Requirement
When the user is actively using Planillium (typing, clicking, saving Settings, re-categorising diary rows),
that time must never come back later as a "Welcome back - you were away N min" gap or an "unaccounted
time" diary row. Real absence (no keyboard/mouse input for the idle threshold, sleep, session lock, app
closed) must still be detected and prompted exactly as today.

## Root cause (investigated, not guessed)
Input detection itself is NOT the problem. `NativeInput.IdleSeconds()` uses `GetLastInputInfo`, which is
system-wide and counts input into any window, Planillium's included. `HandleActiveSession` has no
special-casing of Planillium's own window either. The bug is in tracker lifecycle:

1. A diary row is only written when a session *closes* (window title changes, idle, lock, end of day). A
   long stretch in one window - e.g. hours in Planillium - is an **open, unflushed session** held only in
   the tracker's memory (`_sessionStart`).
2. Several actions *inside Planillium* call `MainWindow.RestartTracker()`: Settings save
   (`Pages/SettingsPage.xaml.cs:359`), diary re-categorisation (`Pages/ReportsPage.Diary.cs:714`), teaching
   rules (`Dialogs/TeachPlanTools.cs:28`). `RestartTracker` does `Tracker.Stop()` (which only stops the
   timer - `ActivityTracker.Stop()` does **not** flush the open session) then builds a new tracker and
   `Start(lastDiaryEnd)`.
3. `Start` seeds `_lastPollAt = lastDiaryEnd`, the end of the last *written* row, which can be hours old
   because the open session was never written. The new tracker's first poll computes
   `sleepS = (now - last) - 60` in `HandleSleepGap`, finds it >= idle threshold, treats the whole stretch
   as a sleep gap (sets `_idleSince = last`, `_idleNotified = true`, drops no session since the new tracker has
   none). The same poll then sees `idleS < threshold` (user is typing) and runs `HandleIdleReturn`, which
   writes an "unaccounted time" placeholder row for [last diary end, now] and fires `OnIdleReturn` - the
   "Welcome back - you were away N min" dialog. The in-session time that was really spent working is lost
   from the diary and shown as absence.

The same loss occurs for any `Stop()` while a session is open: app exit (`MainWindow.xaml.cs:177`) and tray
Pause (`MainWindow.Tray.cs:173`), though for those the following gap is arguably real absence/pause time.

## Chosen approach
Make `ActivityTracker.Stop()` flush the open session to the diary before it tears down, so
`LastDiaryEnd` is current (within one poll) when the next tracker seeds `_lastPollAt`.

- Add a poll/stop mutual-exclusion lock (`_pollLock`) held for the duration of `PollOnce` and taken by `Stop()`,
  so Stop (UI thread) never races a poll in progress (poll thread) over `_sessionStart` etc. (state is
  documented poll-thread-only today; Stop is the one new cross-thread toucher).
- Under that lock in `Stop()`: if `_sessionStart` and `_sessionApp` are set (and not `_idleNotified`), close the
  session with `DiaryWriter.LogSession(conn, _sessionStart, min(now, today's work end), _sessionClass, _sessionApp)`
  then `SetSession(null,null,null)`. Respect working hours: write nothing past `_workEnd`, nothing if
  end <= start (mirror `HandleOutsideDiaryHours`). Wrap in try/catch + `Log.Error` (Stop must never throw).
  Put the DB write in an `internal` method so a test can exercise it.
- Leave `HandleSleepGap`, `HandleSessionLock`, `HandleIdleReturn`, `HandleActiveSession` thresholds and
  `Start(lastDiaryEnd)` untouched - genuine absence paths are unchanged.

Rejected alternatives:
- *Ignore the sleep gap when `idleS` is small.* Loses: on wake from sleep the wake keypress itself makes
  `idleS` ~0, so real sleep would stop being detected.
- *Carry state (open session, `_lastPollAt`) from old tracker to new in `RestartTracker`.* Avoids splitting
  one session into two rows, but needs a snapshot/handover object across threads and does not fix exit/pause;
  more surface in the file behind most of this app's bugs. A one-row split at a restart is harmless
  (same window, contiguous times).
- *Stop restarting the tracker on Settings save.* Out of scope and changes how config applies (documented
  behaviour: new thresholds apply immediately).
- *Special-case Planillium's own window as "always present".* Wrong layer; input detection is already right
  and this would mask real absence with Planillium in front.

## Exact files to change
- `winui-agentic/Planillium.App/Services/ActivityTracker.cs` - `_pollLock`, wrap `PollOnce` body, flush in `Stop()`.
- `winui-agentic/Planillium.App.Tests/ActivityTrackerFlushOnStopTests.cs` (new) - tests below (link/Compile
  rules per `CLAUDE.md`; follow `ActivityTrackerPendingGapTests.cs` and `TestRootFixture.cs`).
- No XAML, no config.json key, no SQLite schema change, no migration.
- Docs in the same pass: `context/todos.md` entry is the orchestrator's job per workflow; add `CHANGELOG.md`
  (Unreleased) line. Not `CONTEXT.md`.

## Edge cases
- Stop with no open session (idle pending, outside hours, rest day): writes nothing.
- Stop while `_idleNotified` is true: no flush (no open session by construction); gap is re-detected by
  the new tracker from last diary end - existing behaviour.
- Stop outside working hours or after `_workEnd`: clamp end to `_workEnd`; if end <= start write nothing.
- Session shorter than a minute: `LogSession` floors to 1 min (existing); acceptable.
- Rapid repeated restarts (several Settings saves): each flushes a short contiguous row; no overlap since
  each new tracker opens its session at its first poll after the previous row's end.
- Poll in flight when Stop is called: Stop waits on `_pollLock` (poll holds it for at most one DB op chain; the
  DB has busy_timeout 2000). Do not hold the lock across `Timer.Dispose` re-arm races: after Stop sets
  `Running=false` the `finally` re-arm in the timer callback must still not re-arm.
- Timezone shift / `ShiftClock` unaffected; flush uses `DateTime.Now` after `CheckClockOffset` state is settled.
- Day boundary: a session opened yesterday is closed at end of day by existing paths; flush clamps to today's
  `_workEnd` using `now.Date`, so a session whose start is on a previous date and still open must not produce a
  negative or cross-date row (end <= start -> skip).
- Genuine absence: 10 min no input, Win+L, sleep, app closed overnight -> still produce the gap prompt/placeholder
  unchanged.
- Tray Pause then Resume: the paused interval is now flagged as gap on Resume from last-flush time rather than from
  an arbitrary old row; behaviour class unchanged (see Open question).

## Acceptance criteria (mechanical)
1. `dotnet build -p:Platform=x64 -c Debug` from `winui-agentic/Planillium.App/` succeeds with no new warnings.
2. `dotnet test` from `winui-agentic/Planillium.App.Tests/` passes, including new tests:
   a. Open a session via `SimulateOpenSession` at T-90 min, call the flush/Stop path against a scratch DB: exactly
      one `time_diary` row, start = session start, end ~now, category/window as the session's.
   b. After that flush, a new tracker seeded with `LastDiaryEnd` as `_lastPollAt` does NOT enter the sleep-gap path
      (`now - lastDiaryEnd - 60s < threshold`) - verify via the same arithmetic helper or an extracted
      predicate; no `Idle`/"unaccounted time" row is written.
   c. Stop with no open session writes zero rows.
   d. Stop after `_workEnd` writes no row extending past `_workEnd`; session start after clamp end writes none.
3. Code inspection: `Stop()` takes the same lock as `PollOnce`; no other place assigns session fields off the poll
   thread without it; `Stop()` cannot throw (try/catch logs).
4. `HandleSleepGap`/`HandleSessionLock`/`HandleIdleReturn`/`HandleActiveSession` and the idle-threshold comparison
   are byte-for-byte unchanged (diff shows only lock wrap + Stop + helper).
5. Sibling sweep (code-inspection, report result): every `Tracker.Stop()` / `RestartTracker()` caller listed above
   benefits; confirm no other path discards `_sessionStart` without writing (`grep SetSession(null`): the
   rest-day branch in `PollOnce` drops it deliberately (documented) and stays as is.
6. Existing `ActivityTrackerPendingGapTests` still pass.

## Safety notes
- Never run against the real `data/progress.db`, real `config.json`, or the live Planillium instance. Tests use
  scratch DB/config via `TestRootFixture` / a temp root in the QA worktree only.
- Do not simulate clicks in the live app (Settings Save would trigger RestartTracker and write diary rows).
- The only DB writes are through app code into scratch copies; no manual SQL against real tables.
- `winui/Planillium.App/` (original side) must not be read or touched.

## Open question
- Tray Pause/Resume: should time between Pause and Resume be treated as "absence" (prompted on resume, as now,
  just anchored from the flush) or silently skipped? Not settled in DECISIONS.md; this spec leaves existing
  behaviour (gap prompted) unchanged and does not add scope.

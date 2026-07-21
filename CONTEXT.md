# Mentor-Overseer — Project Context

**Display name is "Planillium"** (renamed 2026-07-08, prepping to open-source as a
portfolio piece — detail in Session handoff notes). Repo folder, C# namespace, and
this doc's title stay `MentorOverseer` on purpose — internal, invisible to users.
Compiled exe: `Planillium.App.exe`.

## What this app is
A desktop personal mentor and accountability companion that tracks the user's progress
across up to 2 active life/career plans simultaneously. It monitors his activity,
keeps him on-plan, logs his full day (06:00–20:00), and generates weekly reports.

## The user
- Location: Milan, Italy (moving to Utrecht/Eindhoven, NL)
- Goal: Land a Dutch Digital Transformation Manager role → HSM visa → EU citizenship
- Key tools: Power BI, Power Platform, SharePoint, MBA from Bologna
- Active plans: Netherlands Relocation, The Complete 10-Level Claude Code Mastery Guide
  (both under `plans/active/`)

---

## App architecture

**The WinUI app (`winui/MentorOverseer.App`) is THE app.** It started as a Python/
Tkinter app (main.py), was fully rebuilt in WinUI 3/.NET 8 over 2026-07-06/07, and the
Python source was removed from the repo on 2026-07-08 once the WinUI app had shipped
everything it did (still recoverable from git history/tags if ever needed).

```
mentor-overseer/
├── CONTEXT.md              ← you are here. Read this first every session.
├── CLAUDE.md               ← operating instructions (how to work here — build/verify/
│                              publish workflow, safety rules). CONTEXT.md is facts,
│                              CLAUDE.md is process; read both, keep them separate.
├── config.json             ← shared user settings, idle threshold, scoring rules (no
│                              secrets — TickTick client_secret/access_token live in
│                              Windows Credential Manager via CredentialStore, not on disk)
├── plans/
│   ├── active/             ← up to 2 active plan JSONs (e.g. netherlands.json)
│   └── archive/            ← completed plans moved here; frees a slot for a new plan
├── data/
│   ├── progress.db         ← SQLite — see Database schema below
│   ├── winui_state.json    ← window size/theme/kickoff-review state
│   └── mentor-winui.log    ← app log
├── release/                ← Inno Setup installer pipeline (release.ps1, app.iss) — see
│                              the windows-app-releaser skill for how to cut a build
└── winui/MentorOverseer.App/  ← WinUI 3 / .NET 8 source — see Tech stack below
```

---

## Tech stack
- **Framework:** WinUI 3 / .NET 8 (`net8.0-windows10.0.19041.0`), Windows App SDK 1.8.260529003
- **Build:** `dotnet build -p:Platform=x64 -c Release` from `winui/MentorOverseer.App/` —
  no Visual Studio required. Unpackaged, self-contained (`WindowsPackageType=None`,
  `WindowsAppSDKSelfContained=true`), no admin rights needed.
- **Database:** SQLite via `Microsoft.Data.Sqlite`
- **Tray:** H.NotifyIcon.WinUI — close hides to tray, tracking keeps running
- **Activity tracking:** P/Invoke `GetForegroundWindow` + `GetLastInputInfo` (Windows)
- **TickTick:** REST API (OAuth2), own `TickTickAuth`/loopback listener on 8765
- **Credentials:** Windows Credential Manager (`CredentialStore`, python-keyring-compatible
  byte format)
- **Packaging:** Inno Setup via `release/release.ps1` (see `release/README.md`)
- **App name constant:** `AppNames`, `MAX_PLANS = 2`

---

## Plan JSON format
```json
{
  "id": "kebab-case-slug",
  "name": "Plan title",
  "color": "#0a84ff",
  "start_date": "2026-06-29",
  "total_days": 160,
  "briefing": { "high_leverage": [...], "ignore_completely": "...",
                "common_time_wasters": "...", "realistic_timeline": "..." },
  "phases": [
    {
      "phase": 1,
      "name": "Phase Name",
      "tasks": [
        { "day": 1, "task": "Task title", "detail": "...", "mentor_note": "...",
          "category": "profile", "duration_min": 60 }
      ]
    }
  ]
}
```
Required fields: `id`, `name`, `phases`. `mentor_note` (per task) and `briefing`
(plan-level) are what drives the app's "mentor" UI (💡 note under a task, MENTOR'S
NOTE in Details, 📋 Briefing button) — a plan imported without them just won't show
mentor commentary; that's a content gap in the source JSON, not a bug. The "Add Plan"
wizard's 3 prompt templates (`Services/PlanTemplates.cs`) ask Claude to include both.

---

## Database schema (data/progress.db)
```sql
task_completions (id, plan_id, plan_day, task_text, completed, completed_at, last_updated)
  UNIQUE (plan_id, plan_day, task_text)
task_overrides   (plan_id, task_text, original_day, assigned_day)  PK (plan_id, task_text)
  -- reschedule / move-to-today / day-off all write here; "insert don't overlap" shift semantics
plan_days_off    (plan_id, day, marked_at)  PK (plan_id, day)
task_notes       (plan_id, task_text, note, updated_at)  PK (plan_id, task_text)
  -- personal scratchpad per task, inline-editable on Today/Schedule; empty note deletes the row
score_ledger     (id, ts, date, delta, reason, detail)
  -- UNIQUE(reason, date) for reason IN (daily_score, overdue_accrual, weekly_comeback_bonus)
reflections      (id, date UNIQUE, text)          -- evening review answers
ticktick_sync    (id, plan_day, task_text, ticktick_task_id, ticktick_proj_id, pushed_at, synced_at)
  UNIQUE (plan_day, task_text)
time_diary       (id, date, start_time, end_time, duration_min, category, window, description)
  -- category: on_plan | off_plan | neutral | idle | paid
diary_daily_rollup (date PK, on_min, off_min, neutral_min, paid_min, ...)
  -- written just before a day's raw time_diary rows age out (90-day retention, user-
  -- configurable in Settings), keeps
  -- Year-view totals accurate forever after per-entry detail is gone
```

---

## Key business rules
1. Working hours: 08:00–20:00 (configurable in config.json)
2. Diary tracking hours: 06:00–20:00
3. Reminder grace: 15 min off-plan before first alert; escalates every 5 min after
4. Idle threshold: 10 min; a wall-clock gap much larger than the poll interval is
   treated as sleep, not idle
5. Plan day: `(today − start_date).days + 1`, exclusion-aware (`excluded_weekdays`,
   `plan_days_off`)
6. Overdue: any incomplete task from a past day resurfaces; `task_overdue_penalty`
   applies once for the day-of miss (folded into that day's `daily_score`) and again
   *every subsequent day* it stays outstanding (`overdue_accrual`, capped at 3 days).
   Rescheduling doesn't refund penalties already taken.
7. **The user's steady-state rule: one task per day.** A day holding two tasks is only ever
   a transient fact ("I did two things today"), never a permanent state a scheduling
   action should create. This is *why* Reschedule/Day-off and Move-to-today deliberately
   behave differently (clarified with the user 2026-07-09, after an audit flagged the
   difference as a possible inconsistency — it isn't one):
   - **Reschedule / Day-off** use the "insert, don't overlap" forward shift: whatever's
     already on the target day (and everything after it) shifts forward one day first,
     rather than doubling up — because these are "place this specific task on this
     specific day" actions, and the one-task-per-day rule must hold going forward.
   - **Move-to-today** does *not* shift forward (changed 2026-07-09): pulling a future
     task to today just adds it alongside today's own task (a deliberate, transient
     exception to the rule — you really did finish two things today). If that empties
     out the task's old day, everything after it shifts *back* one day to close the gap
     — finishing something ahead of schedule compresses the remaining plan back down to
     one-task-per-day, rather than leaving a dead day in the middle of it.
   - **Already-completed tasks are never shifted** by any of these four operations
     (fixed 2026-07-09; see Session handoff notes — shifting a completed task orphaned
     its `task_completions` row, keyed by assigned day, silently unmarking it and
     moving it to tomorrow).
8. Archive: plan moves to `plans/archive/` when ALL tasks done; frees a slot (max 2
   active plans)
9. Score floor: daily score floors at −10; a `weekly_comeback_bonus` (20 pts) rewards
   a full week back on track after a bad stretch; a `multi_task_bonus_per_extra_task`
   (3 pts, added 2026-07-09) rewards each task completed beyond the first one on the
   same day, on top of the flat per-task rate.
10. **Day-off scoring (added 2026-07-17)**: when EVERY active plan considers a day off
    (recurring exclusion or manual day-off — `ScoreService.AllPlansScoringExempt`; one
    plan off while another still has real work due does NOT trigger this), that day's
    on/off-plan minutes, missed-task penalty, and streak bonus are all suppressed —
    "no points calculation" is the default for a day off. The one exception: a task
    actually brought in and completed that day (e.g. via Move-to-today onto an
    off day) still earns its own task-completion + multi-task-bonus credit — being off
    doesn't forfeit credit for real work done anyway. The same day-off dates are also
    excluded from Reports' aggregate totals (weekly/monthly/yearly summary, Time-by-App,
    Top Distractions) — tracked and still visible in the raw Diary list, just not
    counted toward any total. Editing a diary entry's category/time (or bulk-marking
    several) now recalculates that date's `daily_score` via `RecalculateDayScore`
    (unlike `CreditDayScoreIfMissing`, this overwrites an already-credited day — it has
    to, since the whole point is a stale figure needs updating). The off-plan nag alert
    also doesn't fire on a manually-off day (`ActivityTracker.IsFullyOffToday`) — but
    unlike a recurring rest day, tracking itself still runs normally on a manual day off
    (diary rows keep getting written); only scoring and the alert are suppressed.

---

## config.json key fields
Working hours, reminder/idle timing, `ticktick.client_id` (secret/tokens are in
Credential Manager, never here), `activity_rules` (on_plan/off_plan/neutral keyword
lists), `scoring` (task_completed/task_overdue_penalty/on_plan_hour/off_plan_hour/
streak_bonus_per_day/weekly_comeback_bonus), `score` (points_per_minute/
points_per_currency_unit/currency_symbol — the "buy entertainment time" economy),
`idle_activity_rules` (idle-dialog answer → category reclassification, the user
populates via Settings), `start_with_windows`, `appearance.theme_mode`,
`late_day_task_reminder_hours` (added 2026-07-20, default 2.0 — how long before
`end_of_day_summary_time` the once-a-day "tasks still open" toast fires). Edited via
the in-app Settings dialog, which writes this file and restarts the activity tracker.

---

## Project history (pre-WinUI, Python/Tkinter era — retired 2026-07-07/08)
Built 2026-06-28 through 2026-07-06 as a single-file Tkinter app (`main.py` +
`tracker/`, `ticktick/`). Delivered, in order: plan engine + daily view, TickTick
sync, activity tracker + idle detection, weekly reports, multi-plan support, a
spendable score economy, light/dark theming, a UX/accessibility remediation pass,
and a Claude-assisted plan-generation wizard. A 2026-06-29 security audit fixed
credential storage (moved to keyring/Credential Manager), SQLite thread isolation,
OAuth CSRF, and a plaintext-secret git-history leak (rewritten out via
`filter-branch`). Full blow-by-blow detail for all of this lives in git log — every
commit from that era is still there, the source itself is not. Nothing from this
period needs re-reading to work on the app today; the WinUI rebuild below ported
every feature that mattered.

## Session handoff notes
_Update this section at the end of each Claude Code session. This is an index, not an
archive — full blow-by-blow detail for any entry lives in git log/commit messages.
Compress aggressively rather than letting this grow forever (compressed 852→224 lines on
2026-07-06; ~230→~50 lines on 2026-07-09; rounds 1-5 condensed 2026-07-15; three 07-15 turns
condensed same evening; rounds 1-6 + all 07-15/07-16 entries condensed into one paragraph each
on 2026-07-17 after the round-7 audit)._

- **2026-07-20, late-day task reminder + diary window-title investigation**: user asked for three
  things. (1) New feature: a once-a-day toast warns a configurable number of hours before
  `end_of_day_summary_time` (default 2h, `late_day_task_reminder_hours` in Settings) if today's or
  overdue tasks are still open across active plans — reuses `TodayPage.BuildPlanView`'s exact
  "today's tasks" definition (`AssignedDay == planDay && !Completed`, plus `Overdue`) so it can't
  disagree with what the Today page itself shows, and naturally skips a day every relevant plan
  already excludes (no separate rest-day special-case needed). New `MainWindow.Startup.cs` watcher
  (`StartLateDayTaskReminderWatcher`), same one-tick-per-minute pattern as the existing EOD/kickoff
  watchers. (2) Investigated "why do so many diary rows just say 'File Explorer'" — first pass
  wrongly concluded this wasn't a bug (checked only the raw `time_diary` table, which does have the
  folder/tab name in every row, e.g. `'last INAILs - File Explorer'`). User pushed back with a
  screenshot of the actual Reports diary list still showing bare "File Explorer (198m)" rows — that
  screenshot was the real per-row list, not the "Time by App" summary chart, so the grouping
  explanation was wrong. Real cause: `AppNames.Sub()` (`Services/AppNames.cs`), which the Reports
  page's row label goes through, only extracts a sub-detail (filename/project/chat/etc.) for a fixed
  set of apps — browsers, messengers, VS Code, and a hardcoded Office/Adobe list (`FileSubApps`) —
  and "File Explorer" was never in that list, so its `Group()` name alone ("File Explorer") was
  shown and the folder name in front of it silently dropped, for every File Explorer row, not just
  one. Fixed by adding a case to `Sub()` that returns the leading segment (the folder/tab name) when
  the app group is "File Explorer" — same idea as the existing `FileSubApps` filename case. (3) The
  "-" entries the user saw were rows with a genuinely empty `window` value (118 rows, all `neutral`,
  ~1-2 min each — a window with a blank title bar, most commonly the desktop itself briefly focused
  mid-app-switch) — this WAS a real, if minor, gap: `ActivityTracker.ActiveWindowTitle` now falls
  back to the process name when both the raw title and the `ExeAppNames` lookup come up empty, so
  these show e.g. "explorer" instead of nothing. Both (2) and (3) verified via clean Debug build (0
  warnings) + 83/83 tests. **Not yet in the live Release instance** — held off rebuilding/relaunching
  since the original request landed right at/after the 20:00 `end_of_day_summary_time`, and
  CLAUDE.md's own caution is not to risk skipping that day's evening-review popup; pending the user's
  go-ahead on timing.
  - Lesson: when re-investigating a report the app's own screen contradicts, check what the *screen
    the user is looking at* actually does with the data, not just the raw table — a value can be
    correct in storage and still wrong on screen if a formatting/grouping step between the two drops
    information for a case it wasn't written to handle. Folded into the `windows-app-auditor` global
    skill's "verify, don't vibe" section.
- **2026-07-20, unread-notification tray dot + missing-EOD-notification report**: user reported no
  end-of-day review notification appeared one evening, and asked for all notifications to stay
  "unread" (tray icon shows a dot) until the app is actually looked at. (1) The missing-notification
  report couldn't be conclusively root-caused: the live process had been running continuously since
  well before 20:00 and was still alive over an hour past it, `winui_state.json`'s `last_review`
  never advanced past the previous day, and the log had zero entries (not even an error) in that
  window — ruling out a crash but not narrowing down which of several plausible causes (a stuck
  `DialogGate` semaphore held by an earlier undismissed dialog; `window.IsOnScreen()` misreading
  state; a toast that fired but was never noticed) actually happened. Per the audit skill's own
  "instrument, don't guess" principle, added diagnostic-only logging instead of a speculative fix:
  `DialogGate.ShowAsync` now logs a `Warn` if a wait for the gate exceeds 15s (would catch a stuck
  dialog starving the queue), and `ReviewDialog.Trigger` logs once/day the first time it actually
  attempts to offer the review, naming whether the window was on-screen. Next occurrence should be
  diagnosable from the log. (2) New `Services/NotificationCenter.cs`: a persisted unread counter
  (`unread_notifications` in `winui_state.json`, via `StateService`) that `ToastNotifier.Show`
  increments on every successful toast (centralized there so all current+future call sites
  participate automatically) and that `MainWindow`'s `Activated` event clears — "read" means the
  window was brought to the foreground, there's no separate per-notification inbox. `MainWindow.Tray.cs`
  builds two cached `System.Drawing.Icon`s once at startup (plain + a badged version with a red dot
  composited via GDI+ `FillEllipse` onto the existing `icon.ico`) and swaps `_tray.Icon` between them
  on `NotificationCenter.UnreadChanged`; the badged icon's `Icon.FromHandle`-wrapped HICON is
  destroyed explicitly in `Closed` (that call doesn't take ownership of the handle, a known .NET GDI
  gotcha — left unhandled it would leak one handle for the life of the badge, though since it's only
  built once per process run, not per toggle, this is a fixed one-time cost, not unbounded growth).
  Verified: clean Debug build (0 warnings) + 83/83 tests; the mark-unread→mark-read round trip was
  confirmed end-to-end via a throwaway instance (`MENTOR_ROOT` pointed at a scratch folder, a
  pre-seeded `unread_notifications: 1`, launched, confirmed it reset to `0` on window activation);
  the badge composite itself was verified by rendering the exact same GDI+ calls standalone and
  visually inspecting the output (a clear red dot with a white ring, bottom-right corner) — the tray
  shell's own overflow-flyout UI resisted UI-Automation-driven inspection (auto-closes when a
  non-foreground process invokes it) so the live tray icon wasn't itself screenshotted, but the
  underlying draw call is proven correct and `TaskbarIcon.Icon` is a plain property assignment.
  **Both items are now in the live Release instance** — the user closed the previously-running
  instance (from the prior session's end-of-day/File-Explorer/blank-title batch, which was also
  still pending a rebuild) between turns; rebuilt Release and relaunched cleanly (log shows a normal
  startup, no errors) once confirmed stopped.
- **2026-07-21, diary-tracking-gap RESOLVED (closes the 2026-07-15/07-20 open TODO)**: user reported
  the same bug a third time — today's tracking started at 10:35 instead of the configured 06:00,
  nothing accounted for the gap. This time the 2026-07-20 diagnostics (now live, deployed in that
  session's Release rebuild) caught it directly: at 10:35:31, `HandleSessionLock` logged
  `idleSince set to 2026-07-21T10:35:31` immediately followed by `HandleIdleReturn` logging
  `idleStart=10:35:31 idleEnd=10:35:31` (zero-length, `willFire=False`) — and critically, NO
  `HandleSleepGap` "gap detected" line at all, despite the live process (confirmed via its actual
  `CreationDate`) having been running continuously since 21:36:57 the previous evening with no
  restart. Root cause, now conclusively identified from this evidence plus `PollOnce`'s own control
  flow: `HandleSessionLock` ran *before* `HandleSleepGap` in `PollOnce`. On a poll that resumes after
  a long real gap (most likely Windows throttling this app's background timer for hours while
  it sat hidden in the tray overnight — a documented power-management behavior, not something this
  app controls) while also carrying a pending "session was locked" notification, `HandleSessionLock`
  ran first and stamped `_idleSince = now` / `_idleNotified = true` using "whenever this poll
  happens to run" as the gap's start — then, one line later in the *same* call,
  `HandleSleepGap`'s own guard (`|| _idleNotified`) saw that flag already set and silently skipped
  the check that would have anchored the gap correctly to `_lastPollAt` (the true last-known-good
  time, however long ago). Net effect: the whole gap collapsed to zero and vanished with no diary
  row, no prompt, no log trace of the real duration. **Fix**: swapped the call order so
  `HandleSleepGap` runs first — it claims the gap correctly when there is one; when there isn't, it's
  a no-op and `HandleSessionLock` behaves exactly as before. Verified via clean Debug build (0
  warnings) + 83/83 tests. **Live in Release** — rebuilt and relaunched same session. Watch for one
  more occurrence to confirm (the existing `HandleSleepGap`/`HandleIdleReturn` diagnostic lines are
  enough to see it) since the underlying trigger (timer throttled while backgrounded overnight) isn't
  something this fix prevents — only its previously-silent, gap-eating side effect.
- **2026-07-20, diary-tracking-gap investigation (next occurrence of the 2026-07-15 open TODO)**:
  user reported today's diary starting at 07:59 instead of the configured 06:00 diary-start, with
  no accounting at all for the gap before it. Investigation (log + direct read-only DB query via
  a PowerShell-loaded `Microsoft.Data.Sqlite` — CLI `sqlite3` isn't installed on this machine;
  native `e_sqlite3.dll` had to be added to `PATH` from the build output's
  `runtimes/win-x64/native/` folder before `SQLitePCL.Batteries_V2.Init()` would succeed) confirmed
  this is real, not just "the PC was off": `time_diary` has zero rows for 2026-07-19 (a genuine
  rest day — both active plans exclude Sat+Sun) but ALSO a second, cleaner occurrence the same
  morning mid-session with no reboot/rest-day involved — a 198-minute continuous "neutral - File
  Explorer" block (08:04→11:22) that should have been split by idle detection but wasn't. Both
  `ActivityTracker.HandleIdleReturn` log lines that morning show `idleStart` landing only ~1 poll
  interval (60s) before `idleEnd`, instead of reflecting the true, much longer preceding gap.
  Traced every `_idleSince`/`_idleNotified` write site in `ActivityTracker.cs`: all four are
  correctly gated so `_idleSince` should stay frozen at the true idle-start once `_idleNotified`
  is true, and the timer is already confirmed non-reentrant (one-shot + re-arm, `Start()`
  correctly built) — yet the evidence shows it isn't staying frozen. Root cause NOT conclusively
  found from static reading alone; rather than ship a speculative fix, added `Log.Info` diagnostics
  to the two `_idleSince` writers that were previously silent (`HandleSessionLock`,
  `HandleActiveSession`'s idle branch — commit pending) so the *next* occurrence pins which path
  actually fires and what value it writes. Verified via clean Debug build (0 warnings) + 83/83
  tests; **not yet in the live Release instance** — the diagnostic won't take effect until the
  user rebuilds Release and relaunches, done on their own schedule per the EOD-time caution in
  CLAUDE.md. **Open TODO: watch the log for the next occurrence of this exact signature**
  (idleStart within ~1-2 poll intervals of idleEnd despite a much longer real gap) and check which
  of the two new log lines fired beforehand.
- **2026-07-18, two new global skills + first posting-plan content**: user asked (scheduled via a
  local one-shot cron job to start at 14:30, since a cloud routine can't touch local `~/.claude/skills`
  files) to build two reusable global skills. Both live in the canonical `Desktop/CLAUDE/skills` repo
  (edit there, never directly in `~/.claude/skills` — a previous session's direct edit to the deployed
  copy got silently overwritten by this session's `install-skills.sh --global` run, caught and
  reconciled per `knowledge-upkeep`'s own warned failure mode). **`posting-plan`**: maintains a
  per-project `POSTING_PLAN.md` + `posts/` folder, drafts LinkedIn/Reddit posts in a human voice,
  triggers proactively alongside `knowledge-upkeep` at shipped milestones — never publishes (no
  platform connector exists). Bootstrapped for this project: `POSTING_PLAN.md` now exists here with a
  real drafted post (`posts/2026-07-18-plan-day-perf-fix.md`, about the closed-form perf fix below) plus
  two queued ideas (round-11 audit, the WinUI-rebuild arc) — ready for the user to review and post.
  **`project-media`**: one-page project presentations via the Artifact tool (always available); short
  animated content scoped honestly after verifying this machine's real capabilities (2026-07-18):
  ffmpeg NOT installed, Playwright available via `npx` and can drive the system-installed Edge browser
  with no download needed, no video-generation API connected — so it defaults to an animated one-pager
  (no video file needed) and documents the exact `winget install ffmpeg` gap-closing steps for real
  video export rather than assuming a pipeline that isn't there. Neither skill is Planillium-specific;
  both apply to any future project.
- **2026-07-18, plan-day math performance fix** (user-requested, not an audit round): closed
  out the last open-TODO performance item — see the Open TODOs entry above (now struck
  through) for the closed-form approach and test coverage. Verified via clean Debug build
  (0 warnings; Release skipped since the live exe was running and holding it locked) + 83/83
  tests (up from 19 — new `PlanModelsSchedulingTests.cs`). Also added a "simplicity is king"
  lesson to the global `windows-code-refiner`/`windows-app-auditor` skills after this fix —
  see Standing lessons.
- **2026-07-18, round-11 audit + fix** (5 parallel background sub-agents, all completed —
  extra scrutiny requested on round 10's new code since it hadn't been re-audited yet). 4
  Medium + 6 Low + 5 Info, all 15 fixed same session, verified via clean Debug build (0
  warnings — Release build skipped this pass since the live exe was running and holding the
  output file locked) + 19/19 tests. Headline: **R11-01**, a real gap in round 10's own new
  "Disconnect TickTick" button (no try/catch, unlike every sibling on the page) — caught
  *independently* by two different audit passes (UX and Security) landing on the exact same
  finding, a good confidence signal for the remediation-loop's "re-check the fix's own new
  code" lesson. Other fixes: **R11-02** `MainWindow`'s tracker-startup fire-and-forget Task
  had no try/catch, the one sibling that missed a pattern three other call sites in the same
  file already use; **R11-03** four places reading back a date/time the app itself wrote
  (a plan's `StartDate`, work hours, EOD time) weren't pinned to `InvariantCulture` like the
  write side already was — `PlanModels.StartDateParsed` was the more serious one, could
  silently reset a plan's day-numbering on a non-Gregorian-calendar locale; **R11-04** new
  `DateExtensions.TryParseIsoDate` closes the read-side gap `ToIsoDate` never had a
  counterpart for — `ReportData.cs` had hand-typed the same parse expression 6 times;
  **R11-05** deleted a redundant "belt-and-suspenders" copy of the `sl_reason_date`
  index-creation SQL in `ScoreService`'s constructor — `Database.EnsureSchema` (which the
  `Database` object passed into that constructor has already run) is the only copy that
  actually knows how to migrate that index, so the second copy could only ever be a silent
  no-op if it ever went stale; **R11-06** new `Services/ExportFiles.cs` is now the *one*
  source of truth for the three export filenames — R9-01 had already unified the *delete*
  side, this closes the *write* side (`DataExport.cs`/`ReportExport.cs` no longer retype the
  names); **R11-07/08/09/10/12** Settings polish (Disconnect button naming/ellipsis
  convention, name-save confirmation, retention-days upper bound, "Clear all my data"
  confirmation text now mentions settings/name aren't touched); **R11-13/14** `CredentialStore`
  hardening — `Delete` now logs a warning on a genuine (non-"not found") `CredDeleteW`
  failure instead of leaving no trace, and `Write` zeroes the plaintext secret's managed and
  unmanaged buffers immediately after use instead of leaving them for the GC/allocator;
  **R11-15** the one remaining `ScoreReason` SQL-interpolation site (`RecalculateDayScore`'s
  DELETE) parameterized to match the rest of the file's convention. **R11-11** (TickTick
  disconnect is local-only, doesn't revoke server-side at TickTick) and **R11-16**
  (`ActivityTracker`'s 2026-07-15 diagnostic `Log.Info` lines) were investigated, not code-
  fixed: R11-11 would need a TickTick-side revoke endpoint, out of scope; R11-16's log data
  was actually reviewed this round (17 real firings across 3 days) and shows the diagnostic
  working as intended — the two `willFire=False` entries are both correctly explained by the
  idle period starting after that day's 20:00 diary-hours cutoff, not a bug — so the lines
  stay in place as still-useful, low-cost instrumentation rather than orphaned scaffolding.
  Security pass came back completely clean again (zero new findings) apart from the R11-01
  cross-confirmation.
- **2026-07-18, round-10 audit + fix** (5 parallel background sub-agents this time — all 5
  completed successfully, unlike round 9's outright failures). 4 Medium + 5 Low + 4 Info, all
  13 fixed same session, verified via clean Release build (0 warnings) + 19/19 tests. Two
  themes: (1) round 9's own fixes (`Log.Friendly`, `ExportFileNames`) were applied correctly
  everywhere they were sent, but missed sibling instances of the same shape in files that
  round didn't touch — cross-confirmed independently by two different agents (UX and
  code-quality) landing on the identical `TickTickConnectDialog.cs`/`TickTickSection.cs` gap;
  (2) a real regression in my own round-9 fix: wrapping `ThrowIfExportFilesRemain`'s deliberate
  "your data was cleared, but a file survived" message in `Log.Friendly`'s "Couldn't clear..."
  prefix produced a self-contradicting string. Fixes: new `ExportCleanupException` type lets
  `RunClearActionAsync` display that one message as-is instead of double-wrapping it (R10-01);
  new `CredentialStore.Delete`/`TickTickAuth.Disconnect` + a "Disconnect TickTick" Settings
  button, also folded into "Clear all my data" — previously no in-app way existed to remove the
  3 Credential-Manager-stored TickTick secrets, only Windows Credential Manager by hand (R10-02,
  Medium/privacy); `ScoreService.Overrides()` memoizes `task_overrides` per plan the same way
  `DaysOff` already was, single invalidation point in `SaveOverride` (R10-03, was causing 100+
  redundant SQL round-trips per Reports week-render); `TickTickConnectDialog`/`TickTickSection`
  routed through `Log.Friendly` (R10-04); `TickTickAuth.RedirectUri` exposed `internal` and read
  by the connect dialog instead of a second hardcoded copy of the callback URL (R10-05, same
  "two copies of one fact" shape as round-5 finding #15, just one file over); `AppNames.cs`'s
  three independently-typed dash-separator lists unified into one `DashChars` source (R10-06);
  `SpendDialog`'s two raw ledger-reason literals folded into `ScoreReason` (R10-07); several
  Settings error-copy wording passes for consistency (R10-08/09/10); shared
  `WatcherPollInterval` constant for the EOD/kickoff timers (R10-11); `Log.Friendly` gained an
  optional `action` parameter for call sites with a more specific fix than the generic "try
  again" (R10-12 — applied to `TickTickAuth`'s port-in-use message; `App.xaml.cs`'s
  fatal-startup dialog deliberately left alone, per the audit's own note that its
  multi-paragraph native-MessageBox shape isn't a clean fit); new "Your name" field added to
  Settings, previously only settable once at first run via `NameSetupDialog` (R10-13). Security
  pass came back completely clean — zero new findings.
- **2026-07-18, round-9 audit + fix (no artifact — flat table in chat, per standing
  preference)**: The 5 background sub-agents originally launched for this round all failed
  outright on a session usage-limit error before producing any output — ran the full 5-category
  pass directly (no sub-agents) instead. Found 1 Medium + 1 Info, both fixed same session (no
  separate re-audit loop needed — small enough to fix and verify inline): **R9-01** (Medium,
  code quality/privacy) — the 3-filename export list (`report.html`/`report.csv`/
  `full-export.json`) that both "Clear activity history" and "Clear all my data" use to also
  delete exported copies was typed out as two independent array literals
  (`SettingsPage.xaml.cs:226` and `:448`) — another instance of this project's recurring
  duplicated-lookup-table bug class, and one sitting directly in the R8-05 privacy-fix code path;
  unified into one `ExportFileNames` constant. **R9-02** (Info) — ~10 spots across
  Settings/Today/Plans/Reports/Schedule pages showed a caught exception's raw `.Message` as the
  entire error string; added `Log.Friendly(what, ex)` (technical detail kept, in parentheses,
  since for this app's actual solo-developer audience it's useful for self-diagnosis — just no
  longer the *whole* message) and routed all ~10 sites through it. Everything else checked clean
  on direct re-verification: R8-05's export-delete fix and its own re-audit-caught silent-failure
  fix still intact, `ClearAllData`'s table list still covers all 9 schema tables, no locale
  regressions, no new instances of the duplicated-lookup-table pattern beyond R9-01, icon-only
  buttons still have `AutomationProperties` names, timer reentrancy/disposal/dialog-gating/
  single-instance-mutex/credential-handling all still correct. Verified via clean Release build
  (0 warnings) + 19/19 tests both before and after the fix, plus `dotnet list package
  --vulnerable` (both projects clean).
- **2026-07-18, round-8 full audit + same-day remediation** (5 parallel passes — architecture/
  security/UX/code-quality/privacy; report — note the user's since-established preference is
  flat-text findings in chat, not an artifact, going forward:
  https://claude.ai/code/artifact/016b54c5-9852-4e12-9092-c5fdb799b4e9): 0 Critical, 1 High, 8
  Medium, 7 Low, 3 Info. **All 19 fixed same day** across 4 commits (295e333, 5fb45b1, 42867f7,
  ea37165) after the user asked to fix the findings — triaged 18 AUTO-FIX + 1 NEEDS-DECISION
  (R8-05: unconditional export-file deletion, user chose "delete unconditionally" over an
  opt-in checkbox). Full 5-pass re-audit afterward confirmed all 19 resolved with zero
  regressions in 4 of the 5 categories; the privacy re-audit caught 2 new issues introduced by
  its own R8-05 fix (both "clear data" actions silently swallowed a locked-file delete failure
  into the log instead of telling the user — the exact "logged but not surfaced" gap the fix
  itself should have closed) — fixed in a 4th commit, verified, no further findings. New
  regression test: `ScoreServiceScoringTests.RecalculateDayScore_PreservesThatDaysOwnStreakBonus_NotTodays`
  (19 tests total, up from 18). Headline fix (R8-01, High): `ScoreService.CurrentStreak` now
  takes an optional `asOf` date instead of always anchoring on `DateTime.Today`, so
  `RecalculateDayScore` (called whenever a past diary entry is edited/split/bulk-recategorized)
  no longer silently zeroes a day's real streak bonus — the identical bug was also found and
  fixed in `ReportData.WeekStats` while fixing this one. Other fixes worth remembering: new
  `Plan.IsOffOn` unifies a day-off predicate that `ScoreService`/`ActivityTracker` used to each
  hand-roll (R8-04); new `ScoreReason`/`DiaryCategory.EditableOptions`/`TimeByApp.StackedCategories`
  close out three more instances of this project's recurring duplicated-lookup-table pattern
  (R8-09/11/13); `MoveTaskToToday` now transaction-wrapped like its three siblings (R8-03,
  repeat of the round-5 finding #27 shape). Full original list, plain-English explanations, and
  the fix commits are in the artifact above and in git log 5f067d7..HEAD.
- **2026-07-18, diary-overlap re-audit + git-history purge**: Two open TODOs, both resolved.
  (1) Full-history scan of `time_diary` found 42 overlapping row-pairs (06-29 through 07-16),
  not just the single 07-15 instance previously flagged — see the Open TODOs entry above for
  the count and the user's decision to leave the data untouched rather than guess-correct 40
  pairs with no confirmed single cause. (2) Personal-data git-history purge — see the Open
  TODOs entry above (now struck through) for full detail; also picked up two things not
  originally scoped: an accidentally-committed `obj/` build-artifact tree (would have risked
  binary corruption from the name text-replace — Microsoft's own "David" Hebrew-script font
  name inside a Windows DLL was a false-positive match) and commit-message text (filter-repo's
  `--replace-text` only touches file blobs, not messages — needed a second `--replace-message`
  pass to actually finish the job).
- **2026-07-07 to 07-15, WinUI rebuild through round-6 audit** (full detail in git log; artifact
  links kept for the rounds that have them). v1.0.0 shipped 07-07 (18 findings fixed), app renamed
  "Planillium" (display only). TickTick secret purged from git history via `filter-branch` +
  rotated 07-08/09 (Credential Manager entry still needs the user to reconnect TickTick from Settings
  to pick up the new secret — see Open TODOs). Audit rounds, each verified with full Debug+Release
  build + tests + `dotnet list package --vulnerable` + live UIA pass: rounds 1-2
  (https://claude.ai/code/artifact/1f5b15bb-c6d5-4ea0-a1db-a46b984db19e, 07-09) — diary column
  width, window-clamp-to-monitor, completed-task-shift data loss (business rule 7), move-to-today
  backward compaction, `multi_task_bonus_per_extra_task`, `config.json`/`plans/active/*.json`
  gitignored (history not purged — Open TODOs); round 3
  (https://claude.ai/code/artifact/8bcbfc6d-c998-4084-840c-23e8641483c2) — `ReviewDialog`
  reentrancy guard, per-task Reschedule, God-Object file splits; round 4
  (https://claude.ai/code/artifact/edcb4afc-aea5-429d-bb30-4889c3b04ba6) — `Plan.DriftDays`
  unified, SQLite CVE-2025-6965 pin; round 5
  (https://claude.ai/code/artifact/e54a2317-7406-49ab-bc77-7607b9920860) — introduced
  `Database.RunInTransaction`/`CreateCommand` and `DateExtensions.ToIsoTimestamp()`, the two
  mechanisms every later round keys off; round 6
  (https://claude.ai/code/artifact/4575736f-87f3-4926-b233-6135aaac0530, 07-14, remediated 07-15)
  — `PlanStore.IsValidPlanId` unified, several dialogs made transactional with a `bool?`
  return + new `SaveErrorBar`, `DateExtensions.ToIsoTimeOfDay()`/`ToDisplayDate()`,
  `Services/JsonFileIO.cs` atomic writes, Reports period selector unified to one `RadioButtons`
  group. Same evening (07-15), three more turns: `ReviewDialog._offeredOn` was being set by any
  `ShowCore` call (not just automatic), so a manual "Evening review" click burned the day's
  automatic EOD offer — fixed, plus a 30s live-refresh timer added to the Reports diary section;
  a missing post-overnight-gap "where were you" prompt was investigated but not conclusively
  root-caused — `Log.Info` diagnostics added at the sleep-gap/idle-return decision points
  (**open TODO: watch the log next occurrence**); skills housekeeping (reconciled canonical
  `Desktop/CLAUDE/skills` vs deployed `~/.claude/skills`, added global skill `knowledge-upkeep`);
  **found live** via a user screenshot — `ActivityTracker.HandleActiveSession`'s idle-detected
  branch closed the outgoing session through "now" instead of the real idle-start instant,
  double-counting up to 10 min of on/off-plan time against idle for as long as idle detection has
  existed — fixed by computing idle-start once, shared with `_idleSince` (**open TODO: the
  already-written overlapping rows this caused in `data/progress.db`, e.g. on-plan 11:41→11:51
  inside idle "Break" 11:40→11:55, are still uncorrected, pending the user's explicit go-ahead naming
  the specific rows — there may be older instances further back too, not audited**); all three
  "Add Plan" wizard templates fixed (keyed phases as `"title"` instead of `"name"`, silently
  dropping phase names on import); idle-return dialog now shows the actual clock time range;
  sidebar gained a "Finishes dd.MM.yyyy" line (`Plan.CurrentEndDate`).
- **2026-07-16, reschedule/day-off shifting bugs** (full detail in git log): Fixed three related
  bugs. `RescheduleTaskDialog`/`ReplanOverdueDialog` allowed only tomorrow-or-later as a
  reschedule target, so an overdue task could never be moved to *today* (fixed: `MinDate` now
  uses `plan.PlanDay`). Root cause shared by two more reports: `MarkDayOff`/`UnmarkDayOff`/
  `RescheduleTask`/`MoveTaskToToday` shifted tasks by a blind `±1` with no awareness that another
  day might already be marked off, so a shift could land a task directly on a day-off day while
  leaving a plain working day looking empty — fixed with `NextWorkingDay`/`PrevWorkingDay`
  helpers (skip over `plan_days_off` entries) used at all four shift sites. 4 new regression
  tests. Verified with full build+test+live relaunch.
- **2026-07-17, full 5-category audit + remediation** (full detail in git log/commit messages):
  Ran all 5 `windows-app-auditor` passes in parallel (architecture/security/UX/code-quality/
  privacy). Fixed 1 High (an unobserved-`Task` risk on the toast-notification click-through,
  mirroring a bug class already fixed elsewhere — `MainWindow.xaml.cs`), 9 Medium, and ~15
  Low/Info. Highlights: `StateService.Save` now uses the atomic-write helper; the tray pill and
  Reports page now read activity-category colors from one shared `Services/CategoryStyle.cs`
  table instead of two independently-drifted copies (the "fix one sibling, miss the other"
  pattern recurring a 4th time — see Standing lessons); `TreatWarningsAsErrors` turned on so a
  plain build now enforces the zero-warnings bar a manual `/warnaserror` flag used to;
  TickTick's connect dialog now discloses what data syncs before you connect it; `MANUAL.md`'s
  privacy section rewritten to actually describe window-title tracking/retention/export; a new
  **"Clear all my data" Settings action** wipes every remaining table (task_completions/
  overrides/day-offs/notes/score_ledger/ticktick_sync) plus the debug log — previously only
  activity-history and reflections had any delete path at all; Settings' hours/reminders/
  keyword-list fields now autosave like the rest of the page instead of needing an explicit
  "Save settings" click; `DiaryCategory`/`MessengerApps` shared-constant classes replaced
  scattered string literals across 11+2 files; `ReportData.Buckets` split into
  `MonthBuckets`/`YearBuckets`; a few functions had their pure data-decisions pulled out of
  UI-building code (`TodayPage.BuildPlanView`, `ReportExport.GatherWeekReportData`,
  `TickTickAuth.TryParseCallbackRequest`, `IdleReturnDialog.BuildSegments`) — `ReportsPage.
  Diary.BuildDiarySection` and a few other verbose-but-already-data-separated UI builders
  (`PlansPage.PlanCard`, `ReviewDialog.BuildReviewPanel`, `ReportsPage.TimeByApp.
  AppBreakdownPanel`) were deliberately left unsplit after inspection showed no real hidden
  business-logic-in-UI risk, only inherent WinUI UI-construction verbosity, or (BuildDiarySection,
  IdleReturnDialog) genuine shared-closure-state across UI modes. Both projects confirmed clean
  via `dotnet list package --vulnerable`. Verified with full Debug+Release build + all 14 tests +
  live Release relaunch + log check. Deferred (see Open TODOs): git-history purge, keyboard/
  dark-mode/timing items that need a live human check, not a code change.
- **2026-07-17, day-off scoring feature (same day, after the audit)**: the user asked for four
  related changes to how day-offs interact with scoring — see business rule 10 for the settled
  behavior. Confirmed two design decisions with the user first: the exemption only fires when
  *every* active plan is off that day (not just one), and a brought-in/completed task earns only
  its own task-completion credit, not full normal scoring for the whole day. Implementation:
  `ScoreService.AllPlansScoringExempt` (new) gates `ComputeDayScore`'s on/off-plan/missed/streak
  terms (TaskPoints/MultiTaskBonus never gated); `DayTaskCounts`'s per-plan skip now only fires
  for a *recurring* exclusion, not a manual day-off (a manually-off day-number is real and
  unique, so a task moved onto it — e.g. via Move-to-today — must still be counted, which it
  silently wasn't before); `ScoreService.RecalculateDayScore` (new) lets `daily_score` be
  recomputed after the fact, wired into `EditDiaryEntryDialog`/`SplitDiaryEntryDialog`/
  `ReportsPage.Diary.MarkSelected` so editing/splitting/bulk-recategorizing a diary entry
  refreshes that date's score instead of leaving it stale forever; `ReportData`'s
  `WeekStats`/`MonthBuckets`/`YearBuckets`/`AppBreakdown`/`TopDistractions` all now exclude
  exempt dates from their totals too (the user confirmed: totals, not just score) while the raw
  Diary list stays untouched; `ActivityTracker.IsFullyOffToday` (new, separate from the existing
  recurring-only `IsRestDayToday`) suppresses the off-plan nag alert on a manually-off day
  without pausing tracking itself. `ScoreService.DaysOff` gained a per-instance memoization
  (with cache invalidation on `MarkDayOff`/`UnmarkDayOff`) since `AllPlansScoringExempt` can now
  run hundreds of times in one Reports render (once per date in a Year view). 6 new regression
  tests (`ScoreServiceScoringTests.cs`). Verified with full Debug+Release build + all 18 tests +
  live Release relaunch + log check.
- **Standing lessons** (apply every session, not just the one that taught them):
  - **Simplicity is king: prefer the algebraic/closed-form fix over a caching layer when the
    thing being repeated has structure to exploit.** The `PlanDayForDate`/`DateForPlanDay`
    O(days-elapsed) walk (round-5 finding #28) could have been "fixed" by memoizing results per
    plan — but that adds an invalidation surface (must be cleared whenever `ExcludedWeekdays`
    changes) for a problem the math itself dissolves: the exclusion pattern is weekly-periodic,
    so skipping full weeks in closed form turns O(days-elapsed) into O(1) with zero state to
    keep in sync, ever. When a repeated calculation has periodic/structural regularity, look for
    the closed form before reaching for a cache — a cache is the right tool when the underlying
    work is genuinely irreducible (e.g. `ScoreService.DaysOff`'s DB-backed per-instance cache),
    not when it's just an unexploited pattern in the math.
  - **A remediation's own re-audit must re-check the fix's *own* new code, not just confirm the
    original findings are gone.** 2026-07-18 round-8: the R8-05 fix (delete export files on
    "Clear all my data") wrapped each file-delete in its own try/catch that logged-and-swallowed
    failures — the exact "silent failure, nothing shown to the user" shape that made R8-05 a
    finding in the first place, now reproduced inside its own fix, plus in the untouched sibling
    `ClearHistory_Click` that had carried the same latent bug the whole time. The full 5-pass
    re-audit caught it because it re-ran the whole privacy checklist against the new code instead
    of only checking "is R8-05 gone" — confirms remediation-loop.md's "re-run the FULL audit, not
    just the touched files" instruction is protecting against a real, not hypothetical, failure
    mode: a fix silently reintroducing (or revealing) the same bug class it was meant to close.
  - **"Fix one sibling, miss the other" — general pattern + full history now lives in the global
    `windows-app-auditor` skill (4 rounds, 4 shapes, most recently 07-17's color-table +
    messenger-list duplicates); this project's specific unfixed-until-caught instance: round-5's
    shared color table (`ReportsPage.Styling.CategoryBrushKey`) never got the tray pill
    (`MainWindow.Tracker.UpdatePill`) migrated onto it, and a doc comment wrongly claiming "both
    now read from this one table" went uncorrected for two rounds.
  - Any "show a prompt on a timer" trigger here needs BOTH the `IsOnScreen()`-check-then-toast-
    fallback pattern AND an "already showing, don't reopen" guard — confirmed missing on
    `StartEodWatcher` (round 2), `ReviewDialog` (round 3), `KickoffDialog` (round 5). A once-per-day
    "don't re-offer" flag must only be set by the *automatic* trigger path — if a manual preview
    button shares the same show-and-persist function as the automatic watcher, the manual path
    silently burns the automatic offer with no error to catch it (confirmed: `ReviewDialog`
    07-15).
  - Subscribe to `AppNotificationManager` events *before* `.Register()` (reverse order throws at
    the WinRT layer). A dedup helper (e.g. `ToIsoDate()`) needs a second pass to check *adjacent*
    formats (e.g. timestamps) didn't get left uncovered by the same helper.
  - Check `end_of_day_summary_time` before killing a live instance near EOD — killing it
    early can skip the evening-review popup entirely for that day.
  - The live instance usually runs from `bin\x64\Release\...`, not Debug — confirm via
    `wmic process where "ProcessId=X" get ExecutablePath` before trusting a Debug rebuild
    as verification.
  - Never simulate input (clicks/keystrokes) that would mutate the user's real plan/score
    data — verify data-mutating logic by code inspection + a clean build, not by clicking
    it live. Any direct write to `data/progress.db` outside the app's own code path needs
    the user's explicit confirmation naming the specific table/change first — the harness's
    auto-mode classifier enforces this and will block an unnamed attempt.
  - `CopyFromScreen`/GDI `BitBlt` doesn't capture WinUI3 Mica/DirectComposition content —
    use `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`). For exact layout comparisons,
    UI Automation `BoundingRectangle` beats pixel-diffing screenshots.
  - When a background poll first *notices* a state change (idle crossing a threshold, a timer
    tick), the poll's own timestamp is not the same as when that state change actually happened —
    it can lag by up to the full poll/threshold interval. Log/close out events at the
    back-computed real moment, not at "now," or two records meant to be back-to-back end up
    overlapping instead (see the 2026-07-15 idle-transition overlap bug below).
  - **`git filter-repo` must never run in-place in a repo that has other live `git worktree`
    checkouts attached** (this repo has 3) — it refuses to run at all unless the repo looks
    like a fresh clone (or `--force`), and forcing it in-place risks corrupting the other
    worktrees since they share the same object store. Do the rewrite in an isolated scratch
    clone (`git init` + `git fetch <local-repo-path> branch:branch` for just the branches in
    scope — this also cleanly excludes any other local-only branches from the rewrite), verify
    with a git blob-level diff (`git diff <old-sha> <new-sha> --stat`, not a raw filesystem
    diff — checkout line-ending differences between two separate clones make raw `diff -rq`
    falsely report nearly every file as changed), then push from there and reset the real
    working copy afterward. Also: `--replace-text` only rewrites file blob content, not commit
    messages — a separate `--replace-message` pass is needed to actually remove a string from
    "history" in the sense a user means it (2026-07-18).
- **Open TODOs** (not yet done — the user's or a future session's to pick up):
  - **Diary-tracking-gap bug (2026-07-20): root cause not yet found, diagnostics added.** Watch the
    log for another occurrence of `HandleIdleReturn`'s `idleStart` landing only ~1-2 poll intervals
    before `idleEnd` despite a much longer real gap (two confirmed today: 06:00-07:59, and a
    198-minute unbroken "File Explorer" block idle detection should have split). The two previously-
    silent `_idleSince` writers (`HandleSessionLock`, `HandleActiveSession`'s idle branch) now log —
    check which fires next time and what value it writes. Fix intentionally deferred until the
    mechanism is actually pinned, not guessed.
  - **LinkedIn/Reddit autonomous-publish for `posting-plan`: direction not yet decided, deferred by
    the user (2026-07-20) rather than answered.** Reddit is realistically buildable — a "script" app
    at reddit.com/prefs/apps (instant approval) + OAuth2, credentials in Windows Credential Manager
    like TickTick's, gated behind per-post confirmation. LinkedIn is not: the official API needs a
    slow/uncertain review (often expects a Company Page) and browser-automation posting was declined
    outright as a LinkedIn ToS violation risking the account — the fallback (paste-ready drafts vs.
    pursuing the official API application) is still an open choice. Revisit when the user wants to
    pick a direction; don't build either without their explicit go-ahead given real accounts/ToS are
    involved.
  - **A full-history scan (2026-07-17/18) found 42 overlapping diary-row pairs from 06-29 through
    07-16, not just the one 07-15 instance previously flagged, plus 2 rows with end_time before
    start_time.** Only 2 of the 42 cleanly match the documented `HandleActiveSession` bug
    signature (active row's end == idle row's end, active row started at/before idle start);
    the other 40 are mostly 1-2 minute boundary artifacts with no single confirmed cause. The
    user's call: leave all of it untouched rather than guess-correct real data (2026-07-18).
    Not expected to be revisited unless a clear mechanism for the other 40 turns up.
  - ~~`Plan.PlanDayForDate`/`DateForPlanDay`'s day-by-day walk for plans with excluded weekdays~~
    — **fixed 2026-07-18.** Since the exclusion pattern is by weekday, it repeats every 7
    calendar days — replaced the O(days-elapsed) walk with a closed-form "skip full weeks,
    walk only the remainder" calculation (O(1) plus at most ~7 loop iterations). Kept the
    old walk as a private fallback (`DateForPlanDaySlow`) only for the degenerate all-7-days-
    excluded case (avoids a divide-by-zero; the picker dialog doesn't block that input, though
    it was already a pre-existing infinite-loop risk untouched by this fix). New
    `PlanModelsSchedulingTests.cs` brute-force-verifies the closed form against the plain walk
    across every 0/1/2-weekday exclusion combination plus weekend/3-day cases, 400+ plan-days
    each (83 tests total, up from 19) — deliberately no caching/memoization layer, since the
    math alone is O(1) and doesn't need cache-invalidation bookkeeping the way e.g.
    `ScoreService.DaysOff`'s per-instance cache does.
  - ~~Rotate the TickTick OAuth client secret~~ — **user confirmed done 2026-07-09**
    (rotated at developer.ticktick.com). One follow-up remains, not yet done: the app's
    Windows Credential Manager entry still holds the *old* secret until the user reconnects
    TickTick from Settings (disconnect → "Connect TickTick" → re-auth writes the new value
    via `TickTickAuth.SaveClientSecret`) — until then, TickTick sync will fail with an
    auth error using the now-invalid old secret.
  - ~~Scrub personal data before making the repo public~~ — **done 2026-07-18.** Ran
    `git filter-repo` in an isolated scratch clone (not in-place — this repo has 3 other live
    `git worktree` checkouts sharing its object store, and filter-repo refuses/risks corruption
    unless it's a fresh clone) to: strip `config.json`, `plans/active/*.json`, and an
    accidentally-committed `winui/MentorOverseer.App/obj/` build-artifact tree from every
    commit; and replace the developer's real first name with "the user" in both file content
    and commit messages across all 134 commits (name only appeared in comments/docs, never a
    credential — current-tree source/docs/installer were hand-edited to match first, in the
    same pass, so the name doesn't reappear in the next commit either). Verified before
    pushing: tip-tree file list and content identical to pre-purge (git blob diff, one
    intentional line), both branches + `v1.0.0` tag intact, commit count unchanged (134), zero
    "David" hits left anywhere in history. Force-pushed `master`+`winui-rebuild`+tag to origin,
    resynced this working copy. The 3 other worktrees (`mentor-overseer-audit-test`,
    `-test`, `-theme-test`) are on local-only branches never pushed to origin, so they're
    untouched by this — no action needed unless/until someone tries to reconcile them against
    the rewritten master/winui-rebuild.
  - TickTick redirect URI must be registered at developer.ticktick.com as
    `http://localhost:8765/callback` in the **OAuth redirect URL** field specifically (not
    "App Service URL").

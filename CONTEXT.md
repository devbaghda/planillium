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
- Name: the user
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
- **Framework:** WinUI 3 / .NET 8 (`net8.0-windows10.0.19041.0`), Windows App SDK 1.5.240627000
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

**Known bug (found 2026-07-08, not yet fixed):** `PlanTemplates.cs`'s "Format my own
plan" (reformat) template tells Claude to key each phase's name as `"title"`, but
`PlanModels.cs`'s `Phase` class deserializes `"name"` — any plan generated via that
specific wizard mode gets phases with a silently-empty name. Low impact today since
the Schedule page doesn't render phase names, only `Day N`, so it's not visibly broken
yet.

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
activity_log     (id, logged_at, window, class)   -- one row per 60s poll; pruned to 90 days
diary_daily_rollup (date PK, on_min, off_min, neutral_min, paid_min, ...)
  -- written just before a day's raw time_diary rows age out (365-day retention), keeps
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
7. Reschedule / Move-to-today / Day-off all use the same "insert, don't overlap" shift:
   whatever's already on the target day (and everything after it) shifts forward one
   day first, rather than doubling up — **except already-completed tasks, which are
   never shifted** (fixed 2026-07-09; see Session handoff notes — shifting a completed
   task orphaned its `task_completions` row, keyed by assigned day, silently unmarking
   it and moving it to tomorrow).
8. Archive: plan moves to `plans/archive/` when ALL tasks done; frees a slot (max 2
   active plans)
9. Score floor: daily score floors at −10; a `weekly_comeback_bonus` (20 pts) rewards
   a full week back on track after a bad stretch; a `multi_task_bonus_per_extra_task`
   (3 pts, added 2026-07-09) rewards each task completed beyond the first one on the
   same day, on top of the flat per-task rate.

---

## config.json key fields
Working hours, reminder/idle timing, `ticktick.client_id` (secret/tokens are in
Credential Manager, never here), `activity_rules` (on_plan/off_plan/neutral keyword
lists), `scoring` (task_completed/task_overdue_penalty/on_plan_hour/off_plan_hour/
streak_bonus_per_day/weekly_comeback_bonus), `score` (points_per_minute/
points_per_currency_unit/currency_symbol — the "buy entertainment time" economy),
`idle_activity_rules` (idle-dialog answer → category reclassification, the user
populates via Settings), `start_with_windows`, `appearance.theme_mode`. Edited via
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
_Update this section at the end of each Claude Code session:_

- Last session: 2026-07-09, branch `winui-rebuild`. Continued a prior session
  from repo state alone (no conversation history carried over — reconstructed
  intent from `git status`/diffs). the user gave live screenshot feedback across
  several rounds, so this went through real rebuild/relaunch/verify cycles
  against his running instance, not just code inspection.
  - **`ReportsPage` diary column width instability** — took three iterations
    to actually fix, each surfacing the next layer of the bug: (1) the
    description column was `Star`-sized with ellipsis trimming, which always
    shrinks to fit and can never overflow, so the horizontal scrollbar had
    nothing to scroll to — switched to a fixed `GridLength(480)` column with
    `TextTrimming.CharacterEllipsis` + a `ToolTipService` tooltip for the
    full text on hover. (2) That stopped per-row jitter but the whole page
    still visibly re-centered day to day — `HorizontalAlignment="Center"`
    on the `MaxWidth="880"` body `StackPanel` sizes to its widest child's
    *natural content width* (capped at 880, not fixed at it), so a short
    day still measured narrower than a long one. Switching to `Stretch`
    fixed the width but left a ~29px position-only shift depending on
    content height — a WinUI `ScrollViewer`/`ScrollContentPresenter` quirk
    where a `Stretch`-aligned child's arrange width isn't fully independent
    of content extent (confirmed via UI-Automation `BoundingRectangle`
    reads, not just screenshots — pixel-scanning and scrollbar-visibility
    theories were both tested and ruled out along the way). (3) Root-fixed
    by dropping `MaxWidth`/alignment from XAML entirely and computing the
    content column's width/position explicitly in code-behind
    (`RootScroller_SizeChanged` in `ReportsPage.xaml.cs`) from the
    `ScrollViewer`'s own `ActualWidth` — set top-down by the window/nav
    pane, never by this page's scrollable content, so it's deterministic
    regardless of how tall any given day's diary is. Verified byte-identical
    `BoundingRectangle` (`645,58,880,38`) on both a populated day and a
    confirmed-empty day (Sunday).
  - **Window opens off-screen / oversized** — a second, unrelated bug the user
    caught while screenshotting the above: `AppWindow.Resize` in
    `MainWindow.xaml.cs` had no ceiling against the monitor's actual work
    area and never repositioned after resizing, so a saved state from a
    larger/multi-monitor session (`window_width: 1936` in
    `data/winui_state.json`) could open partially off-screen on a smaller
    display. Fixed by clamping both size *and* position to
    `DisplayArea.GetFromWindowId(...).WorkArea` at startup (needs `using
    Microsoft.UI.Windowing;`). Verified via `GetWindowRect` P/Invoke showing
    exact in-bounds `(0,0)-(1920,1032)`.
  - **Added a `CalendarDatePicker` to the diary header** (`DiaryHeader()` in
    `ReportsPage.xaml.cs`) so a date can be jumped to directly instead of
    only stepping day-by-day, bounded to `Database.DiaryRetentionDays`.
    `DateFormat` uses WinRT template syntax with explicit width specifiers —
    `{day.integer(2)}.{month.integer(2)}.{year.full}` — for a zero-padded
    `dd.MM.yyyy` (the user caught the unpadded `7.7.2026` on the first pass;
    plain `{day.integer}` doesn't zero-pad). Numeric-only tokens deliberately
    avoid Russian month/day names given the OS-locale-vs-app-language
    mismatch documented elsewhere in this file. Verified via UIA
    `ValuePattern` reading `"09.07.2026"`. **Not fully click-verified**: could
    not confirm via automation that the flyout calendar itself opens —
    `CalendarDatePicker` flyouts render in a separate popup window that
    `PrintWindow`-against-the-main-HWND doesn't capture, and simulated
    clicks didn't produce a detectable `Calendar` UIA element either. Standard
    WinUI control, very likely fine, but worth the user clicking it once.
  - Also deleted two stale `CLAUDE_HANDOFF.md` files (root +
    `winui/MentorOverseer.App/`) left by another tool — their rename
    instructions were already fully merged (verified via grep for
    `Planillium.App` in the csproj/manifest and `git log`).
  - **Later same session — completed tasks getting silently unmarked**: the user
    reported that finishing a task today, then pulling a future task to
    today ("get a head start on tomorrow"), unmarked *today's already-done*
    task and moved it to tomorrow. Root cause: `PlanStore.TasksFor` looks up
    a task's completion by `(plan_id, assigned_day, task_text)` — the
    *current* assigned day — but `MoveTaskToToday`'s "insert, don't overlap"
    shift (business rule 7) pushed every task assigned between today and
    the pulled task's old slot forward by one day, **including already-
    completed ones**, orphaning their completion row under the old day.
    Fixed by excluding completed tasks from the shift loop in
    `MoveTaskToToday`, `RescheduleTask`, `MarkDayOff`, and `UnmarkDayOff`
    (`ScoreService.cs`) — a completion is a historical record, not a
    schedule slot, and multiple tasks already share a day routinely so
    there's no collision to avoid by moving it. `ReplanAllOverdue` was
    already safe (only ever touches overdue = incomplete tasks, never
    reassigns anything else). Also added a `multi_task_bonus_per_extra_task`
    scoring rate (default 3, `config.json`/`config.default.json`) — each
    task completed beyond the first one on a given day now adds this on top
    of the flat `task_completed` rate in `ScoreService.DayScore`, surfaced
    as its own line in the evening `ReviewDialog` breakdown. Release build
    verified clean and relaunched; the fix itself was **not** click-tested
    against the user's live data (would require actually completing/moving
    real tasks) per the standing rule against simulating input that mutates
    his real plan/score data — logic verified by code inspection against
    the exact reported symptom instead.
  - **Testing-tool lessons for next time**: `CopyFromScreen`/GDI `BitBlt`
    doesn't capture WinUI3's Mica/DirectComposition rendering correctly —
    use `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`) against the
    specific HWND instead. For exact layout comparisons, UI Automation
    `BoundingRectangle` (via `System.Windows.Automation` in PowerShell) is
    far more reliable than pixel-diffing screenshots. Cached
    `AutomationElement` references go stale across a `Render()` that rebuilds
    the visual tree — re-`FindFirst` inside loops, don't cache across
    iterations. The app's live instance runs from `bin\x64\Release\...`, not
    Debug — confirm via `wmic process where "ProcessId=X" get ExecutablePath`
    before trusting a Debug-only rebuild as verification.
- Previous session: 2026-07-08, branch `winui-rebuild` (now == `master`)
- **Full 5-category audit (windows-app-auditor) + remediation loop**, the user's
  instruction: apply everything. 16 findings (1 Critical, 1 High, 8 Medium, 4
  Low, 2 Info) across architecture/security/UX/code-quality/privacy — full
  writeup in this session's transcript, fixes in commit `2a7b31f` (+ the
  history rewrite below). Highlights:
  - **Critical, fixed**: TickTick `client_secret`/`access_token` were still
    plaintext in git history (`config.json` @ commits `96a6293`/`eb22f8c`, on
    the now-default branch, repo headed for open-source). Rewrote history with
    `git filter-branch` (tree-filter regex-redacts `client_secret`/
    `access_token`/`refresh_token` values in every historical `*.json`),
    purged `refs/original/` + reflog + `git gc --prune=now --aggressive`,
    force-pushed `master`/`winui-rebuild`/`v1.0.0` tag. **the user still needs to
    rotate the actual secret** at developer.ticktick.com — the rewrite only
    removes it from the repo, the value itself is still whatever it always
    was. Old hashes remain reachable from 3 purely-local, never-pushed spots
    (`code-refinement` branch + the `mentor-overseer-test`/
    `mentor-overseer-theme-test` worktrees) — deliberately left alone since
    rewriting them would disrupt those worktrees for no public-exposure
    benefit; harmless unless one is ever pushed.
  - **High, fixed**: no first-run disclosure of activity tracking —
    `NameSetupDialog` now says so before `ActivityTracker.Start()` fires.
  - **Medium, all fixed**: `activity_log` had no retention limit and nothing
    read it (removed the write); `ReportsPage.Render()` was 283 lines doing
    everything (cut to 55, extracted named methods, moved `DiaryInRange` into
    `ReportData.cs`); "All done for today" congratulated empty days with zero
    tasks (copy-honesty bug); no SQLite busy-timeout + lock errors weren't
    surfaced to the user (new `AppPaths.OpenConnection()` + a `SaveErrorBar`
    InfoBar on Today/Schedule); `TickTickService` allocated a new `HttpClient`
    per call including once per project in a loop (now one shared client); no
    in-app data export/delete (new Settings "Export all my data" +  "Clear
    activity history" — see `Services/DataExport.cs`,
    `Database.ExportAllTables`/`ClearActivityHistory`); an empty `catch {}`
    from the Planillium-rename patch broke this codebase's own "every catch
    logs" rule.
  - **Low, fixed**: dead Python-app-detection code in `ActivityTracker`
    (mutex/process probe every 60s poll, now-impossible since the Python
    source is gone) removed, along with the "paused" tray-pill state it fed
    and stale "Python app was already running" copy in Settings;
    `_pidAppCache` now clears past 500 entries instead of growing for a 24/7
    process; added `packages.lock.json` and `THIRD-PARTY-NOTICES.md`.
  - **Not done — real product decisions, deferred**: schema ownership split
    across `Database`/`ScoreService`'s two independent SQLite connections
    (Low, high blast radius to fix, left as-is).
  - `dotnet build /warnaserror` came back clean (only a benign WindowsAppSDK
    RID-usage SDK warning, nothing in this app's own code) — checked before
    starting the fix pass.
- **Renamed app to "Planillium"**, prepping to open-source as a portfolio piece.
  Finished a partial rename another tool left uncommitted (`CLAUDE_HANDOFF.md`,
  deleted 2026-07-09 once its instructions were confirmed already merged):
  `Services/AppInfo.cs` centralizes the display
  name/mutex/startup-registry-value; `csproj`/`app.manifest` identity →
  `Planillium.App`; installer renamed, its uninstall reg-delete sweep now covers
  every legacy Run-key name too; Desktop shortcut retargeted. Kept the repo folder,
  csproj filename, and C# namespace as `MentorOverseer` — internal, invisible to
  users, not worth touching ~50 files' namespace declarations for. Caught a real
  bug first: `CredentialStore`'s Credential Manager target was about to flip from
  `...@MentorOverseer` to `...@Planillium`, which would've silently orphaned
  the user's stored TickTick token — added a `LegacyService` read fallback, verified
  live (TickTick still loaded after rebuild). **Not done — needed before anything
  goes public**: scrub personal data (`plans/active/netherlands.json`, `config.json`
  secrets/keywords, git history) — `release/installer/config.default.json` is a
  template starting point, but going public is a separate decision, not yet made.
- **Repo consolidation**: `master` fast-forwarded to `winui-rebuild` (had diverged
  since WinUI started as a feature branch); branches fully contained in it
  (`audit-remediation`, `ux-audit-fixes`, `ui-theme-light-dark`) deleted local +
  origin; GitHub default branch switched to `master`. `code-refinement` kept (in
  the `mentor-overseer-test/` worktree). Removed ~430MB local build cruft and
  retired the Python source entirely (see Project history above).
- **New features**: per-plan independent scrollable schedule boxes (was one shared
  list — reaching plan 2 meant scrolling past all of plan 1); "Get a head start on
  tomorrow?" prompt once today's tasks are cleared (reuses `MoveTaskToToday`);
  manual "+ Add step" on Plans; personal per-task notes inline on Today/Schedule;
  a "Details" link on Schedule (was Today-only).
- **Fixes**: dark-mode live theme switching (`ThemeSync`'s `ThemeDictionaries`-walk
  silently returned `null` always — replaced with hardcoded Light/Dark hex values
  from the pinned WindowsAppSDK package); Reports page bar-column alignment (3
  layered bugs); reschedule no longer double-books a day (now shifts).
- Also folded in same-day 2026-07-07 work that hadn't made this log yet: v1.0.0
  release (Inno Setup pipeline, version surfacing), overdue-reschedule dialog,
  editable diary entries, app icon, score-chip styling.
- Everything built clean, verified via `PrintWindow` screenshots against a running
  instance. Shift/reschedule logic verified by code-pattern match, not click-tested
  — standing rule against simulating input on the user's live app/data.
- **Lesson**: killing a running instance to rebuild/test can destroy its EOD-review
  watcher before it ever ticks — happened today, killed the real session before
  20:00 while starting the rename work, so the automatic evening-review popup never
  fired; the user had to trigger it manually at 20:23 (worked fine, real data, just no
  reflection prompt). Check the time against `end_of_day_summary_time` before
  stopping a live instance, don't just kill-and-rebuild reflexively near EOD.
- Earlier session: 2026-07-07 — full audit of the WinUI app, all 18 findings
  applied (file logger wired into every silent `catch{}`; global exception
  handlers + single-instance mutex; `ActivityTracker` poll reentrancy fix + pauses
  when the Python app runs; `DialogGate` semaphore around every dialog; EOD-check
  `>=` fix; plan-id filename validation; a11y names; `InvariantCulture` everywhere
  — OS locale is Russian, app language is English). Also **one-app consolidation**
  — WinUI became the sole app (OAuth/Credential-Manager port, tray, plan import,
  full Settings editing, HTML export) — the user's call. Full detail in git log
  around `~9e0..194beec` on `winui-rebuild`.
- Open, still the user's to do: **rotate the TickTick OAuth client secret** at
  developer.ticktick.com. It's been committed in plaintext twice (2026-06-29,
  and again in the `eb22f8c` baseline snapshot) and shown once in a
  screenshot — all three exposures are burned regardless of git state. Both
  git-history leaks have now been rewritten out (2026-06-29's via an earlier
  `filter-branch` pass, `eb22f8c`'s on 2026-07-08 — see above), but a history
  rewrite only removes the *committed record*, not the *value* — the actual
  secret is still whatever it's always been until rotated at the source.
- Known issue: TickTick redirect URI must be registered at developer.ticktick.com
  as `http://localhost:8765/callback` in the **OAuth redirect URL** field
  specifically (not the separate "App Service URL" field).

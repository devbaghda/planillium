# Mentor-Overseer — Project Context

## What this app is
A desktop personal mentor and accountability companion that tracks the user's progress
across up to 2 active life/career plans simultaneously. It monitors his activity,
keeps him on-plan, logs his full day (06:00–20:00), and generates weekly reports.

## The user
- Name: the user
- Location: Milan, Italy (moving to Utrecht/Eindhoven, NL)
- Goal: Land a Dutch Digital Transformation Manager role → HSM visa → EU citizenship
- Key tools: Power BI, Power Platform, SharePoint, MBA from Bologna
- Current active plan: Netherlands Relocation (plans/active/netherlands.json)

---

## App architecture

```
mentor-overseer/
├── CONTEXT.md              ← you are here. Read this first every session.
├── config.json             ← user settings, idle threshold, scoring rules (no secrets —
│                              TickTick client_secret/access_token live in Windows
│                              Credential Manager via `keyring`, not on disk)
├── main.py                 ← single-file app entry point (MentorApp class, ~2100 lines)
├── plans/
│   ├── active/             ← up to 2 active plan JSONs (e.g. netherlands.json)
│   └── archive/            ← completed plans moved here; frees a slot for a new plan
├── tracker/
│   ├── __init__.py
│   └── activity.py         ← window monitor, diary session tracking, idle detection
├── ticktick/
│   ├── __init__.py
│   └── sync.py             ← TickTick OAuth2 REST client
├── data/
│   └── progress.db         ← SQLite: task_completions, activity_log, time_diary, ticktick_sync
├── icon.ico                ← app/exe icon (only icon file actually used by build.bat)
├── requirements.txt        ← pip dependencies
├── MentorOverseer.exe      ← PyInstaller single-file build (rebuild with build.bat, not auto-updated)
└── build.bat               ← PyInstaller build script (no run.bat — use `python main.py` for dev runs)
```
Legacy `plan/roadmap.json` (pre-multi-plan migration source) and icon design exploration
files (`icon_options/`, `gen_*.py`, alternate `.ico`/`.png` variants) were removed
2026-07-04 — fully superseded / unused, still recoverable from git history if ever needed.

---

## Tech stack
- **Language:** Python 3.11 — `C:\Users\devba\AppData\Local\Programs\Python\Python311\python.exe`
- **UI framework:** Tkinter (3-column layout: sidebar + task list + detail panel)
- **Database:** SQLite via sqlite3
- **Notifications:** pystray (tray icon + notify) with plyer fallback
- **Activity tracking:** ctypes `GetForegroundWindow` + `GetLastInputInfo` (Windows)
- **TickTick:** REST API (OAuth2) — redirect URI: `http://localhost:8765/callback`
- **Packaging:** PyInstaller `--onefile --noconsole --icon`
- **App name constant:** `APP_NAME = "Mentor-Overseer"`, `MAX_PLANS = 2`

---

## Build phases — ALL COMPLETE

### Phase 1 — Plan engine + daily view ✅
- Load roadmap, calculate plan day, show today's tasks, mark complete, save to DB
- End-of-day summary at 20:00

### Phase 2 — TickTick sync ✅
- OAuth2 flow, pull personal tasks, push plan tasks, sync completions bidirectionally
- "My Tasks (TickTick)" section shown below plan tasks in the main list

### Phase 3 — Activity tracker ✅
- Polls active window every 60s; classifies on_plan / off_plan / neutral via keyword rules
- Focus alerts: off-plan >15 min → notification; escalates every 5 min
- Full time diary 06:00–20:00: logs every session to `time_diary` table
- Idle detection: `GetLastInputInfo` + wall-clock gap for sleep detection
- After 10 min idle/sleep: blocking modal dialog (no X button, must type) asks "What were you doing?"
  Answer saved to `time_diary` with category="idle"

### Phase 4 — Reports ✅
- Weekly report view (sidebar "Weekly Report" nav)
- Today's productivity score card (tasks × 10, overdue −5, on-plan +3/hr, off-plan −2/hr)
- Weekly table: 7 days of tasks / on-plan min / off-plan min / score
- Top distractions with mini bar chart
- Rule-based insights (>2h off-plan, <50% task completion, etc.)
- Time Diary section: stacked breakdown bar + chronological timeline; idle entries in amber italic
- Export HTML report button → opens `report.html` in browser

### Multi-plan architecture ✅
- Up to 2 active plans loaded from `plans/active/*.json`
- Auto-migrates legacy `plan/roadmap.json` → `plans/active/netherlands.json` on first run
- Sidebar shows "ACTIVE PLANS" section with colored dot, day counter, today's progress
- "+ Add Plan" button (file picker → validate → ask start date → copy to active/)
- "Archive ✓" button appears when all tasks done → moves to `plans/archive/`; frees slot
- Daily task list: per-plan colored section header + Overdue/Today sub-sections, then TickTick section
- Activity from any active plan counts as on-plan

### Settings + Score economy ✅ (2026-07-02)
- ⚙ button in sidebar header → in-app Settings dialog (working hours, reminders,
  scoring rates, score economy rates, activity keywords, idle-answer library).
  Saving updates `config.json` and restarts the activity tracker.
- Score is now a spendable, persistent balance (`score_ledger` table), credited once
  per day from the existing score formula (now actually reads `config["scoring"]`
  instead of hardcoded numbers) plus a streak bonus for consecutive fully-completed
  days.
- Sidebar SCORE section: balance, "Buy entertainment time" (spend points for a timed
  window — off-plan activity during it logs as `paid`, doesn't cost score, doesn't
  trigger focus-alert nagging), "Log spend (no regrets)" (manual expenditure,
  deducts points at a configurable rate).
- Idle-answer library: typed idle-dialog answers get matched against
  `config["idle_activity_rules"]` and reclassified from generic `idle` to
  on_plan/off_plan/neutral when they match — seeded empty, the user populates via
  Settings.
- TickTick sidebar simplified to a single "Connect TickTick" → "TickTick connected"
  control; push + completion sync now run automatically every 5 min once connected,
  no manual buttons.

---

## Plan JSON format
```json
{
  "id": "netherlands",
  "name": "Netherlands Relocation",
  "color": "#0a84ff",
  "start_date": "2026-06-29",
  "total_days": 160,
  "phases": [
    {
      "phase": 1,
      "name": "Phase Name",
      "tasks": [
        { "day": 1, "task": "Task title", "detail": "...", "category": "profile", "duration_min": 60 }
      ]
    }
  ]
}
```
Required fields when adding a plan: `id`, `name`, `phases`. `start_date` asked interactively if missing.

---

## Database schema (progress.db)

```sql
task_completions (id, plan_id TEXT DEFAULT 'netherlands', plan_day, task_text, completed,
                  completed_at, last_updated)
  UNIQUE INDEX tc_plan_idx ON (plan_id, plan_day, task_text)

activity_log (id, logged_at, window, class)          -- one row per 60s poll

time_diary (id, date, start_time, end_time,
            duration_min, category, window, description)
  -- category: on_plan | off_plan | neutral | idle
  -- description: user's answer to idle dialog (idle rows only)

ticktick_sync (id, plan_day, task_text, ticktick_task_id,
               ticktick_proj_id, pushed_at, synced_at)
  UNIQUE(plan_day, task_text)
```

---

## Key business rules
1. Working hours: 08:00–20:00 (configurable in config.json)
2. Diary tracking hours: 06:00–20:00 (hardcoded in ActivityTracker)
3. Reminder grace: 15 min off-plan before first alert
4. Reminder escalation: every 5 min after first
5. Idle threshold: 10 min (config `idle_threshold_minutes`)
6. Sleep detection: if wall-clock gap between polls >> POLL_SECONDS → treat as sleep/idle
7. Idle dialog: modal Toplevel, X button disabled, submit disabled until text entered
8. Plan day: `(today - start_date).days + 1`
9. Overdue: any incomplete task from a past day surfaces again with [OVERDUE] style
10. Archive: plan moves to `plans/archive/` when ALL tasks done; slot freed for new plan
11. Completions key: `(plan_id, plan_day, task_text)` — both in-memory dict and DB
12. Overdue scoring: `task_overdue_penalty` applies once per day *for every day a task
    stays overdue* (not just the day it was first missed) — the "day-of" miss is folded
    into that day's `daily_score` ledger entry as before; each subsequent day it's still
    outstanding adds a further `overdue_accrual` ledger entry. Rescheduling a task (see
    below) doesn't refund penalties already taken.

---

## config.json key fields
```json
{
  "user_name": "the user",
  "plan_start_date": "2026-06-29",
  "working_hours": { "start": "08:00", "end": "20:00" },
  "reminder_grace_minutes": 15,
  "reminder_interval_minutes": 5,
  "idle_threshold_minutes": 10,
  "end_of_day_summary_time": "20:00",
  "ticktick": { "client_id": "..." },
  "activity_rules": {
    "on_plan":  ["Power BI", "PowerPoint", "LinkedIn", "Excel", "Claude", ...],
    "off_plan": ["Netflix", "YouTube", "Steam", "Tanki Online", "Besiege", ...],
    "neutral":  ["Chrome", "Firefox", "Slack", ...]
  },
  "scoring": { "task_completed": 10, "task_overdue_penalty": -5,
               "on_plan_hour": 3, "off_plan_hour": -2, "streak_bonus_per_day": 5 }
}
```
`ticktick.client_secret` / `access_token` / `refresh_token` are never written to this file —
they live in Windows Credential Manager via `keyring`, migrated out of config.json on first
launch after the 2026-06-29 audit fix.

---

## Session handoff notes
_Update this section at the end of each Claude Code session:_

- Last session: 2026-07-07, branch `winui-rebuild`
- **Full audit of the WinUI app + all 18 actionable findings applied** (the user's
  instruction: apply everything, do NOT re-audit — a re-audit pass is still owed
  whenever he asks). What changed, by finding #:
  - #1/#2 logging: new `Services/Log.cs` (thread-safe file logger →
    `data/mentor-winui.log`, invariant timestamps, never throws); every
    previously-silent `catch {}` in pages/services/dialogs now calls
    `Log.Error(context, ex)` first.
  - #3/#9 App.xaml.cs: `UnhandledException` (+`e.Handled=true`),
    `TaskScheduler.UnobservedTaskException`, AppDomain handler; single-instance
    named mutex `MentorOverseerWinUI_SingleInstance` (second launch logs + exits);
    `AppNotificationManager.Register()` wrapped in try/catch.
  - #4/#10/#13 ActivityTracker: poll timer is now one-shot + re-arm in `finally`
    (no reentrancy pile-up on slow polls); every poll checks for the Python
    `MentorOverseer.exe` first and pauses (flushes open diary session, pill shows
    "Python app is tracking"); private DB method renamed `Log`→`LogActivity`
    (shadowed the new Log class, CS0119).
  - #5 `Dialogs/DialogGate.cs`: SemaphoreSlim(1,1) around all 7 ContentDialog
    call sites — no more "already an open dialog" crash.
  - #6/#7 MainWindow: EOD check uses `>=` (fires at exactly EOD, once/day via
    `_eodOfferedOn` + LastReview); close now confirms via AppWindow.Closing
    ("tracking and focus alerts stop") with `_reallyClose` flag.
  - #8 AddPlanDialog: plan `id` validated against `^[a-z0-9][a-z0-9_-]{0,63}$`
    before it becomes a filename.
  - #11 close confirmation (see #7), #12/#17 a11y: AutomationProperties.SetName
    on every task/TickTick checkbox; TickTick section extracted to
    `Views/TickTickSection.cs` (network I/O out of TodayPage.Render).
  - #13/#16 InvariantCulture on ALL DB-bound date strings and UI dates (OS
    locale is Russian; app language is English).
  - #14/#15 copy: zero-task kickoff no longer celebratory; review dialog shows
    "floored at −10" split and blocks closing the day before EOD.
  - #18 Database.cs: `$cat` param renamed `$done_at` (was bound to done_at).
  - Verified: `dotnet build -p:Platform=x64` clean (0 warnings/0 errors); smoke
    run — window up + responsive, second instance blocked by mutex (confirmed in
    `data/mentor-winui.log`), no error entries.
- Previous session: 2026-07-06 (second pass), branch `winui-rebuild` (off `ux-audit-fixes`)
- **WinUI rebuild phase 1 shipped** (`e44b6f9`): the user approved the Fluent redesign
  (mockup: https://claude.ai/code/artifact/a3ab9082-83c6-4bb5-8dac-5f696bdc12c0 —
  full design direction in memory `project-design-direction`). New `winui/
  MentorOverseer.App` — WinUI 3 / .NET 8 / WASDK 1.5, unpackaged + self-contained,
  builds with plain `dotnet build -p:Platform=x64` (no Visual Studio). .NET 8 SDK
  was installed **user-scope** at `%LOCALAPPDATA%\Microsoft\dotnet` (on user PATH).
  Shell: Mica + custom title bar + NavigationView + score chip. Today page reads
  the REAL plans/db (same files as the Python app, found by walking up from the
  exe or via MENTOR_ROOT) and toggles completions with byte-identical SQL.
  Verified live: built, launched, PrintWindow screenshot — real plan, real
  overdue meta, real score. Polish backlog: date header uses system locale
  (showed Russian day names), Replan button disabled (score v2 phase), stub
  pages for Schedule/Reports/Plans/Settings.
  **Screenshot lesson v2:** even after confirming the window title,
  `CopyFromScreen` grabs whatever is visually on top (captured the user's live
  Tanki Online game once, and `SetForegroundWindow` yanked his fullscreen
  focus). Use `PrintWindow` with flag `PW_RENDERFULLCONTENT` (2) instead —
  renders the target window's own surface, no focus theft, works while covered.
- **ALL rebuild phases 2–7 shipped same day** (`b9d57f3`, +2,478 lines, builds
  clean 0 warnings): ActivityTracker port (stands down if the Python exe is
  running — never double-tracks), morning kickoff + evening review dialogs
  (reflections go to a new additive `reflections` table; kickoff/review/theme
  state in new `data/winui_state.json`), score v2 (floor −10, 3-day accrual
  cap, replan-all flat −10 — same `daily_score`/`overdue_accrual` ledger
  reasons + guards as Python so whichever app runs EOD first wins), Reports
  page, TickTick pull-only (reads the Python keyring token from Credential
  Manager, target `ticktick_access_token@MentorOverseer` — verified with a
  live API roundtrip), Plans page + 3-template Add Plan wizard, Settings
  (theme override). All pages screenshot-verified via `MENTOR_PAGE=<page>`
  env hook + PrintWindow. Keep SQLite schema, plan JSON, and config.json
  formats frozen — that's the Python↔C# contract (winui_state.json and
  reflections are additive, Python ignores them).
  Known deltas vs Python (deliberate): score days floor at −10 in C# only;
  overdue accrual caps at 3 days in C# only — don't run both apps' EOD long-
  term or scores drift; C# has no OAuth flow yet (connect via Python once);
  Schedule page still a stub (day-off/do-today/reschedule stay in Python for
  now); config editing (keywords, rates) still Python-side.
- Previous session: 2026-07-06, branch `ux-audit-fixes` (off `ui-theme-light-dark`)
- What was built: **UX/UI audit + all-12-findings remediation loop** (4 commits,
  `753cd50`..`2ed5c24`). Highlights:
  - Theme system hardened: new `hover`/`accent_amber`/`accent_purple`/
    `accent_blue_active` keys in `THEMES`; `CATEGORY_COLORS` is now per-theme
    (populated by `_apply_theme` like `C`); light `text_muted` darkened to
    `#6e7480` for WCAG contrast; ~13 in-app hardcoded dark-era hex colors
    replaced with theme keys (HTML export deliberately untouched — that page
    is fixed dark). Task rows now show the category *name* next to the dot.
  - Destructive-action safety: "Day off" asks for confirmation with exact
    task counts and has a full **Undo day off** (`_unmark_day_off` inverse
    shift + record delete); Archive asks first; Settings has a new
    "Archived plans" section with per-plan Restore.
  - Keyboard: Esc closes all 12 dialogs (`_finalize_dialog` helper — also
    centers every dialog over the main window); Ctrl+1/Ctrl+2/Ctrl+,/Ctrl+N
    global shortcuts; task rows are Tab-focusable with visible focus ring,
    Return opens detail, Space toggles done.
  - Nav: active view gets an accent left-bar + bold label (`_sb_item` grew
    `nav_key`; `self._active_view`); "Overdue" now scrolls the Today view to
    the first overdue section instead of duplicating "Today".
  - "Automatic" theme re-checks the OS setting on the 60s tick — live
    light/dark switching without restart.
  - Per-Monitor-v2 DPI awareness (fallback to the old system-aware call).
  - Empty states got CTA buttons (+ Add Plan / Connect TickTick); tooltips
    (`Hovertip` class) on ⚙, calendar </>, idle-row ✕, truncated plan names
    (now ellipsized via `_ellipsize`, not hard-cut).
  - **Two latent bugs found & fixed during the loop**: `tick_status` reset
    the list title to "Today" every 60s (clobbered the Weekly Report header;
    view checks now use `_active_view`, not the title string), and rebuilds
    could stack parallel 60s tick loops (now guarded with `after_cancel` via
    `_tick_after_id`).
  - Verified: 42/43 automated widget-level checks on an isolated copy (the
    1 "failure" was a withdrawn-root key-event artifact, re-verified green
    with a focused window) + in-process screenshots of light/dark/report.
  - NOT yet rebuilt into `MentorOverseer.exe` — run `build.bat` when ready.
- Previous session: 2026-07-05, branch `ui-theme-light-dark` (off `audit-remediation`
  at commit `4884eda` — kept separate per the user's request, not merged back yet)
- What was built:
  - **Light/Dark/Automatic theme system.** The `C` dict (was a static
    hardcoded dark palette, ~389 references across the file) is now populated
    from a new `THEMES = {"light": {...}, "dark": {...}}` dict at startup and
    whenever the user changes the appearance setting — every existing
    `C["some_key"]` reference keeps working unchanged since only the *values*
    swap, not the 389 call sites. Light (soft off-white `#f4f5fa`/white cards,
    indigo `#6366f1` accent, emerald/rose semantic colors) is the new default;
    dark keeps the same design language on a soft-charcoal `#111318` base.
  - `_detect_system_theme()` reads the Windows registry
    (`...\Themes\Personalize\AppsUseLightTheme`) for "Automatic"; defaults to
    light on any failure (non-Windows, key missing). `_resolve_theme(mode)`
    maps `"light"/"dark"/"auto"` → an actual theme name.
  - `_apply_theme(mode)` (new method) resolves the mode and does
    `C.clear(); C.update(THEMES[resolved])` in place.
  - `build_interface()` (main.py) is now idempotent — destroys its previous
    `self._main_container` before rebuilding, so a theme change can call it
    again for a **live** re-render with zero restart needed (leans on
    `render_tasks()`/`_rebuild_plans_sidebar()` already being destroy-and-
    rebuild internally).
  - New "APPEARANCE" section in `_show_settings_dialog` — 3 toggle buttons
    ("☀ Light" / "🌙 Dark" / "🖥 Automatic"), same mode-toggle idiom as the
    plan-generation wizard's mode picker. Saving with a changed theme calls
    `_apply_theme()` + `build_interface()` immediately.
  - New config key `appearance.theme_mode` (default `"light"`, not written to
    disk until first Settings save — `self.config.get("appearance", {})`
    degrades gracefully otherwise).
  - Found and fixed one real stale-callback edge case during testing: the
    sidebar's mousewheel-rebind (`self.root.after(500, self._bind_sidebar_wheel)`)
    could fire after a theme-triggered rebuild destroyed the old sidebar,
    raising a `TclError` trying to `.bind()` a dead widget — guarded with a
    `winfo_exists()` check.
  - Verified with a 22-check automated widget-level suite (theme resolution,
    default-light on fresh config, Settings toggle rendering/selection, live
    Dark switch + persistence + rebuild + continued interactivity, Automatic
    resolution, persisted-value read-back) — all passing, `data/mentor.log`
    stays empty throughout. One screenshot of the live light theme confirmed
    visually. (Two follow-up attempts to screenshot the dark theme
    misfired — see the "screen automation" lesson below — so dark/auto are
    verified via the automated color-value checks, not a visual capture.)
  - **Lesson learned, worth remembering**: do NOT use PowerShell to simulate
    real mouse input (`SetCursorPos`/`mouse_event`) or blindly screenshot "the
    active window" to inspect a Tkinter test app — it can act on the user's
    actual desktop/cursor instead of the intended process (confirmed twice:
    once actually clicked into a YouTube tab, once just mis-captured one).
    Prefer driving Tkinter widgets in-process (`widget.invoke()`, as the rest
    of this session's test harnesses already do) and only screenshot after
    positively confirming the target window's title via a read-only
    `Get-Process | Select MainWindowTitle` check immediately beforehand.
  - **Feedback from live-testing the theme**, fixed same pass:
    - Plan header ("{name} · Day X of Y") and "My Tasks (TickTick)" section
      header now render in plain `C["text"]` (grayscale) instead of the
      plan's accent color / `C["tt_badge"]` — the user found the indigo/purple
      too colorful for these two specific headers. `_render_section_header`
      itself is unchanged (still supports an accent color); only these 3
      call sites (main.py, plan header ×2 states + TickTick header) now pass
      `C["text"]`.
    - **TickTick sync made pull-only.** `_tt_autosync_cycle` no longer pushes
      plan tasks into TickTick or pushes completion status there — it now
      only reads personal tasks in (`get_all_tasks`), same as before, minus
      everything upstream of that. Removed as dead weight along with it:
      `get_tt_mapping`/`save_tt_mapping` (main.py), `_tt_complete_quietly`,
      and the push-on-checkbox-toggle branch in `_on_plan_toggle`. The old
      "Netherlands Plan" mirror TickTick project (from before this fix) is
      still looked up by name and excluded from "My Tasks" so previously-
      pushed duplicates don't resurface — but it's never created or written
      to again. `_on_tt_toggle` (completing a *pulled* personal TickTick task
      from inside the app) is intentionally untouched — that's still a
      legitimate one-directional action on data that originated in TickTick,
      not the app pushing its own plan data outward.
    - Investigated "today's TickTick task is missing" via a read-only
      diagnostic against the real account: confirmed the account genuinely
      has 0 personal tasks due today (134 of 180 open tasks have no due date
      at all, which the app intentionally excludes per an earlier session's
      explicit ask) — not a bug, just an accurate empty state.
    - This fix logically belongs on `audit-remediation` (unrelated to
      theming) but was applied directly on `ui-theme-light-dark` since that's
      where the user was actively testing — flagged as cherry-pick-able to
      `audit-remediation` separately if wanted before the theme branch merges.
- Previous session: 2026-07-04 (sixth pass same day)
- What was built:
  - **"✨ Generate with Claude" plan-generation wizard** — no live Anthropic API
    integration (deliberately cancelled: needs a paid API key separate from a
    claude.ai subscription); instead the app builds a filled-in prompt from 3
    fields, the user copies it to claude.ai manually, and pastes Claude's JSON
    reply back in to import it as a real plan. New sidebar button next to
    "+ Add Plan" opens `_show_generate_plan_dialog`, a 2-step wizard:
    - **Step 1** — a mode toggle ("🎯 Learn a skill" / "📌 Achieve a goal")
      backed by `PLAN_GEN_MODES`, plus 3 fields (subject, Claude's role, area
      of expertise) with per-mode captions/examples. "Generate prompt →" fills
      the active mode's template (`TEMPLATE_SKILL` / `TEMPLATE_GOAL`, module-
      level constants) via `str.replace` (not `.format`, since the templates
      contain literal JSON braces).
    - **Step 2** — the filled prompt in a read-only `Text` box with a
      clipboard "Copy prompt" button (same pattern as the TickTick redirect-URI
      copy button), and a second box to paste Claude's reply. "Import plan"
      extracts JSON directly or from a ```json fenced block via regex, then
      calls the new shared `_import_plan_dict()` (extracted out of the
      existing file-based `_add_plan_dialog` so both paths share one
      validation/save path).
    - Both prompt templates ask Claude to (a) write every task's `detail` as
      the concrete how-to and a separate `mentor_note` as the why-it-matters +
      common-mistake commentary — mentoring baked into the plan content itself,
      not a live back-and-forth chat — and (b) repeat the 4-point strategic
      briefing (80/20 high-leverage items, what to ignore, time-wasters to
      skip, realistic timeline) as a structured `briefing` object on the plan,
      not just prose in the chat reply.
    - New UI to surface the new fields: `_show_task_detail` shows a
      "💡 Mentor's note" block under the existing detail text; `_edit_task_
      dialog`/`_apply_task_edit` can edit it; a new "📋 Briefing" button (shown
      only if `plan.get("briefing")` is set) opens `_show_plan_briefing_dialog`,
      a read-only 4-section view of the briefing.
    - Added 8 generic `CATEGORY_COLORS` entries (`theory`/`practice`/`project`/
      `review` for skill plans, `research`/`decision`/`logistics`/`execution`
      for goal plans) alongside the existing Netherlands-specific ones.
  - **Third wizard mode: "📝 Format my own plan"** (`TEMPLATE_REFORMAT`) — for
    plans the user writes himself and just needs reformatted into the app's JSON
    schema. Step 1's field layout is now conditional per mode: `gen_fields23`
    (Claude's role + Area of expertise, used by skill/goal modes) and
    `reformat_fields` (a big "paste your plan" `Text` box) are two sibling
    frames under a shared `field1_block`, toggled via `pack()`/`pack_forget()`
    in `_set_mode` — `field1` itself is reused across all 3 modes (relabeled
    "Title for this plan" here). Unlike the other two templates, this one
    explicitly tells Claude not to invent new content — restructure only —
    and that `mentor_note`/`briefing` are optional, not required padding.
  - **Merged `code-refinement` into `audit-remediation`** (commit `fb38e65`,
    clean auto-merge) — brings the 5 audit-fix refactors and the calendar-
    picker reschedule dialog (built/tested separately earlier this session)
    together with the new plan-generation feature into one branch. The test
    worktree (`mentor-overseer-test/`, still on `code-refinement`) was fast-
    forwarded to match and its 4 local-only isolation tweaks (test mutex
    name, window title, disabled registry writes) re-applied on top.
- Previous session: 2026-07-04 (fifth pass same day)
- What was built:
  - **Project cleanup** — removed everything unused/stale from the repo root:
    build cruft not worth tracking in the first place (`dist/`, `build/`,
    `__pycache__/`, the old pre-rename `NetherlandsMentor.exe`, the auto-
    regenerated `MentorOverseer.spec`) and, with the user's confirmation, content
    that had been deliberately committed earlier but was fully superseded:
    `icon_options/` (93 icon design candidates) + the 4 `gen_*.py` scripts that
    produced them, unused icon variants (`icon_round.ico`, `mentor-overseer.ico`,
    `icon_source.png` — only `icon.ico` is actually referenced by `build.bat`),
    and `plan/roadmap.json` + `plan/Claude_Code_Mastery_Guide.md` (the roadmap
    migration source is fully superseded by `plans/active/netherlands.json`;
    the guide was an unrelated personal doc parked in that folder). All still
    recoverable from git history if ever needed. Updated the architecture
    diagram above to match current reality (also dropped the long-stale
    `run.bat` reference, which never existed on disk).
- Previous session: 2026-07-04 (fourth pass same day)
- What was built:
  - **"My Tasks (TickTick)" now shows only tasks due today**, not every open
    task across all personal projects. New `_tt_task_local_date(tt_task)`
    static helper parses `dueDate` and converts to this machine's local
    timezone before extracting the date — necessary because TickTick returns
    all-day due dates as local midnight *converted to UTC*, e.g. a task due
    04.07 in CEST (UTC+2) comes back as `"2026-07-03T22:00:00.000+0000"`.
    Slicing the raw string (`[:10]`) would have silently read that as due on
    the 3rd, off by one day, and hidden every genuinely-due-today all-day
    task. `_tt_on_autosynced` now filters on `_tt_task_local_date(t) ==
    date.today()` in addition to the existing open-status check. Tasks with no
    due date at all no longer show up here either — matches the user's ask
    literally ("only the task for today").
  - Verified live against the real account: 181 open personal tasks across 7
    projects narrowed to exactly 1 due-today task, cross-checked against an
    independent manual recount using the same timezone conversion. Confirmed
    the run created no new duplicate TickTick tasks (today's plan task was
    already mapped from the previous session's push).
- Previous session: 2026-07-04 (third pass same day)
- What was built:
  - **Root-caused why TickTick never actually synced.** Every `_tt_autosync_
    cycle` run (push + pull + completion sync, every 5 min) crashed on its very
    first `self.conn` access — `get_tt_mapping`/`save_tt_mapping`/raw SQL calls
    all used the *main-thread* connection from inside the background autosync
    thread, raising `sqlite3.ProgrammingError: SQLite objects created in a
    thread can only be used in that same thread`. This has been broken since
    the reliability audit removed `check_same_thread=False` from `self.conn`
    (correctly, for the main thread) without noticing autosync also runs off-
    thread. The exception was swallowed by `_run()`'s own except-block and only
    ever surfaced as a sidebar status string nobody was watching — so this was
    the *real* reason "My Tasks" was empty; the earlier same-day fix (querying
    every project instead of just the plan's own) was correct but never got a
    chance to run because the push loop died before reaching it. Fixed by
    giving `_tt_autosync_cycle` its own `sqlite3.connect(DB_PATH)` inside the
    thread (same pattern as the tracker's poll thread from the original audit)
    and adding an optional `conn=` param to `get_tt_mapping`/`save_tt_mapping`.
    Verified live end-to-end, unmocked: the real threaded autosync now
    completes ("TickTick: connected · synced HH:MM"), pulled 181 open personal
    tasks, and — as a real, correct side effect of finally working — pushed one
    genuinely new task ("Rewrite all job experience bullets") to the user's real
    "Netherlands Plan" TickTick project during the test. Flagged to the user since
    it'll appear in his TickTick app without him having clicked anything.
  - **Day-off is now visible.** New `plan_days_off` table (`plan_id`, `day`)
    written by `_mark_day_off`; Schedule dialog's day list now includes marked-
    off days even though they have zero tasks, rendered as "Day N · DAY OFF" in
    purple, with the "Day off" button hidden for days already marked (can't
    double-apply). Previously the day just vanished from the dialog once its
    tasks were shifted away, with nothing indicating it had been marked off.
  - Both re-verified with the existing task-edit/day-off/TickTick and reschedule/
    overdue-accrual test harnesses from earlier today — all 40 checks across
    three isolated-copy runs still pass, nothing regressed.
- Previous session: 2026-07-04 (second pass same day)
- What was built:
  - **Overdue tasks now cost score every day they stay undone, and can be
    rescheduled to a specific date.** Each overdue task's row in the main Today
    list has a "Reschedule" button (`_reschedule_task_dialog`) asking for a
    DD.MM.YYYY date (same input convention as `_ask_start_date_for`); it moves
    just that one task via `_save_override` — no cascading shift like "Do
    today"/"Day off". New `_credit_overdue_accrual_if_missing(d)` adds a further
    `overdue_accrual` score_ledger deduction (`task_overdue_penalty` rate) for
    every task still overdue on calendar date `d`, once per date (guarded like
    `_credit_day_score_if_missing`) — wired into both `fire_eod_summary` and the
    7-day startup catch-up in `_ensure_score_caught_up`. The one-time "day-of"
    miss already folded into that day's `daily_score` is untouched; this is
    strictly additional. Rescheduling doesn't refund anything already deducted
    (by design, per the user).
  - Verified end-to-end against an isolated copy of the app (same harness as
    the task-edit/day-off/TickTick session earlier today): drove the real
    reschedule flow (with `simpledialog.askstring`/`messagebox.showerror`
    monkeypatched to feed answers and capture validation errors — confirmed it
    rejects a bad date format and an invalid calendar date like Feb 31, then
    correctly moves the task off its overdue day onto the exact chosen date's
    plan-day), confirmed the accrual ledger entry amount matches an independent
    manual recount of overdue tasks and is idempotent per calendar date, and
    ran `fire_eod_summary` for real.
- Previous session: 2026-07-04 (first pass same day)
- What was built:
  - **Task editing**: detail panel now has an "Edit" button (`_edit_task_dialog`)
    for title/detail/duration/category. Saving (`_apply_task_edit`) rewrites the
    task in-place in `plans/active/<id>.json` and, if the title changed, migrates
    `task_completions`/`task_overrides`/`ticktick_sync` rows from the old title
    to the new one (these tables key on task text) so completion history and the
    TickTick mapping stay attached.
  - **Non-working days**: Schedule dialog now has a "Day off" button on every day
    header (`_mark_day_off`) — pushes that day's tasks, and everything assigned
    on/after it, forward by one day via the existing `task_overrides` mechanism
    (same mechanism as "Do today", just applied to the whole tail instead of one
    task). Dialog refreshes in place after each click instead of closing, so
    several consecutive days (e.g. a week off) can be marked in one sitting.
  - **Bug found + fixed: TickTick tasks not showing up.** "My Tasks (TickTick)"
    was pulling from `self.tt_project_id` — the "Netherlands Plan" project the
    app itself creates to push plan tasks into — never the user's actual personal
    TickTick lists. Confirmed live against his account: real tasks live in 7
    other projects (STUDY, NOTES, HOME, "My 2026 Goals", etc.), none of which
    were ever queried. Added `TickTickClient.get_all_tasks(exclude_project_id)`
    in `ticktick/sync.py` (loops `get_projects()`, pulls each project's tasks,
    tags each with `projectId`/`_projectName`); `_tt_autosync_cycle` now uses it
    (excluding the plan's own mirror project) to populate `self.tt_tasks`.
    Completing a "My Tasks" row now routes to the task's own `projectId`
    instead of always assuming the plan project (`_on_tt_toggle` signature
    updated to carry it through).
  - All three verified by driving the actual widgets (not reimplemented mocks):
    instantiated `MentorApp` against an isolated copy of config/plans/db, with
    autostart/tray/tracker/TickTick-autosync side effects monkeypatched out,
    then typed into the real Edit-Task Entry/Text widgets and invoked the real
    Save button, invoked the real "Day off" flow and checked the resulting
    `plan_day` shift, and called the real `get_all_tasks` live (read-only) against
    the user's actual TickTick account.
- Previous session: 2026-07-02
- What was built:
  - Bug: single-instance mutex guard (`_acquire_instance_mutex`) existed but silently
    let duplicate `MentorOverseer.exe` instances run — `windll.kernel32.GetLastError()`
    is unreliable in ctypes without `use_last_error=True`, so the "already running"
    check could read a stale/zero error code. Fixed by using `WinDLL(...,
    use_last_error=True)` + `ctypes.get_last_error()`. Verified with 20 truly-parallel
    launches (raw ctypes) and repeated exe-launch stress tests — exactly one instance
    survives every time now.
  - Bug: `self.today` was computed once at `__init__` and never refreshed — since this
    app runs continuously in the tray, staying open past midnight silently used
    yesterday's date for all "today" calculations. Converted to a `@property` that
    always reads `date.today()`.
  - TickTick reconnect flow fixes: `on_connect_ticktick` skipped the credentials dialog
    whenever *any* client_id/secret were already saved, even if stale/rotated — no way
    back in to fix bad credentials. `_tt_on_error` now reopens the setup dialog (and
    actually retries the connection after saving — the dialog's "Save & Connect" button
    previously only saved). `_exchange_code` in `ticktick/sync.py` now surfaces
    TickTick's real OAuth error text instead of a bare HTTP status line. OAuth wait
    timeout cut from 120s to 45s with a clearer status message, since a hard rejection
    at TickTick's `/authorize` (bad client_id, unregistered redirect_uri) never calls
    back to the local server at all — previously that looked like the app was hung.
  - TickTick sidebar simplified: removed the separate status line and the "Push
    Today"/"Sync Completions" buttons. Now just "Connect TickTick" → "TickTick
    connected". Once connected, `_tt_autosync_cycle()` pushes today's plan tasks and
    syncs completions both ways automatically every 5 min (`TT_AUTOSYNC_INTERVAL_MS`),
    no manual action needed.
  - Bug: `_tt_autosync_cycle`'s error handler referenced `exc` (the `except ... as exc`
    variable) inside a `lambda` passed to `root.after()` — Python unbinds `exc` when
    the except block exits, so by the time the deferred lambda ran it raised
    `NameError: cannot access free variable 'exc'` on every failed sync. Same bug fixed
    in `_on_tt_toggle`. Both now capture the message as a plain string before the
    closure.
  - **Settings moved into the app**: new ⚙ button in the sidebar header opens a
    scrollable Settings dialog (working hours, reminders, scoring rates, score economy
    rates, activity keyword lists, idle-answer library — see below). Saving writes
    `config.json` and restarts `ActivityTracker` so changes take effect immediately
    (it only reads config at construction, doesn't poll it live).
  - Found `config.json`'s `scoring` block was dead — `_week_stats()` had the formula
    hardcoded (10/-5/3/-2, no streak bonus despite `streak_bonus_per_day` existing in
    config). Extracted `_day_score()`/`_day_task_counts()`/`_day_diary_minutes()`
    helpers that actually read `config["scoring"]`, and added a real streak bonus
    (`_current_streak()`) applied to today's score only.
  - **Score monetization**: new `score_ledger` table (persistent running balance,
    never resets). Credited once/day via `fire_eod_summary()` (`_credit_day_score_if_
    missing`), with a 7-day startup catch-up (`_ensure_score_caught_up`) for days the
    app wasn't open at EOD — bounded window, doesn't retroactively credit the whole
    plan history. New sidebar SCORE section: balance + "Buy entertainment time" (spend
    points for a timed window; off-plan activity during it is tracked under a new
    `paid` diary category — excluded from the score penalty and from the off-plan
    nagging alerts — via `ActivityTracker.set_paid_until`/`_effective_class`) + "Log
    spend (no regrets)" (manual expenditure logging, deducts points). Both dialogs
    block purchases exceeding the current balance. Rates (`points_per_minute`,
    `points_per_currency_unit`, `currency_symbol`) live in `config["score"]`, editable
    via Settings.
  - Bug found + fixed while building the above: the sidebar had no scrollbar, and
    adding the SCORE section pushed content below the visible window at the default
    1060x720 size with no way to reach it. Wrapped the whole sidebar in a scrollable
    canvas (mousewheel + drag), matching the pattern already used for the Schedule/
    Settings dialogs. Re-binds mousewheel after the two sections that rebuild their
    own children (`_rebuild_plans_sidebar`, `_refresh_score_sidebar`).
  - **Idle-time activity library**: new `config["idle_activity_rules"]` block
    (on_plan/off_plan/neutral phrase lists, seeded empty — the user fills it in via the
    new Settings section). `ActivityTracker.classify_idle_text()` substring-matches a
    typed idle-dialog answer against it (same mechanism as the existing window-title
    `classify()`); `log_idle_answer()` now stores the matched category instead of
    always hardcoding `'idle'`. Diary rendering (live view + HTML export) updated to
    show the typed description for *any* category, not just `idle` — needed because a
    reclassified row still has `window='idle'` literally, which would otherwise render
    as the unhelpful literal text "idle" instead of what was typed.
  - All changes verified via `python main.py` dev-mode screenshots (sidebar layout,
    Settings dialog, live purchase flow end-to-end against the real DB, reverted after)
    plus a rebuilt/relaunched exe with a clean `mentor.log` after each fix.
- Previous session: 2026-07-01
- What was built (2026-07-01):
  - Security/reliability audit (2026-06-29) — all High/Medium findings fixed on branch
    `audit-remediation` (keyring credentials, autostart toggle, SQLite thread isolation,
    HTML escaping, OAuth CSRF state, parametrized SQL). See git log for detail.
  - Feature work (committed 2026-07-01): idle/sleep dialog now fires on return (reports
    actual elapsed duration) instead of the moment idle starts; messenger window-title
    badge stripping; Schedule dialog to pull a future task to today (shifts intervening
    tasks forward a day); startup gap check; diary entry edit/delete UI.
  - Bug fix (2026-07-01): Today/Report task list canvas could render blank — it never
    reset scroll position when switching views, so returning to Today from a taller
    view (Weekly Report, or after the Schedule dialog) left the view scrolled past the
    short amount of real content. Fixed by resetting `canvas.yview_moveto(0)` on view
    switch. Also fixed the Schedule dialog's mousewheel binding, which used `bind_all`/
    `unbind_all` and was clobbering the main window's global scroll binding.
  - Reliability: added `root.report_callback_exception` so exceptions raised inside
    Tkinter callbacks (button commands, binds, `after()`) are logged to
    `data/mentor.log` — previously only `sys.excepthook` was wired up, which does NOT
    catch Tkinter callback exceptions, so UI crashes were silently swallowed with zero
    trace. `render_tasks()` also now has a try/except safety net that shows a "Retry"
    button instead of leaving the pane blank.
  - Schema fix: `task_completions` carried a legacy inline `UNIQUE(plan_day, task_text)`
    constraint from before multi-plan support (global across all plans, not caught by
    the `ON CONFLICT` clause in `save_completion`). `ensure_data_store()` now detects
    and rebuilds the table without it, preserving data.
  - Telegram grouping fix (2026-07-01): "Time By App" reports were splitting the same
    Telegram chat between a standalone top-level entry and a nested Telegram sub-item,
    and some Telegram sub-names retained leftover unread-badge digits (e.g.
    "Telegram (1027)"). Root causes, confirmed against a week of real `time_diary` data:
    - `_active_window_title()` re-resolved the owning exe via `OpenProcess` on every
      poll; when that WinAPI call failed (happened often for Telegram), the title was
      stored with no "– Telegram" suffix and got misfiled as a standalone app. Fixed
      with a sticky per-PID cache (`_pid_app_cache` in tracker/activity.py) so one
      successful resolution covers the rest of that process's session.
    - `_strip_unread_badge()` required pure ASCII digits inside the parens, so a
      locale-formatted thousands separator (`1,027`) or a second trailing paren group
      (`(525) (2)`) left the badge stuck. Now strips iteratively and tolerates common
      separator characters.
    - `_poll_once()` was re-stripping the Unicode LTR mark *after*
      `_active_window_title()` already returned, without also stripping the badge that
      went with it — this silently broke the report's older LTR-mark-based Telegram
      fallback whenever exe resolution failed. Removed the redundant re-normalisation.
    - `main.py`'s `_get_app_sub()` now re-applies badge stripping at report-render time
      too, so already-logged rows with a stuck badge display correctly with no DB
      migration needed. Chats logged with zero identifying signal at all (bare name,
      no marker — e.g. old data predating any of this session's fixes) can't be
      retroactively regrouped; only newly-logged sessions benefit going forward.
  - Security: found `ticktick.client_secret` / `access_token` committed in plaintext in
    git history (baseline commit, predating the keyring migration). No remote was
    configured, so exposure was local-only. Rewrote git history on both `master` and
    `audit-remediation` with `git filter-branch` to redact the values, then purged all
    backup refs/reflog and ran `git gc --prune=now --aggressive` — verified via a full
    object scan that the secret no longer exists anywhere in the repo. Commit hashes on
    both branches changed as a result. the user still needs to rotate the TickTick OAuth
    credential at developer.ticktick.com as a precaution (old values are unrecoverable
    now, but treat them as burned).
- What to build next: nothing specified — await the user's direction. Idle-answer
  library (`config["idle_activity_rules"]`) is seeded empty; the user needs to populate
  it via Settings for idle-time reclassification to do anything.
- Known issues / notes:
  - TickTick redirect URI must be registered at developer.ticktick.com: `http://localhost:8765/callback`
    — separate from the "App Service URL" field on that page, which is a different
    setting; the redirect URI specifically goes in the "OAuth redirect URL" field.
  - the user's TickTick client_id/secret were visible in a screenshot shared this session
    (2026-07-02) — same "treat as burned, rotate when convenient" situation as the
    git-history leak from 2026-06-29.
  - `tracker/__init__.py` / `ticktick/__init__.py` were missing from git for a while
    (package worked locally without them tracked) — now added.
  - `run.bat` mentioned in the architecture diagram above does not actually exist on
    disk — use `python main.py` directly for dev runs, or `build.bat` for a full
    PyInstaller rebuild (kills any running `MentorOverseer.exe` first).
  - Historical Telegram activity data from before 2026-07-01 may still show some chats
    as standalone top-level apps instead of nested under Telegram (see Telegram
    grouping fix above) — this is expected and won't self-correct for old rows.
  - `config["notifications"]`, `config["user_name"]`, and `config["plan_start_date"]`
    are dead — not read anywhere in main.py. Deliberately left out of the new Settings
    dialog rather than exposing controls that do nothing; flagged here in case the user
    wants them wired up or removed later.
  - Restarting `ActivityTracker` (Settings save, or anywhere else that calls
    `self.tracker.stop()` + `init_tracker()`) leaves the old poll thread running for
    up to ~60s until its current sleep cycle ends, briefly overlapping with the new
    one. Both hold independent SQLite connections so this is harmless, just noting it
    exists rather than treating an overlap as new gremlin behavior if it comes up.
- Python: `C:\Users\devba\AppData\Local\Programs\Python\Python311\python.exe`
- Exe: `MentorOverseer.exe` (next to config.json / plans/ / data/) — rebuild with
  `build.bat` after any source change; the compiled exe does NOT auto-update.
- Desktop shortcut: `C:\Users\devba\Desktop\Mentor-Overseer.lnk`

## Full-toolset audit + remediation (2026-07-07)

Full 5-pass audit of BOTH apps + coexistence/tester/releaser/launch-readiness lenses.
13 findings; everything fixable was fixed, built, and deployed the same day:

- **Scoring unified to v2** ("balanced coach", approved 2026-07-06): main.py now has
  `DAILY_SCORE_FLOOR = -10` and `OVERDUE_ACCRUAL_CAP_DAYS = 3`, matching
  ScoreService.cs. The shared score_ledger is no longer written with two formulas.
- **DB net under the score guards:** partial UNIQUE index `sl_reason_date` on
  score_ledger(reason, date) for daily_score/overdue_accrual, created by both apps;
  both catch the constraint hit (Python IntegrityError / C# SqliteErrorCode 19) as
  "other app credited first". Verified on a scratch DB and present on the live DB.
- **WinUI pause guard now probes the Python app's mutex** (`MentorOverseerSingleInstance_v1`)
  instead of only matching the frozen exe's process name — dev-mode `python main.py`
  no longer double-tracks. Probe verified cross-process.
- **Retention:** activity_log pruned to 90 days, time_diary to 365, VACUUM after
  pruning; skips tables that don't exist yet (fresh-DB first launch — caught by test).
- mentor.log now `encoding="utf-8"`; stale `NetherlandsMentor` autostart value deleted
  live AND cleaned by `_set_startup_key` forever; HTML report hints now escaped;
  keyring migration only strips config secrets after read-back verification; OAuth
  callback page tells the truth on error/CSRF and ignores stray requests.
- **WinUI v0.2.0 Release build** produced; desktop shortcut "Mentor Overseer (New
  Design).lnk" now points at bin\x64\Release (was Debug).

**Still owed (the user's side):** rotate the TickTick client secret — commit eb22f8c
(pushed, on origin/master) contains the old client_secret + access_token in
config.json. Private repo, but rotation at the TickTick developer console is the
real fix. Optional afterwards: history rewrite + force-push (needs the user's go-ahead).
**Known accepted gaps:** no test suite for this repo yet (tester skill unapplied),
nothing code-signed, migration-limbo between the two apps (primary app undecided).

## One-app consolidation (2026-07-07, the user's call: "all functionality in one place")

The WinUI app is now THE app; the Python app is retired (kept in the repo +
MentorOverseer.exe still on disk as fallback, no autostart, old desktop shortcut
removed; "Mentor Overseer.lnk" now points at the WinUI Release build). Ported in
this session, in the user's chosen order (highest-risk first):

- **OAuth (Phase A):** CredentialStore (Credential Manager read/WRITE, python-keyring
  byte format), TickTickAuth (TcpListener loopback callback on 8765, CSRF state,
  code exchange, refresh), 401 auto-refresh in TickTickService, Connect/Reconnect
  dialog (Settings + Today's TickTick section). Verified live: reads the token the
  Python app stored.
- **Tray (Phase B):** H.NotifyIcon.WinUI; close hides to tray (tracking continues),
  tray menu Open/Quit, --minimized boots to tray, Start-with-Windows toggle in
  Settings (StartupService sweeps legacy Run values). NOTE: autostart NOT yet
  registered — the user flips the toggle (auto-mode reviewer required his explicit call).
- **Plan import (Phase C):** AddPlanDialog "Load from file" (.docx via zip XML
  extraction, .txt/.md into reformat prompt, .json straight to import).
- **Settings (Phase D):** full editing — working hours, day-review time,
  grace/repeat/idle minutes, activity keywords, idle-answer library → config.json
  via ConfigService.Mutate + tracker restart.
- **Export (Phase E):** ReportExport.ExportWeek → data/report.html (escaped,
  dark-mode aware), button on Reports page.

Python app changes on retirement: none — it still works if launched manually.

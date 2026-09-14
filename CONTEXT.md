# Planillium — Project Context

> Handoff document. Read in full at session start. Records what was decided, what it cost to work
> out, and what must not be redone. `CLAUDE.md` is *how to work here*; this file is *what is true
> here*. The **settled business rules and standing lessons** live next door in `DECISIONS.md` — a
> lookup register, not a read-through, unaffected by the split below.
>
> **Compaction threshold: 400 lines** — `wc -l`, not PowerShell's `Measure-Object -Line` (it skips
> blank lines and under-reported this file by ~60). **Migrated 2026-09-14** to the split scheme (see
> `~/.claude/CLAUDE.md` → Keeping knowledge current): the schema/format reference and the full
> session-log history moved verbatim to `context/*.md` (never compacted, no size ceiling); this
> file stays thin — current state, rules, a short decisions-highlights list, open items with dates,
> and a Section Index. `DECISIONS.md` was **not** absorbed into `context/` — it's a mature,
> independent register, pre-dating this scheme (split out 2026-08-04 for the identical reason),
> and moving it would mean auditing every cross-reference for no benefit. Last updated 14 Sep 2026.

---

## 1. What this is

A desktop personal mentor and accountability companion that tracks the user's progress across up
to 3 active life/career plans simultaneously. It monitors his activity, keeps him on-plan, logs
his full day (06:00–20:00), and generates weekly reports.

**The user:** Location Milan, Italy (moving to Utrecht/Eindhoven, NL). Goal: land a Dutch Digital
Transformation Manager role → HSM visa → EU citizenship. Key tools: Power BI, Power Platform,
SharePoint, MBA from Bologna. Active plans: Netherlands Relocation, The Complete 10-Level Claude
Code Mastery Guide (both under `plans/active/`).

Display name, internal rename history and the three legacy-compat exceptions:
`context/domain.md` — "Display name and internal rename history".

## 2. Current state

Phase: **shipped, in daily live use** (v1.2.0+, public GitHub repo). **The WinUI app
(`winui/Planillium.App`) is THE app** — full architecture, directory map and tech stack:
`context/domain.md` — "App architecture" and "Tech stack". Automated test suite: 152/152 passing
as of the last recorded run (2026-09-04) — re-run before trusting that number stale.

Data formats and reference: `context/domain.md` — "Plan JSON format", "Database schema", "config.json
key fields".

## 3. Where everything lives

Directory map: `context/domain.md` — "App architecture". Quick orientation: `CONTEXT.md` (this
file) · `CLAUDE.md` how to work here · `DECISIONS.md` business rules + standing lessons ·
`context/` domain reference + session/todo history, never compacted · `config.json` shared
settings (no secrets) · `plans/{active,queued,archive}/` · `data/progress.db` the live SQLite DB ·
`release/` Inno Setup pipeline · `winui/Planillium.App/` the WinUI 3 / .NET 8 source.

## 4. Section index

- `DECISIONS.md` — the 13 numbered business rules with full rationale (working hours as the diary
  window · plan-day arithmetic · overdue accrual · the one-task-per-day steady state and why
  Reschedule/Day-off and Move-to-today deliberately differ · archiving · the score floor and
  bonuses · day-off scoring · queued plan ideas · `DriftDays` · the progress-based "Day X of Y"
  counter) plus **Standing lessons** (verification discipline, safety around real data including
  the scratch-`MENTOR_ROOT` technique, WinUI layout traps, prompt/timer/state rules, design
  lessons) — read the relevant rule before changing anything it governs; several record a decision
  the user made after Claude argued the opposite.
- `context/domain.md` — architecture/schema reference, headers by subject: display name and
  internal rename history · app architecture (+ directory map) · tech stack · Plan JSON format ·
  database schema · config.json key fields.
- `context/todos.md` — resolved work only. Done: pre-WinUI Python era (retired) · the full session
  log, 07-07 through 09-04, dated · resolved-and-closed one-line pointers. Decided not to
  do/settled without action: the 42 overlapping `time_diary` pairs · `ActivateQueuedPlan`'s
  non-atomic write · the memory footprint.

## 5. Rules that are not negotiable

- **`data/progress.db` is the user's real, live data — not a fixture.** Any direct write to it
  outside the app's own code requires the user's explicit confirmation **naming the specific table
  and change** before running it — the harness's auto-mode classifier enforces this and rejects a
  vague "yes, go ahead." Never simulate clicks/keystrokes that would mutate real plan/score data;
  verify that logic by code inspection plus a clean build instead. Full detail (including the
  scratch-`MENTOR_ROOT` technique for exercising the UI safely): `DECISIONS.md` — Standing lessons,
  "Safety around real data".
- **The repo is public** (`devbaghda/planillium`, flipped 2026-07-21). Never add secrets or
  personal data — there is a live public-exposure risk if something slips through. `.gitignore`
  excludes `data/`, `config.json`, and `plans/active/*.json`.
- **Check every sibling** before calling a fix complete — grep for the shape, not just the
  reported call site. Origin story and what it produced as a global rule: `CLAUDE.md` →
  "Regression-prevention lesson".

## 6. Key decisions and standing lessons — highlights

Short pointers kept inline (full text in `DECISIONS.md`):

- **One pair of working hours governs both the off-plan nag and the diary tracking window** —
  merging a second `diary_hours` block back into one was a deliberate 2026-08-04 simplification;
  a `diary_hours` block in an existing config.json is now inert.
- **One task per day is the steady state, but Reschedule/Day-off and Move-to-today deliberately
  shift differently** — the former is "insert, don't overlap," the latter transiently doubles up
  on purpose. Both now gap-close (2026-08-05), with one exception: an overdue task's own past day
  is never compacted.
- **"Day X of Y" is a progress counter, not a calendar counter** (`Plan.ProgressDay`) — display
  only; every scheduling/overdue/scoring decision uses the separate calendar-pure `Plan.PlanDay`.
- **`dotnet test -c Release` was silently hitting the real DB** (2026-08-13) — 154 bad rows
  deleted after backup+confirmation; fixed via a `PLANILLIUM_TESTS` define, not a `#if DEBUG` gate
  (those compile out under Release).
- **A settings page split across independent sections must save each section on its own success**
  — one invalid reminder field used to silently discard an already-valid working-hours edit.

## 7. Still open

1. **(opened 2026-09-01) `FlashContentRefresh` COMException on wake.** Caught, harmless so far,
   cause not investigated — possibly two queued ticks firing close together after timer suspension
   during sleep. *Rec:* revisit if a visible glitch or a less-harmless failure accompanies it.
2. **(opened 2026-09-01) TickTick widget disappearance — still inconclusive.** Checked every
   TickTick-touching file/Win32 call; Planillium has no shared state with the widget's login. A
   temporary diagnostic (`Log.Info` in `TickTickService.cs`/`TickTickAuth.cs`, marked "temporary,
   remove together") is still live-deployed. *Rec:* waiting on the user to report a timestamp the
   widget next disappears at, to compare against the diagnostic log. Once confirmed or ruled out,
   remove the three temporary calls and rebuild Release.
3. **(opened 2026-08-14) How the 08-14 archive move happened is unconfirmed** — an archived plan
   at 11/23 tasks (short of the 100% Archive requirement) turned up back in active; moved back, DB
   intact. *Rec:* watch for a recurrence.
4. **(opened 2026-08-13) The 08:00–11:28 gap on 08-13 never produced a diary row — cause
   unconfirmed** (ruled out as the test-data cleanup). *Rec:* revisit if it recurs.
5. **(opened 2026-08-07, still open as of 08-28) Not yet live-UIA-verified** (clean build + tests
   only): 08-13's narrowed diary columns/Auto scrollbar and Reports DayOffs figure; 08-07's
   `IdleReturnDialog` Category/Tag fields + Mark-tag toolbar row; 08-17's Schedule collapsible
   cards + `EditDiaryEntryDialog` quick-pick chips.
6. **(opened 2026-08-05) Diary midnight rollover never observed actually happening** — needs the
   clock to cross midnight with the app sitting on Reports. *Rec:* if Diary still shows yesterday
   some morning, check the assignment at the top of `BuildDiarySection` first.
7. **(opened 2026-09-04) Insights "this week" fix not yet live-verified by eye** across all four
   period tabs (today/week/month/year) — fix is in, tests pass, Release rebuilt and relaunched;
   pending user confirmation on screen.
8. **(standing, not a bug) Scoring Settings' 12 inputs are live-checked (present/reachable at both
   window extremes) but their values are never edited live** — that writes `config.json` and
   restarts the tracker, so verification stays code-inspection-only by design. Revisit only if
   this verification approach changes.

## 8. Next steps

No standing backlog beyond §7 — the app is shipped and in daily use, not mid-build. Take §7 in the
order written if picking up idle work; otherwise this file's job is to be current when the next
real bug report or feature request arrives.

## 9. Environment notes

- TickTick redirect URI must be registered at developer.ticktick.com as
  `http://localhost:8765/callback` in the **OAuth redirect URL** field (not "App Service URL").
- Build/verify commands, the live-vs-Debug-exe check, and UI-Automation verification technique are
  in `CLAUDE.md` (this project's own, not the machine-wide one) — don't duplicate them here.
- Machine-wide notes (`py` not `python`, cp1252 console) are in `~/.claude/CLAUDE.md`.

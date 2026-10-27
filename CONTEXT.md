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
> and moving it would mean auditing every cross-reference for no benefit. Last updated 13 Oct 2026.

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
(`regular/winui/Planillium.App`) is THE app** — full architecture, directory map and tech stack:
`context/domain.md` — "App architecture" and "Tech stack". Automated test suite: 169/169 as of
2026-10-01 (agentic side 243), stable at any time of day — `ActivityTrackerPendingGapTests`' former
time-of-day-dependent flake (previously time-of-day-dependent within ~3h of midnight) is fixed,
not just unlucky-not-to-hit; see `context/todos.md` Session log 09-22.

Data formats and reference: `context/domain.md` — "Plan JSON format", "Database schema", "config.json
key fields".

## 3. Where everything lives

Directory map: `context/domain.md` — "App architecture". Quick orientation: `CONTEXT.md` (this
file) · `CLAUDE.md` how to work here · `DECISIONS.md` business rules + standing lessons ·
`context/` domain reference + session/todo history, never compacted · `config.json` shared
settings (no secrets) · `plans/{active,queued,archive}/` · `data/progress.db` the live SQLite DB ·
`release/` Inno Setup pipeline · `regular/winui/Planillium.App/` the WinUI 3 / .NET 8 source.
**Layout (restructured 2026-10-11):** project root holds shared things (`data/`, `config.json`,
`plans/`, `release/`, `media/`, `context/`, docs); `regular/` is the regular-workflow build
(`winui/`, `pilot-specs/`); `agentic/` is the agentic twin (`winui-agentic/`).

## 4. Section index

- `DECISIONS.md` — the 14 numbered business rules with full rationale (working hours as the diary
  window · plan-day arithmetic · overdue accrual · the one-task-per-day steady state and why
  Reschedule/Day-off and Move-to-today deliberately differ · archiving · the score floor and
  bonuses · day-off scoring · queued plan ideas · `DriftDays` · the progress-based "Day X of Y"
  counter · the lost/gained-earnings counter's daily-rate/backdated-start/non-retroactive-sign
  rules) plus **Standing lessons** (verification discipline, safety around real data including
  the scratch-`MENTOR_ROOT` technique, WinUI layout traps, prompt/timer/state rules, design
  lessons, agentic-pilot isolation) — read the relevant rule before changing anything it governs;
  several record a decision
  the user made after Claude argued the opposite. Includes the 2026-09-24 rule that a
  regular-workflow build must write its own Dashboard row on close-out, not leave it to the
  agentic side or a later recall pass.
- **A reported sidebar-balance swing is very often not a scoring bug.** Two standing causes to
  check first: `EnsureScoreCaughtUp` can post a large one-shot bulk credit for backlogged
  `daily_score` days at any startup (`DECISIONS.md` rule 9a); the chip is a plain `SUM(delta)` and
  reads negative during a heavy `replan_overdue`/`overdue_accrual` stretch, which a quick glance
  can easily misread with the minus sign dropped (2026-09-16 session, `context/todos.md`).
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
- **Two parallel workflows, fully separate documents (default from 2026-10-01).** Every feature
  runs both the regular build (this folder's docs) and the agentic pipeline (`agentic/winui-agentic/`, with
  its own `CONTEXT.md`/`DECISIONS.md`/`context/`). Method, rules and metrics:
  `~/.claude/reference/feature-workflow.md`. Never write one side's implementation notes into the
  other's docs; user decisions are recorded into each side's docs individually by the orchestrator.
  *(Supersedes the 2026-09-22 "pilot write-ups never go in CONTEXT.md" rule: the leak it guarded
  against is now impossible because the agentic side no longer reads this file.)*
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

One line per item; full detail and history in `context/open-items.md` (grep the item number or date).

- **0 (2026-10-01)** Eight-item batch (pie, diary filters, Top Distractions, away dialog) + payable tags / salary default — regular visuals OK'd, agentic pie legend was failing; payable-tags code was left uncommitted at the time.
- **0a (2026-10-09)** Replace remaining tasks + Plans-card layout — built, QA'd, pushed 44f6f19; live checks owed in both apps.
- **1 (2026-09-01)** `FlashContentRefresh` COMException on wake — caught, harmless; revisit if a visible glitch accompanies it.
- **2 (2026-08-14)** An archived plan at 11/23 tasks turned up in active — cause unconfirmed; watch.
- **3 (2026-08-13)** 08:00–11:28 diary gap on 08-13 — cause unconfirmed; revisit if it recurs.
- **4 (2026-08-07)** `IdleReturnDialog` / `EditDiaryEntryDialog` fields not live-UIA-verified (code-inspection only by design).
- **5 (2026-08-05)** Diary midnight rollover never observed.
- **6 (standing)** Scoring Settings' 12 inputs checked present, values never edited live (by design).
- **7 (2026-09-18)** Two-workflow pilot running — method in `~/.claude/reference/feature-workflow.md`; past outcomes `context/todos.md`.
- **0b (2026-10-13)** AI layer + replace-remaining v2 — built, QA'd, local commits; live checks done for regular, agentic Ask failed on the 8000-token cap (A1, open).
- **0c (2026-10-21)** Vision: autonomous / self-learning Planillium — idea only; decisions (a)-(e) recorded in detail; still undecided: spending cap, push rule, backup/preview/undo for data changes.
- **0d (2026-10-23)** User's request batches: B1 Reports/Settings DONE; B1b + B1c DONE 2026-10-25 (local commits, not pushed); B1d DONE 2026-10-25 both sides (regular 18283fc PASS WITH CAVEATS, agentic 58597a2 PASS after 2 rounds; local, not pushed; dashboard rows written, `defectsUser` awaiting user's eyes-on of both apps; caveat C1 caption click puts cursor in box -- awaiting user decision; scratch app reaches real AI key/TickTick credentials -- proposed own batch); B2 Diary (3-7), B3 data/performance (8, 9, slow Ask), B4 Today dot (11) + A1-A5/R1 fixes queued. Tab out of add-line amount box = KEEP (user, 2026-10-25). Open: regular caveats ("500 123" shows "= --/h", stale "Saved.", mouse-clicked Add leaves focus on the button). UPDATE 2026-10-26 (later): user tried agentic B1d on a scratch copy: all lines OK except line 5 -- blank-pane click after the new-tag box sometimes sends the cursor to the salary box (agentic only; regular 0 of 4). Defect b1d-ag-03 on both dashboards; B1f (agentic-only fix, `agentic/winui-agentic/pilot-specs/b1f-focus-jump/`): fix 1 d2d30b0 missed a 4 px right-edge strip (QA FAIL), fix 2 e82135f QA'd 2026-10-26: click-jump fixed (0 jumps in 258 edge trials), accepted on that evidence; local, not pushed. B1f QA overran (~70 extra min, final verdict FAIL on an unrelated item): the 7 fails are all Previous-salary-box Esc / refused-Enter behaviour (part C input-box matrix, never run on the B1d build; 65 of 144 part-C checks never ran). USER RULINGS 2026-10-27 for the next agentic fix batch (not yet briefed): (1) Esc in a money box leaves the box and clears its contents, same as the regular version (check regular's exact behaviour first); (2) "-5"+Enter keeps cursor and message; (3) empty+Esc shows the saved amount; (4) 12 digits refused with a message. B1f close-out still owed: WINDOW.txt `end:`, dashboard run rows, defect b1d-ag-03 status on both dashboards, qa-extra.md. QA rules tightened 2026-10-26 (global agent defs, journaled): plan with measured minutes first, one trial per distinct action, no re-test of passed checks, regression scope from the diff, QA model by job (opus scope, haiku only script re-runs, sonnet default; revised 2026-10-27), PM declines extensions without a new fact. User ruled QA trial counts follow case complexity, not a fixed 20+. Scratch-app launch note: MENTOR_ROOT works in Debug builds only (Release ignores it). UPDATE 2026-10-26: user OK'd B1d on the regular app (C1 kept as is); agentic app not yet tried; B1e (Reports: remove the two duplicate money cards, all periods) requested, briefs written, not built -- see `context/open-items.md` "Decisions and status 2026-10-26". UPDATE 2026-10-27: B1f closed out (dashboard rows + defects written); B1g DONE agentic-only (commit 6766961, local, not pushed): the four Esc/minus/12-digit rulings built, QA PASS WITH CAVEATS 44/44 (full regression matrix not run by scope); Esc rule decided = revert to saved amount and leave the box; regular app's saved boxes still do nothing on Esc (not changed, user to decide); `defectsUser` for B1f/B1g awaiting the user; qa-extra.md created.
- **Process (2026-10-25)** Lessons loop added: `regular/LESSONS-INBOX.md` and `agentic/LESSONS-INBOX.md` (PM-written) are merged into each side's `LESSONS.md` by its head; QA standing checks in `~/.claude/reference/app-qa-checks.md` §5.

## 8. Next steps

No standing backlog beyond §7 — the app is shipped and in daily use, not mid-build. Take §7 in the
order written if picking up idle work; otherwise this file's job is to be current when the next
real bug report, feature request, or pilot feature arrives.

## 9. Environment notes

- TickTick redirect URI must be registered at developer.ticktick.com as
  `http://localhost:8765/callback` in the **OAuth redirect URL** field (not "App Service URL").
- Build/verify commands, the live-vs-Debug-exe check, and UI-Automation verification technique are
  in `CLAUDE.md` (this project's own, not the machine-wide one) — don't duplicate them here.
- Machine-wide notes (`py` not `python`, cp1252 console) are in `~/.claude/CLAUDE.md`.

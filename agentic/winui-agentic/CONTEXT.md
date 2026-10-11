# Planillium (agentic twin) — Context

> Handoff for the **agentic workflow only**. Created 2026-10-01 as an adapted copy of the project's
> `CONTEXT.md`; from then on edited only by the agentic side. The regular workflow has its own
> `CONTEXT.md` one level up — never read or written from here. The orchestrating session records
> user decisions into this file when they apply to this side.

## 1. What this is

A desktop personal mentor and accountability companion: tracks progress across up to 3 active
life/career plans, monitors activity, keeps the user on-plan, logs the working day (06:00–20:00),
generates weekly reports. This folder is an independent duplicate of the WinUI app
(`Planillium.App/` + `Planillium.App.Tests/`, .NET 8 / WinUI 3), forked 2026-09-22 from the
regular app and built by Designer + Planner → Coder → QA agents. **Nothing here ships.** Pilot
rules and fork point: `PILOT.md`.

## 2. Current state

Built so far through this pipeline: lost-earnings-counter (2026-09-22), an eight-item batch
(2026-10-01), and "Replace remaining tasks…" (2026-10-09, built and unit-tested, not yet QA'd; new tasks start on last done day + 1, day 1 if none done, not clamped to today — `DECISIONS.md` rule 15). Product architecture, schema and settings: `context/domain.md`. Business rules and
standing lessons: `DECISIONS.md`.

## 3. Section index

- `DECISIONS.md` — the numbered business rules with full rationale (working hours · plan-day
  arithmetic · overdue accrual · one-task-per-day · archiving · score floor and bonuses · day-off
  scoring · `DriftDays` · progress-based "Day X of Y" · the lost/gained-earnings counter's rules)
  plus standing lessons (verification discipline, real-data safety incl. the scratch-`MENTOR_ROOT`
  technique, WinUI layout traps, timer/state rules, design lessons). Grep the topic; don't read it through.
- `context/domain.md` — headers: display name and rename history · app architecture · tech stack ·
  Plan JSON format · database schema · config.json key fields.
- `context/todos.md` — resolved work for this side only, dated.

## 4. Rules that are not negotiable

- **`data/progress.db` and `config.json` (at the repo root, outside this folder) are the user's real
  data.** Never read, write or run anything against them or the live app. Verify against a scratch
  database inside a worktree.
- **Never touch `winui/` (the regular app) or the regular workflow's documents** — read or write.
- The repo is public: no secrets, no personal data.
- Check every sibling of a fix: grep for the shape, not just the reported call site.

## 5. Still open

1. **(opened 2026-10-02) Three fixes awaiting the user's visual re-check, and a test run.** Reports column fix, pie alignment, pie slice shading (`context/todos.md` 2026-10-02). Automated tests not re-run after the change. Slice colouring is a design call: if per-category shading is wrong, the alternative is a distinct colour per app.

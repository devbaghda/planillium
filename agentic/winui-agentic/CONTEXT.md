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
(2026-10-01), and "Replace remaining tasks…" (2026-10-09, built and unit-tested, not yet QA'd; new tasks start on last done day + 1, day 1 if none done, not clamped to today — `DECISIONS.md` rule 15), and its v2 (2026-10-13, built and unit-tested, not yet QA'd: full unfinished-task list in the prompt, "What should change?" field, advisory "Words that must be gone" check in the preview), and "Ask Claude" (ai-layer, 2026-10-13, built and unit-tested, not yet QA'd; defect D1 — reason cleared right after an AI fill — fixed the same day by `ReplyErrorState`, Add plan's reply check by `PlanReplyCheck`, both in `Services/AiClient.cs`). Product architecture, schema and settings: `context/domain.md`. Business rules and
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
- **Never touch `regular/winui/` (the regular app) or the regular workflow's documents** — read or write.
- The repo is public: no secrets, no personal data.
- Check every sibling of a fix: grep for the shape, not just the reported call site.

## 5. Still open

1. **(opened 2026-10-02) Three fixes awaiting the user's visual re-check, and a test run.** Reports column fix, pie alignment, pie slice shading (`context/todos.md` 2026-10-02). Automated tests not re-run after the change. Slice colouring is a design call: if per-category shading is wrong, the alternative is a distinct colour per app.
2. **(opened 2026-10-13) "Ask Claude" (ai-layer) built and unit-tested, not QA'd.** Settings -> AI plus
   Ask Claude in Replace remaining tasks and Add plan (`pilot-specs/ai-layer/SPEC.md`, `DECISIONS.md`
   rule 16, `context/domain.md` "AI layer"). Build (Debug, -warnaserror) 0 warnings; `dotnet test` all
   pass, including `AiClientTests.cs` against a local HttpListener stub. Not done: live Credential
   Manager save/test (only the test slot may be used), the one-time notice visuals, Stop and the
   dialog layout at narrow width. Deviation: the notice is inline, not a ContentDialog.
   **2026-10-13 follow-up:** QA defect D1 (garbage AI reply: reason vanished) fixed in Replace remaining
   tasks; Add plan now reports a non-plan reply honestly. Tests 310/310. Needs a live UIA re-check of
   both (`pilot-specs/ai-layer/SPEC.md`, "Follow-up 2026-10-13").

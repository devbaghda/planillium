# Planillium agentic-workflow pilot

Independent duplicate of the WinUI app, built by a Designer + Planner → Coder → QA pipeline
(`.claude/agents/planillium-{designer,planner,coder,qa}.md`), compared against the same feature
built by the regular single-session-with-skills workflow in `regular/winui/Planillium.App/` (shipped, in
daily live use, never touched by this pilot). Method for both sides, rules and metrics:
`~/.claude/reference/feature-workflow.md`.

## Fork point

Created **2026-09-22** from `regular/winui/Planillium.App/` and `regular/winui/Planillium.App.Tests/` — source
only, `bin/`/`obj/` excluded (91 + 17 files, count-verified). Real data (`data/progress.db`,
`config.json`, `plans/*.json`) lives at the repo root, never under `regular/winui/`, and is never read here.

## Documents are split (2026-10-01)

This folder has its **own** `CONTEXT.md`, `DECISIONS.md` and `context/` — copied once on
2026-10-01, edited only by the agentic side from then on. Agents here read only this folder plus the
feature's `pilot-specs/<slug>/{REQUEST.md,ACCEPTANCE.md}`. They never read the parent project's
`CONTEXT.md`, `DECISIONS.md`, `context/`, or anything under `regular/winui/` — the old "shared ground"
arrangement was dropped because a living handoff doc leaked a regular-side implementation write-up
into the blind comparison (2026-09-22). With fully separate copies there is nothing to leak.

## Contamination rule

Planner, Designer, Coder and QA never read `regular/winui/Planillium.App/`'s source, its git history, or any
description of how the regular workflow built a given request. Each side answers the same product
brief independently. QA, when asked to test the **regular** side, reads that side's source (it must
test it) but never the agentic side's spec or code while doing so, and applies the identical
`ACCEPTANCE.md`.

## Per-feature inputs

`pilot-specs/<slug>/REQUEST.md` (product terms, no implementation) and `ACCEPTANCE.md` (mechanical
pass/fail lines) are written by the orchestrator before either side builds. A copy goes to each
side's own folder.

## Comparison basis

Three headline numbers per side per feature — defects (QA first pass + found by the user after),
tokens (new work, with context re-reads shown apart), wall-clock — plus interventions (per feature)
and corrections. Measured with `D:\CLAUDE\MASTERMIND\src\pilot_metrics\pilot_metrics.py` over each
side's logged start–end window; logged by the **orchestrator** to the Dashboard
(`https://claude.ai/artifact/1CJiroBcpychcmgmYApXTr`, `project: "planillium"`). Sides run one after
the other, never interleaved.

**Nothing from this folder ships.** If a pilot-side implementation turns out better, that is a
finding to act on deliberately, not something merged silently.

## Runs

Bare pointers only — outcomes go in `context/todos.md`.

- lost-earnings-counter (2026-09-22) · 2026-10-01 eight-item batch.

# Agentic-workflow pilot — Planillium side

This folder is a source-only fork of `winui/Planillium.App/` and `winui/Planillium.App.Tests/`,
built by the `planillium-planner` → `planillium-coder` → `planillium-qa` pipeline instead of the
regular single-agent-with-skills workflow, so the two workflows' outcomes can be compared on the
same feature requests. See `~/.claude/CLAUDE.md` and this project's own `CLAUDE.md`/`CONTEXT.md`
for the standing rules this pilot inherits; this file is the pilot-specific record.

## Fork point

Copied **2026-09-18**, from the state of `winui/Planillium.App/` and
`winui/Planillium.App.Tests/` on disk at that date (`master`/`winui-rebuild`, test suite 151/152 —
one known time-of-day flake, not a regression). Anything already in this folder from that copy is
shared ground both sides may read. Anything that changes in `winui/` **after** this date is
post-fork and off-limits to the agentic side (contamination rule, enforced in each agent's own
instructions).

## What was excluded, and why

- `data/` (the live SQLite DB, `progress.db`) — the user's real, live personal data. Never copied.
- `config.json` — the user's real settings (working hours, TickTick tokens, thresholds). Never
  copied; if a spec needs new settings fields, it defines their *schema*, not real values.
- `plans/` — real personal plan content. Never copied.
- `bin/`, `obj/` — build output, regenerable, stripped from the copy.

None of the above exists anywhere under `winui-agentic/`. If a feature needs to exercise data or
settings, the agentic side works against a scratch SQLite file / scratch config created inside
QA's isolated worktree — never the real files, never the live running app.

## Comparison design

Same feature request goes to both sides:
- **Regular**: developed as always in `winui/Planillium.App/`, by the normal single-agent-with-
  skills workflow.
- **Agentic**: `planillium-planner` writes a spec (never reading the regular side's post-fork
  diff/history for this feature) → `planillium-coder` implements it here → `planillium-qa` tests it
  in an isolated worktree and logs a row to the shared pilot Dashboard.

## Dashboard

Results (both projects running this pilot — DigiFlow and Planillium — share one Dashboard) are at:
**https://claude.ai/artifact/1CJiroBcpychcmgmYApXTr**

One row per run: project, workflow, feature, wall-clock time, a token-burn proxy, bugs found, and a
1–5 clarity/aesthetic score (QA logs a provisional score; the user's own review is what counts as
final). Nothing on that page is placeholder data — it only ever shows real logged runs.

## Do not ship this folder

`winui-agentic/` is a comparison exercise, not a release candidate. Nothing from here reaches
`origin`, a build artifact, or the user's real installed app without the user explicitly reviewing
and deciding to merge it in — same as the regular workflow's own review bar, but this folder's
default is "never," not "when ready."

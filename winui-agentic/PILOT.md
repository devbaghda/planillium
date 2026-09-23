# Planillium agentic-workflow pilot

Independent duplicate of the WinUI app, built by a three-agent pipeline
(`.claude/agents/planillium-{planner,coder,qa}.md`: Planner → Coder → QA), compared against the
same feature built by the regular single-agent-with-skills workflow in `winui/Planillium.App/`
(the original — shipped, in daily live use, never touched by this pilot).

## Fork point

Created **2026-09-22**, from the live state of `winui/Planillium.App/` and
`winui/Planillium.App.Tests/` at that date — source only, `bin/`/`obj/` excluded. File counts
verified equal at copy time (91 + 17).

**Excluded from the snapshot** (none of it existed under `winui/` to begin with, confirmed by
search before copying, but stated here for the record): `data/progress.db` and its `-wal`/`-shm`
siblings, `config.json`, `plans/*.json` — all real personal data, all live one level up at the
repo root, never under `winui/`. Nothing under this fork should ever read or write those paths;
each pilot run works against scratch data only (`planillium-qa`'s isolation rule).

## Contamination rule

Planner, Coder and QA read only: this folder's own contents, this file, and the feature request
under `pilot-specs/<slug>/REQUEST.md`. They never read `winui/Planillium.App/`'s current source,
its git history, or any description of how the regular workflow implemented a given request —
each side answers the same product brief independently. If either side's file under
`pilot-specs/` needs to reference something from the regular workflow's decisions (e.g. a shared
business rule already settled in `DECISIONS.md`), that's fine — `DECISIONS.md` and
`context/domain.md` are shared ground, not the regular side's implementation.

**`CONTEXT.md` is deliberately not on this list, as of 2026-09-23.** It used to be, on the theory
that it's "shared ground" like the two files above — but unlike them it's a narrative handoff doc,
not a stable rules/architecture register, and it has already carried a post-fork implementation
write-up once (see "Pilot write-ups" below). Telling agents "don't write implementation detail
there" relies on every future session remembering; not granting read access at all doesn't. If a
spec genuinely needs something that only lives in `CONTEXT.md`, that's a sign that fact belongs in
`DECISIONS.md` or `context/domain.md` instead — move it there rather than reading `CONTEXT.md`
directly.

## Pilot write-ups

Outcomes (files touched, build/test results, QA verdicts, caveats) are logged only in
`context/todos.md`, one level up — never in `CONTEXT.md` or in this file's own "Runs" section
below. Both of those are on this pilot's allowed-read list, so an implementation write-up placed
in either becomes the answer key for whichever side hasn't built its version yet. Keep entries
under "Runs" to a bare pointer (feature name, request path, run date) — nothing about how either
side actually implemented it. Learned the hard way 2026-09-22, lost-earnings-counter: a
regular-workflow write-up sat in `CONTEXT.md` while Planner was independently speccing the
agentic side; Planner noticed and declined to use it, but nothing structural had stopped it.

## Comparison basis

For each feature run through both sides: wall-clock time, a token-burn proxy, bugs found,
corrections requested (Coder passes that came back from a QA FAIL), interventions (times the
orchestrating regular-workflow session had to stop and ask the user a decision), and a 1-5
clarity/aesthetic score — QA logs a provisional score, the user gives the reviewed one. All logged
to the shared Dashboard: `https://claude.ai/artifact/1CJiroBcpychcmgmYApXTr` (`project:
"planillium"`, shared with DigiFlow's pilot).

**Nothing from this folder ships.** It exists to compare workflows, not to produce code that
reaches `winui/Planillium.App/`. If a pilot-side implementation turns out better, that's a finding
to act on deliberately — not something that gets merged silently.

## Runs

- **lost-earnings-counter** (queued 2026-09-18, request finalized 2026-09-22) — see
  `pilot-specs/lost-earnings-counter/REQUEST.md`. First feature run through this pilot.

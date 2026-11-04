# BRIEF — G11 group-QA fixes (2026-10-30)
User ruling: "go with your recommendations" — fix every defect from the end-of-group QA on both sides, then one re-QA of only the failed and
not-run lines.
Context: see REQUEST.md (7 items) and ACCEPTANCE.md (10 lines). Source of the items: each side's QA run of 2026-10-29. Not in scope: the open
question whether "Mark all neutral" may overwrite idle and paid rows (asked of the user), G5 memory, the unrun lines (re-QA covers them).
Constraints: own side only; real data off limits (data/, config.json, plans/, live app); builders do not write tests (QA does); precheck
(strict build + shared suite) before hand-off; do not touch the live Release exe; commit locally, do not push; update your own docs and
LESSONS per the project CLAUDE.md. Item 2 changes how the Total is computed: write that into your own DECISIONS/CONTEXT as a rule.
No QA by the builder this round: QA is deferred to the single re-QA run by the PM's order (the hand-off says "QA deferred").

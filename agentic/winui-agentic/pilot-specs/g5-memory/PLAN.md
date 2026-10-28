# G5 memory -- lead plan (2026-10-27)

PM additions (relayed from the user, part of this brief):
- "Show all" must create only the rows actually on screen; results, totals and "Showing N of M" unchanged; the 300-row cap of this side stays as is.
- Also check idle/live refresh growth and period-switch growth; report any unexplained extra memory on Reports Week.
- Precheck only (strict build + dotnet test --no-build), no QA, no push, commit locally on winui-rebuild.

Split:
1. designer -- not launched: no new screen; nothing visible may change.
2. planner (opus) -- cause unknown, so investigation comes first: build a large scratch DB in the scratchpad, launch Debug on scratch root, measure private working set (empty app, Diary 50 rows, Show all, Reports each period, idle/live refresh, period switching), find the causes in code, write SPEC.md with before numbers, ranked causes, fixes, files, measurement script path. Cross-cutting and costly if wrong -> opus.
3. coder -- implements SPEC.md, runs precheck, re-measures with the planner's script (after numbers), updates side docs (CHANGELOG Unreleased, context/todos.md, CONTEXT.md, DECISIONS.md lessons), commits locally.
Order: measure before fixing (acceptance 1-2 need both numbers from the same method).
Playbook: fix-defect.md.

Lead rulings on SPEC open questions (2026-10-28):
1. "50 rows" = this side's default first page (40); measure there, state it in the report.
2. No forced garbage collection; structural fixes only. If memory still climbs after the fixes, report the numbers.
3. Fix 5 (filter lists built when the flyout opens) stays; it also repairs the empty App/Page flyout after a period switch. Flagged to the PM as a visible change.
4. QA note kept for later; no QA this round.

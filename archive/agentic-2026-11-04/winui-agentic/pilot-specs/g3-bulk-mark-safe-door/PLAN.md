# PLAN -- G3 bulk marking + safe door (agentic, lead 2026-10-29)

Spec folder: `agentic/winui-agentic/pilot-specs/g3-bulk-mark-safe-door/` (BRIEF.md incl. 2026-10-29 addendum, REQUEST.md, ACCEPTANCE.md 1-9).

Order and why:
1. **Designer** (screen change: Diary Mark button count text, confirm dialog, success message with Undo, Settings -> Change history list). Writes `SPEC.md` section "Screen design" only, reusing the existing Diary/Settings/dialog look. First, because the planner must spec the code behind exactly these controls.
2. **Planner** -- investigates how Mark and the 300-row cap work today and the cause of disappearing marks AND vanishing ticks (addendum 3), then completes `SPEC.md`: one reusable data-change service (preview -> dated backup keep 5 -> write with old values -> undo with edited-since skip), history 20 / 30 days, Undo forgetting only the keywords that mark taught (addendum 2), ticks surviving Mark/Undo, idle rows unchanged (addendum 4), acceptance copied verbatim. Needs the designer's section to place the code.
3. **Coder** -- implements SPEC.md, updates side docs (CHANGELOG Unreleased, MANUAL, context/todos.md, CONTEXT.md, DECISIONS.md rule + lessons), runs the precheck (strict build + existing suite) until green, commits locally on winui-rebuild.
4. **QA** -- after a green precheck; isolated worktree, scratch MENTOR_ROOT with ~2 400 seeded diary rows; writes QA-REPORT.md. Budget not in the brief: asked of the PM before starting.

Note: REQUEST cites "DECISIONS.md rule 19", which does not exist in this side's DECISIONS.md; the root DECISIONS.md is edited by the regular side, so it is not read (contamination rule). Members work from REQUEST/BRIEF/ACCEPTANCE; the coder writes this side's rule into `winui-agentic/DECISIONS.md`.

## Round 2 (2026-10-29, BRIEF.md "Addendum 2")
- Designer and planner not launched: Addendum 2 answers 1-5 are an implementation-ready change list on top of 9b8e864 (SPEC.md and the service already exist); the coder works from BRIEF Addendum 2 + SPEC.md directly.
- Coder: implement answers 1-5, record them (and the accepted defaults of the other open questions) in this side's docs, precheck, commit locally. No push.
- QA: not started this round (user deferred QA to the end of the group).

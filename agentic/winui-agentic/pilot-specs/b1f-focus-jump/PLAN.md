# PLAN -- B1f (agentic), lead

- designer: not launched -- no new screen.
- planner: not launched -- REQUEST/ACCEPTANCE are a bounded defect (one symptom, clear pass lines); the work is root-cause debugging, which belongs with the coder.
- coder (opus): find the root cause of the salary box taking focus on a blank-pane click after leaving a box (real mouse, ~1 s spacing); fix the cause, not another guard; run the precheck (strict build + dotnet test). Playbook: fix-defect.md.
- QA (workflow-qa): after green precheck; scratch MENTOR_ROOT, Debug build, real mouse input, 20+ trials per case; writes QA-REPORT.md.
Order: coder -> precheck -> QA -> (fix pass if FAIL) -> lessons merge -> commit.

## Resume 2026-10-26 (addendum)
- coder (haiku): write the addendum rule into DECISIONS.md standing lessons, update CONTEXT.md B1f line and context/todos.md, run precheck (strict build + dotnet test), commit docs. Mechanical: rule text is given verbatim. Playbook: fix-defect.md (docs step).
- QA (workflow-qa) on e82135f: edge-strip lines 1-5 plus lines the fix could touch; the 6 addendum cases; 20 min budget; QA-PROGRESS.md line after each case.
Order: coder (docs + precheck) -> QA -> lessons merge.

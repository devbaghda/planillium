# PLAN -- G8 focus protection on other pages (agentic, lead 2026-10-29)

No screen design (no new screen; behaviour only) -> no designer.

1. **Planner (opus)** -> `SPEC.md` in this folder. Find the B1f fix in the code (Settings -> Income), inventory every
   scrolling page/pane/dialog with a text box (acceptance 1 table: covered / not applicable + reason), and specify
   how the same protection is applied to each (shared helper vs per-page), including the edge/padding strips and
   section gaps (LESSONS: B1f missed a 4 px edge strip), every exit path, and that Tab/click-into-box and Settings ->
   Income behaviour stay unchanged. Why first: the inventory and approach are the ambiguous, cross-cutting part.
2. **Coder (opus)** implements SPEC.md, runs the precheck (strict build -warnaserror --no-incremental + existing
   suite), updates the side's own docs (CONTEXT.md, context/todos.md, CHANGELOG.md, DECISIONS.md standing lesson if
   any), commits locally by explicit paths (no push). Why after: needs the spec.
3. **QA: not started** (BRIEF addendum 2026-10-29, tests deferred). Lead stops after the precheck and commit.

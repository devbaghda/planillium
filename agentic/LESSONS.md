# LESSONS -- agentic side (rules only; merged by the lead)

## Focus / input boxes
- Setting a TextBox's text from its own change handler re-fires the handler (echo): ignore the echo; keep the hint while the value is invalid.
- After every way out of an input (Esc, Enter, Tab, click blank/heading/button/label/other box, Add by mouse) assert where focus ends (nowhere, never the salary box); test with REAL clicks and keys.
- Test every exit path of a box each round (Enter, Tab, click blank, click other box); a fix on "leaving" must be applied to all of them (B1d round 1 failed only on Enter).
- A special "keep" state (e.g. after a refused Add) must list which user actions still end it (Esc, click outside) and each must be tested.
- WinUI delivers GotFocus/LostFocus late: never use a one-shot flag consumed by "the next LostFocus"; check the settled focus target.
- A focus guard blocks UIA SetFocus: QA moves focus with real clicks and keys only.

## Validation / user rules (2026-10-25)
- Empty, non-numeric, minus or zero amount is refused with a visible message and never saved; box and stored value must agree on leaving (revert) or the text stays with a message.
- Esc / click-outside clears the add line; Tab out of the add-line amount KEEPS the text; Enter in the add line = Add, then the cursor leaves; Enter or leaving a money box saves and focus ends nowhere.
- A refused entry clears any stale "Saved." message.

## Layout
- Layout-timing / scroll-into-view is hard (opus): force layout, then scroll into view; test at 1, N and N+2 items.
- A focus/pointer fix must cover the whole container incl. edges and padding; test the borders (1-3 px from each edge), not only the interior (B1f: 4 px padding strip still jumped).

## QA technique / safety
- Refuse any click outside the app window and any keystroke unless the app is foreground; never launch shared apps (Notepad) for window tests.
- Find UIA elements by walking descendants and matching automation id, not visible title (non-ASCII, dialogs on top).
- UIA gives a zero rectangle for clipped/scrolled-away elements: treat as direction unknown, wait for scroll animation.
- Focus-jump cases: reproduce with real mouse input (cursor moved, SendInput), ~1 s click spacing, 20+ trials per case; one scripted click per case missed a 2-in-5 defect (B1d, found by user).
- b1f_regress.py A,B,C takes over 8 min (plus ~1 min build/test): budget it, and send BUDGET-REQUEST at start, never after overrunning.
- b1f_regress.py part C (input-box matrix, 144 checks) needs ~7 s per check, about 17 min; plan minutes per script part before asking for budget.
- Item-level start without editing b1f_regress.py: exec its helpers up to the "A: controls" marker, then run one block in the same launch/kill wrapper (qa/b1f_run_b1d9.py).
- Under a tight budget run the regression case first; save script output to qa/*.out, never only piped through grep/head.
- Before dotnet test --no-build, check the test DLL is newer than the last source commit; else build the test project first.
- Run QA Python with PYTHONIOENCODING=utf-8; write scripts with the file tool, not quoted heredocs; reset leftover add-line state before judging a failure.

## Process
- Cheapest prevention is the first fix being right: read these rules and app-qa-checks section 5 before coding.
- Read winui-agentic/CONTEXT.md (own side's record); root CONTEXT.md is not yours.
- Stage commits by explicit path: root CONTEXT.md/context/todos.md share names with the side's files.
- Tool worktree isolation blocks writing QA-REPORT.md to the main target: QA uses its own git worktree and writes the report to the main path.

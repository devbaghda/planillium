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
- Bring the app to the foreground before each case, not only at launch (B1g: another app stole focus mid-run).
- A diff-scoped matrix (2 boxes x every exit x 5 inputs) runs in under 3 min (~3.5 s per check): always run it.
- Fix Esc/Enter/Tab/click behaviour for each (exit x input kind) row before coding; a silent input cap is an unmessaged refusal and the cut value can get saved.
- Leave/revert logic reads the box's current text, not state cached from the last change event (WinUI updates it late).
- A page-level key handler that swallows a key must pass it through when the focused control has its own rule for it.
- Reports layout check: one launch + UIA text dump per period (~1 min); compare y of score caption, strip and section heading for order and gap (qa/b1e_run.py reusable).
- Card captions are built in C# in upper case with the period name: search case-insensitively, read the builder.
- After removing a card, discard unused tuple values with `_` (keep the calculation, stay /warnaserror clean); grep all callers before deleting a shared helper.

## Money columns / icons (B1i)
- Split a period figure into row values by running-total cent rounding, last total pinned to the figure shown elsewhere, so rows add up to the Total to the cent.
- Never drop money-carrying rows for having no activity (Year empty-month filter); give shared row builders an optional switch, keep the default so exports do not change.
- A per-theme colour in page code goes in ThemeSync.Fixed with one key constant, not a RequestedTheme check.
- An icon that shows nothing may be an empty Glyph string: check by code point, write glyphs as \u escapes.
- When a column is added after a "last column" colour test (c == cells.Length - 1), switch it to a named index.

## Secrets / scratch safety (G7)
- Guard secrets at the single store (read, write AND delete), keyed on a runtime check (MENTOR_ROOT non-empty), never Debug-only; search deletes/writes, not only reads.
- Put a shared check in a file the test project already links (AppPaths.cs); name allow-list exceptions by their existing constant.
- Log.Info takes one argument, Log.Warn (context, message): check the signature before copying a call.

## Diary filters / paging (G2)
- Before adding a "no value" filter entry, check what the getter returns for empty (AppNames.Group returns "—", not null): map the placeholder to null first.
- Read NOT NULL/default markers before defining "no value" (category is always set; unmatched = neutral): raise it as an open question.
- A "-" sentinel kept in the same set as real entries must be exempt from the prune step.
- Check hidden caps (300-row search cap) before specifying paging; lifting one widens Select all.
- Odd-value investigation: trace capture, then storage, then the read-time parser.
- Seed a ComboBox SelectedIndex before wiring SelectionChanged; a filter set reassigned per render needs a getter, not a captured reference.

## AI reply cap / dialogs (G4)
- Raising a non-streaming token cap means re-checking the request timeout (reply time ~ tokens/50 s); show durations from one constant, never repeated text.
- To remove a ContentDialog scrollbar raise the ScrollViewer MaxHeight AND override ContentDialogMaxHeight; read xamlRoot.Size when the dialog is built.
- A subagent can exit without a handback: check for its output file, rerun once.

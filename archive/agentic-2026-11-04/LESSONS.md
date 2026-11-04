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
- Restyle to a screenshot: diff the target against the existing builder first; when buttons move into a menu keep their UIA names, keep the long name when the label is shortened.
- Verify a Stop/Cancel by tracing the token to HttpClient.SendAsync, not by its label.

## Memory (G5)
- Measure before fixing: a script that launches on a scratch root and logs private working set AND private bytes per step (Windows trims a hidden window's working set); ctypes GetProcessMemoryInfo, no psutil.
- Long row lists go in an ItemsRepeater (only on-screen rows built); a custom IElementFactory must remove the element in RecycleElement or every row ever shown is kept.
- A partial change (period switch, pie drill) re-renders only its own panel; a whole-page rebuild per click costs 80-150 MB of late-freed trees.
- Live refresh compares the loaded rows with the shown ones and skips the rebuild when equal.
- A cached page clears its panels and stops its timers on leaving, rebuilds and restores scroll on return.
- Never walk the full WinUI UIA tree repeatedly in a measuring run: it inflates the app's own memory (1.5-3 GB); find elements once, reuse.
- Attribute a cause cheaply by rerunning one scenario with one input shrunk (30 vs 2600 pages).
- Long builds/app runs/measurements run in the background writing a log; poll under 2 min per call (a foreground run stalled the G5 planner).
- Edit CRLF files with the Edit tool or byte-level replace; Python text mode or sed -i turns CRLF into LF.
- A leak is a bottom level that keeps climbing: run one scenario per launch for 15 min, sample every 30 s, split .NET-managed vs native (largest private reserved block via VirtualQueryEx); a sawtooth or plateau is not a leak.
- A "low-risk" fix that changes when the screen refreshes is a visible change: report it as an option, do not apply it.

## Bulk marks / caps (G3)
- Lifting a display cap touches four places: the truncation, the paging row's reachable count, Select all's row set, and the order of state resets.
- Count text on a button can outgrow its row: check the width of every sibling row it applies to.
- An option that varies per click (teach keywords) is threaded into every text it affects (dialog note, success bar, Settings confirm); grep the old fixed sentence.
- C# has no `action?.()`: write `action?.Invoke()`; build, do not eyeball.
- Check `git ls-files` before assuming a spec doc is tracked.

## Machine-wide guards / filters / prompts (G10)
- A guard on machine-wide side effects needs a sweep of every sibling (Registry, SpecialFolder/AppData, env vars, Process.Start, credential store); list each as covered or justified (G7 missed the Run key).
- A "no dash" filter rule must exempt known bare program names first, or real apps land in "Other"; ask whether unlisted bare names go to "Other".
- Before rewriting a prompt template grep the tests for its schema phrases; keep them or update assertions in the same pass.
- Compact formats: explicit marker field (cf); only marked replies are expanded/rejected, unmarked input keeps the old path.
- Rewriting a CRLF template file: normalise to LF, replace blocks, write CRLF back, check git diff --stat.
- Spec lines in conflict (reject old form vs keep tests/file load): keep the safe behaviour, report the conflict.

## Focus protection across pages / dialogs (G8)
- Every ContentDialog has a scrolling body (its template wraps title, padding and content in a ScrollViewer): treat dialogs as scrollers.
- Route a whole UI kind through its single factory (DialogControls.Build) so one change covers every sibling and future ones.
- Claim PointerReleased at a scroller's template root to cover padding, scrollbar strip and area below content without layout changes (untested until G8 QA).
- A press-time park must skip presses inside popups and inside the focused box's owner control (NumberBox, AutoSuggestBox), or suggestion picking breaks.
- A "did the user click a control" test walks up to the nearest Control, skipping ScrollViewer/ScrollBar chrome; the pressed element is usually inner layout.
- A park inside a scroller handles BringIntoViewRequested (no scroll) and resets its tab stop on LostFocus; give the helper a prepare method for XAML-declared parks.
- A guard remembering the last focused box per area must forget it when an outside park takes focus.
- WinUI: AccessibilityView is in Microsoft.UI.Xaml.Automation.Peers; fix the CS error first, the WMC9999/WMC1509 XAML errors follow from it.
- Glob may report "No files found" for an existing folder: confirm with ls.

## Indicators / badges (G6)
- Before "fixing" an indicator, grep XAML and code to confirm it exists; if not, build it and report the false premise.
- Derive a new indicator from the existing widget's own condition via one shared function, never a second definition (e.g. overdue).
- Hook refresh to the call every mutating action already makes (RefreshScore) plus the widget's Render: covers all pages and midnight rollover.

## AI cost cap / estimates (G9a)
- Wire a cross-cutting guard in the single factory (AiKey.CreateClient) with optional ctor params so existing tests stay unchanged; any new AI call must be built through it.
- A spend estimate needs one named price table, a dearest-row fallback for unknown models, a chars/3 fallback when usage is missing, and the UI/report must say "estimate".
- "Available wherever X is used": grep who parses X first; the real defect (dash inside Telegram chat name) was in the shared parser, not a missing field.
- sed -i flips CRLF on csproj files too: use Edit or a byte-level replace, git checkout to undo.

## Group QA technique (2026-10-29)
- b1f_lib launch() ends on MENTOR_PAGE=settings and hard-codes stub URL port 9: override per step, run the stub on that port.
- PrintWindow can return black: use pywinauto capture_as_image() with the app in front.
- The Today dot (InfoBadge) is not in the UIA tree: count accent-colour pixels at the nav item before/after.
- Diary "Showing N of M" is not UIA-readable; the "Mark all N ..." button names carry the filtered count.
- Write QA-PROGRESS.md by absolute main path (a relative path from a worktree lands in the worktree copy).
- Python files with NBSP or the euro sign written via sed break UTF-8: write with the file tool using \u escapes.
- AI-dependent lines need a decision up front: scratch runs allow only credential anthropic_api_key_test, stored in the real Credential Manager; ask the PM at plan time.
- Diary: "– Telegram" rows (no chat name) must count under Telegram, not Other; a paid tag equal to a built-in tag must be listed once; check Reports header width at 1180x780; a refusal message must clear when the box text changes.

## G11 group-QA fixes (2026-10-30)
- Before calling a .NET runtime crash fixed, check the triggering switch exists in this build, then prove the fix by forcing the switch in a scratch program; every JsonSerializerOptions carries an explicit resolver (JsonFileIO.Plain/Indented), no Serialize without options.
- A scratch-safety guard at the single store allows no name exceptions; test secrets live in memory (env PLANILLIUM_SCRATCH_AI_KEY, scratch Debug only).
- Compare user-editable keys (tag values) ignoring case where matched or de-duplicated; check money paths separately (open question: paid-tag income is still case-exact).
- A title parser must handle the separator with nothing on one side ("- Telegram").
- "Header cut off" on a sideways-scrolling table is often a hidden scrollbar: wrap the header, do not change the shared column grid.
- Tie a refusal message to the box it refuses and clear it when that box changes; refresh a status line from an event raised by the one writer, not only on page load.
- Model log is agentic/MODEL-LOG.md (winui-agentic/MODEL-LOG.md is an old B1d file).

## G12 Settings Esc / ceiling (2026-10-30)
- Esc leaves every Settings text/number box and restores the saved value; test Esc and blank click on each box (siblings), reading which element got focus ("Nav" = parked).
- Enter in an AI box saves only that box; a key is stored only by Save or Enter in the key box; blank click or Esc empties an unsaved key (G13, QA PASS). A forgotten key left after Tab/click-away is stored by a later Save (rule allows; PM may rule).
- Read summary/status texts outside the tested box after every step, not only focus and value.
- Money ceiling EUR 100 000,00/month applies to salary, tag amounts and new tag; test the boundary (100000 ok, 100000,01 refused).
- A Compact NumberBox's spin buttons are not in the UIA tree: ElementFromPoint at its right edge gives "UpDownPopup"; a real click on the upper/lower half steps it.
- Check the test DLL date before dotnet test --no-build; insert edits above a method's doc comment and read back; keep CRLF when scripting edits.

## G13 key-only-on-save (2026-10-30)
- The password box text is unreadable by UIA: judge "empty" by screenshot vs an empty box.
- Run the "stores the key" line on a fresh launch (a saved key already shows "Key saved", repeat proves nothing); put "No key" cases first, storing cases last (one launch, ~3 min).
- Kill only the PID you started, never by program name (the user's own Planillium may run).
- A shared Enter handler saves per box by branching on the sender; use ReferenceEquals(sender, box) (== on object vs control fails -warnaserror CS0252).
- "Click blank discards" belongs at the single place the park receives the cursor (FocusPark_GettingFocus, OldFocusedElement).

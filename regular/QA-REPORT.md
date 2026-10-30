VERDICT: PASS WITH CAVEATS - workflow=regular feature=g14-user-rulings defectsQa=0

Build 4429235 (winui-rebuild), QA worktree agent-a023b7161cebc3538, Debug strict. Scratch root C:/Users/devba/AppData/Local/Temp/claude/d--CLAUDE-Planillium/56fe036f-c245-4174-a179-705bf772c126/scratchpad/g14/mroot (new, empty, no progress.db copied), suffixes verify-g14a/b/c, AI base URL 127.0.0.1:8767 (nothing listening), no key in env. 14 of 30 minutes used.

## Per-line results
| Line | Result | Evidence |
|---|---|---|
| 1 Missing MENTOR_ROOT refuses; empty folder starts | PASS | Missing path: window "Planillium — data folder missing" with text naming the full scratch path and "No other data folder was opened."; OK -> process exit code 1 (2 launches). Path still absent afterwards; no new progress.db anywhere under D:/CLAUDE/Planillium. Real data/ file list identical before/after (metadata only, no file opened); the only changed timestamps are mentor-winui.log (written every minute at :49, also before and after my runs) and progress.db-wal (at :51, first at 14:10:51 before my first launch) = the live Release app PID 21532. Existing empty folder: app opens, creates mroot/data/progress.db. Screenshot c1_msg.png. |
| 2 Reports headers at 1180x780 | PASS | Window measured 1180x780. Day/Week/Month/Year: "Income / lost/gained" and "Of which / paid work" on two lines (30 px), right edge at most 1235 px, inside the table; one-word headers on one line; screenshots rep_Day/Week/Month/Year.png show every header in full. |
| 3 Enter in AI cap / Model | PASS | Cap: click, Ctrl+A, 7, Enter -> config cap_eur 7, focus on the park button (no box), typed 9 changes nothing. Model: same with a new name -> saved, parked, 9 nothing. Left to Reports and back: 7 and the model still shown. Cap -5 + Enter -> inline "The cap must be more than 0 (...)", focus stays in the cap box, config unchanged. |
| 4 Salary too large, Tab, click other box | PASS | Tab -> box shows saved 2 925, status "Amount is too large — at most €100 000,00 a month. The saved amount was kept."; selecting the AI section keeps it; click AI cap box -> message gone. Variant: click the Studioshoo box -> gone. |
| 5 Live message while typing, nothing saved until Tab | PASS (regular) | While still in the box: red "Amount is too large — at most €100 000,00 a month." under it (screenshot c4_typing.png); config has no income entry; after Tab the box shows 2 925. Agentic half: not tested by this QA (own side only, contamination rule). |
| 6 Regression: Esc / blank click / key + blank | PASS | Salary 3000 + Esc -> 2 925, cursor parked, nothing saved. True blank click (point checked: SectionScroll) -> IncomeFocusPark, then Tab -> Employed toggle, same as a plain Tab. Key typed + blank click -> "No key", Save probe still "No key", Credential Manager names unchanged (57). |
| 7 Strict build, tests | PASS | dotnet build -warnaserror --no-incremental: 0 warnings, 0 errors; dotnet test: 229/229 passed. |

Extra (REQUEST 4/5 on the tag boxes, not in ACCEPTANCE): Studioshoo amount 999999999999 -> live message, Tab -> 1 600 and status refusal, click salary -> gone; new-tag amount shows the live message, Esc clears it; config unchanged. PASS.

qa-extra.md lines: NOT RUN beyond the ones lines 2, 4, 5 and 6 cover (brief limits this run to lines 1-7).

## Defects
None against ACCEPTANCE or REQUEST.

## Caveats / observations (outside the checklist)
1. Click on page text loses the Tab position. Settings > Income: click in the salary box, then click the page title "Settings" (or the "This app is tracking..." line). Cursor goes to an unnamed pane (not the park button); Tab then goes to the nav item "Today" instead of the Employed toggle. Why it matters: standing rule (LESSONS: every way of parking must record the box left) - a keyboard user loses their place. Fix: treat a click on non-focusable page text like a blank click (park and record the box). Likely older than G14 (not in the diff).
2. Salary box keeps only 9 digits: typing 999999999999 shows 999 999 999. The message still appears; just noting the typed text is cut silently.
3. While the too-large amount is in the box the hourly readout shows "= €5 952 380,95/h", a figure for an amount that will not be saved.

## Security
No real key: no key in env, fake key typed only into the scratch run, never stored (Credential Manager names identical before/after, 57). AI calls pointed at an unused local port. Real data/, config.json, plans/ never opened or written; live Release app (PID 21532) not touched; only my PIDs 22184, 17712 (exited themselves) and 20716 (killed) were mine. Item 1 closes the earlier hole where a scratch run could open the real database.

## Untestable lines
None. (Line 5 agentic half is out of this side's scope, not untestable.)

## New cases
- Settings > Income: click in the salary box, click the page title "Settings", press Tab -> focus goes to the Employed toggle (same as a blank click), never to the nav list.
- Salary box: type 999999999999 -> either all 12 digits are shown with the message, or the cut to 9 digits is explained.

## Lessons
- In this worktree-isolated agent, Write/Edit to the shared checkout and some heredocs are refused; write per-run scripts into the scratchpad and import the helper from regular/qa via sys.path.
- A Win32 MessageBox in the app ignores posted BM_CLICK / WM_COMMAND; close it with UIA Invoke on its OK button (no real input needed).
- The live app writes data/mentor-winui.log every minute and the WAL every few minutes: a before/after timestamp diff of data/ needs the write rhythm (seconds pattern, times before launch) to attribute changes.
- A "blank" click on page text is not a blank click: check the element under the point (it parked differently here).
- Don't wait on a background build with a polling loop on its output file; wait for the completion notice (lost 5 minutes).

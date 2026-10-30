VERDICT: FAIL - workflow=regular feature=G12 round 2 (build 9d1a621) defectsQa=1
All 12 acceptance lines pass. The one defect breaks the 2026-10-30 user ruling ("Shift+Tab after leaving a box moves to the control before it"), outside the checklist: after a park by blank click, Tab and Shift+Tab do not continue from the box that was left.

Run: 12:01-12:10 (about 10 of 20 min). Isolated worktree, detached at 9d1a621, Debug. Scratch MENTOR_ROOT = new empty folder `scratchpad\mroot-g12r2-empty` with nothing copied in. Fake key via PLANILLIUM_SCRATCH_AI_KEY, AI URL pointed at 127.0.0.1:8765 (no AI call made). cmdkey /list before = after. Only my own PID 11028 was launched and killed. Before every click and keystroke, a check confirmed my window was in the foreground (h.py guard()).
Deviation: MENTOR_INSTANCE_SUFFIX=`verifyg12r2`, not `verify`. A leftover Debug instance from another worktree (PID 7256, `agent-a3af907c775522590`) holds the `verify` mutex. With that suffix my app exited at once. I did not touch that process because it is not mine.

## Acceptance lines
| Line | Result | Evidence |
|---|---|---|
| 1 cap Esc | PASS | Saved 8; typed 12 + Esc -> box 8, no message, focus to IncomeFocusPark; typed 9 -> no box changed |
| 2 Model + key Esc | PASS | Model: saved qa-model-final; typed xyz-typed + Esc -> qa-model-final, config unchanged, parked, 9 changes nothing. Key: typed sk-ant-TYPED-qa-0001 + Esc -> parked, box empty, "A key is saved on this PC." unchanged, cmdkey unchanged, 9 changes nothing |
| 3 -5 Enter Esc | PASS | After Enter the box shows -5 and "The cap must be more th..."; after Esc it shows 8, the message is gone, config cap 8 (`qa/l3.png`) |
| 4 cap blank click | PASS | Typed 6, clicked SectionScroll blank -> config cap_eur 6, focus parked |
| 5 Income Esc | PASS | Salary 2 925: typed 4000 + Esc -> 2 925, config unchanged, parked, 9 changes nothing. Tag 1 600: typed 1234 + Esc -> 1 600 |
| 6 | PASS (round 1, code unchanged) | QA-REPORT-G12-round1.md |
| 7 | PASS (round 1, code unchanged) | QA-REPORT-G12-round1.md |
| 8 | PASS (round 1, code unchanged) | QA-REPORT-G12-round1.md |
| 9 strict build + tests | PASS | `-warnaserror --no-incremental`: 0 warnings, 0 errors; dotnet test 229/229 |
| 10 Model blank click | PASS | Typed qa-model-blank + blank click -> config model qa-model-blank, parked, 9 changes nothing (`qa/l10.png`). Enter saves (qa-model-enter, parked). Empty + blank click -> box and config show the default claude-sonnet-5-5. After switching to Today and back to Settings > AI the box shows the saved qa-model-final |
| 11 cap 6 + blank + 9 | PASS | Focus moved to IncomeFocusPark; typed 9 -> no Edit changed, the API key box included (`qa/l11.png`) |
| 12 salary Esc Shift+Tab | PASS | Reference: Shift+Tab straight from the salary box -> nav "Settings" item. Click, Esc, Shift+Tab -> nav "Settings" item (same) |

## Regression (one trial each: Esc, blank click, then 9)
| Box | Result | Evidence |
|---|---|---|
| AI cap / Model / key | PASS | Above. Shift+Tab after an Esc park on cap -> Model box (correct) |
| Work start | PASS | Esc reverts 08:00. 07:30 + blank click -> saved to config, parked, 9 changes nothing |
| Grace NumberBox | PASS | 17 + Esc -> 18, parked. 18 + blank click -> saved, parked. A click on the compact spin-up -> 18 to 19, focus stays in the box; a blank click then saves 19 and parks |
| Keyword (On-plan) | PASS | Esc reverts (empty). qakw + blank click -> activity_rules.on_plan ["qakw"], parked |
| Your name | PASS | Esc reverts QA. Blank click saves QAZed, parked |
| Income salary | PASS | 3000 + blank click -> config 3000, parked. Tab after an Esc park -> EmployedToggle (correct) |
| Income tag | PASS (Esc/blank) / FAIL (Tab after blank park) | Esc reverts 1 600. 1700 + blank click -> config 1700, parked. Tab after the blank park: see D1 |
| New payable tag name/rate | PASS with note | Esc clears the text. Focus goes to the "Income" item of the section list, not the park button (see observations) |

## Defects
**D1 (medium): after a park by blank click, Tab and Shift+Tab start from the park button, not from the box that was left.** After an Esc park they work.
- Repro 1: Settings > AI, click the cap box, type 6, click blank page space, press Shift+Tab -> focus goes to the nav "Settings" item. Expected: the Model box, which is what you get after an Esc park. Seen 2 out of 2.
- Repro 2: Settings > Income, click the Studioshoo tag box, click blank space, press Shift+Tab -> nav "Settings" item. Expected: EmployedToggle, which is what you get after an Esc park. Tag box, type 1700, blank click, Tab -> EmployedToggle. Expected: the "Remove" button, which is what you get after an Esc park.
- Why it matters: after clicking away, a keyboard user jumps out of the page, or to the top of it, instead of continuing next to the box they were in. The 2026-10-30 ruling says Shift+Tab after leaving a box goes to the control before it.
- Fix in plain terms: when a blank click parks the cursor, remember the box that was left, the same way the Esc path already does. The park button's Tab and Shift+Tab then hand off from that box.
- Why that fixes it: Esc already records the box that was left, and that is why Tab and Shift+Tab work correctly after Esc. The blank-click path does not record it, so the default tab order from the page-level park button takes over.
- Line 12 passes only because the control before the salary box happens to be the same nav item the park button falls back to.

## Security
- No real key was used, and cmdkey was the same before and after.
- The key typed and then cancelled with Esc was not stored.
- Real data was never touched: no data/, config.json or plans/, and the live Release exe was left alone.

## UNTESTABLE
None.

## NOT RUN
Round-1 New cases 1-3 were run: they are lines 11, 10 and 12 above, all PASS.

## Extra observations
- In the New payable tag name and rate boxes, Esc moves focus to the "Income" item of the section list, not to the park button. One trial: typing "d" afterwards did nothing (the section stayed Income, no box changed), so nothing harmful happens. It is still inconsistent with every other box.
- A stale Debug instance from worktree agent-a3af907c775522590 (PID 7256) is still running and holds the `verify` mutex. Any future QA run using suffix `verify` will exit at once.

## New cases
- Settings > AI: click the cap box, type 6, click blank space, press Shift+Tab -> focus goes to the Model box.
- Settings > Income: click the tag box, click blank space, press Tab -> the tag's Remove button. Shift+Tab -> EmployedToggle.
- Settings > Income: click the New payable tag name box, type text, press Esc -> the text is cleared and focus is parked (not on the section list).

## Lessons
- A Shift+Tab test can leave focus in the nav list. Typed letters then navigate by type-ahead ("t" opened Today), and every later step in the script is invalid. After any Tab or Shift+Tab test, put focus back in the page before the next step. I had to discard and re-run the first Model pass because of this.
- Choose "blank" points by ElementFromPoint, not by an offset from a box. My first Hours point (Work start + 300, +100) landed on the Repeat NumberBox and typed 9 into it.
- Check the result of a park against the Esc path. A matching reference (line 12) can hide a fallback bug.
- WinUI compact NumberBox spin buttons sit in an unnamed PopupHost. Click by coordinate: top half of the popup = up.

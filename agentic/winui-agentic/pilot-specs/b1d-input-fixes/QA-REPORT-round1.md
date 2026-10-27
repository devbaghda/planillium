VERDICT: FAIL - workflow=agentic feature=b1d-input-fixes defectsQa=2

Commit under test 9ad951c (isolated worktree `.claude/worktrees/qa-ag`, detached at 9ad951c, Debug build, scratch MENTOR_ROOT `_scratch_root`, `MENTOR_INSTANCE_SUFFIX=verify`, no real data). Fixture: salary 2925, tag Studioshoo 1600. Driven with REAL mouse clicks and keystrokes (pywinauto), with guards that abort on any click outside the app window or any keystroke while the app is not the foreground window (no abort fired). No other app launched.

Build: `dotnet build -p:Platform=x64 -c Debug -warnaserror --no-incremental` = 0 warnings, 0 errors. Tests: 362/362 passed (test project rebuilt first, so not stale).
Scripts/evidence in `qa/`: `b1d_lib.py` (guards + helpers), `run_b1d.py`/`.out`, `run_b1d_2.py`/`.out`, `run_b1d_3.py`/`.out`, screenshots `b1d*.png`.
Note: no `qa-kit/` folder exists in the project, so I wrote `qa/b1d_lib.py`; it should be moved into a shared qa-kit.

## Per-line results

| Line | Result | Evidence |
|---|---|---|
| 1 empty amount, Add click then Enter in name | PASS | At 1, 3 and 5 tags: nothing added, config has no "asd", message "Monthly net needs a number." under the line (rect inside window at 5 tags; screenshot `b1d_refuse_empty_5tags.png`) plus the same text in the status line |
| 2 amount "abc" | PASS | Letters are dropped, box stays empty; Add click, Enter in name, Enter in amount all add nothing; message visible at 1/3/5 tags |
| 3 Tab out of amount keeps text, then Add works | PASS | After Tab: name "zed", amount "500", not added, focus on the Add button; mouse Add then adds zed=500 |
| 4 Esc / click outside clears | PASS | Esc with cursor in the line, and click on blank area: both boxes empty, nothing added. (See defect 2 for the state after a refused mouse Add.) |
| 5 focus never lands in salary box | PASS | 20 combinations (origin: salary box, tag box, add line, nowhere x target: blank, heading, label, Currently-employed button, window title area), real clicks, then typed "9": no Edit focused, salary box/config, tag box/config and add line unchanged each time (`run_b1d_3.out`). First two runs showed leftovers in the add line from earlier steps (script artefact, rerun clean) |
| 6 "500 123" readout | PASS | Salary, tag box and new-amount box: box shows `500 123` (grouped), readout `= €2 976,92/h` (correct = 500123/168) or empty; never "= --/h" (`b1d_500123_*.png`) |
| 7 "Saved." gone after a refused entry | PASS | After saving salary: "Income settings saved." shown; after refused Add: only the refusal message |
| 8 mouse Add after valid entry | PASS | ok1/400 added; focused elements afterwards = none (no Edit, not the Add button) |
| 9 emptied / "abc" box, leave it | FAIL | Leaving by Tab, click on blank area, click on other box: box reverts to the stored value (12 of 12 PASS, salary and tag; spaces-only same). Leaving by **Enter** (all of "", "abc", spaces, salary and tag box): box stays blank, focus stays in the box, stored value unchanged; the only message is "Not saved: type an amount." at the bottom edge of the window (`b1d2_enter_salary_empty.png`). See defect 1. Tag "0": text stays, "Amount must be more than 0." shown, stored unchanged (PASS) |
| 10 B1/B1b/B1c still hold | PASS | Enter saves a valid value and focus ends nowhere (salary and tag); thin-space grouping `1 234 567`; typed minus stays with "Amounts can't be negative." after Tab, stored value unchanged; add-line minus and 0 messages visible at 5 tags (inside window, screenshots `b1d_minus_5tags.png`, `b1d_zero_5tags.png`); no spinner elements; Tab, Tab, Enter on the Add button adds a tag |
| 11 build clean, standing checks, all lines covered | PASS | Build/test above; standing checks run (see below) |

Standing input-box checks (app-qa-checks section 5): empty, spaces only, letters, minus, 0, 40-digit value, valid value in salary, tag, new-name and new-amount boxes; left by Tab, Enter, Esc, click on other field, blank area, heading, label, button. Invalid input was never saved and never turned into 0; only exception is the Enter path of defect 1. Layout re-run at 1, 3 (N) and 5 (N+2) tags with rectangles and screenshots: message always inside the window.

## Defects

1. **Enter in an emptied money box leaves it blank and focused (line 9, user rule "Enter saves, focus ends nowhere").** Repro: Settings -> Income, click the salary box (or a tag box), clear it (or type letters/spaces only), press Enter. Result: box is blank (placeholder 0), the cursor stays in it, stored value stays 2925; the only explanation is "Not saved: type an amount." in the window's bottom status line, far from the box. Typing a digit then goes into the still-focused box and Tab saves it (7 was saved in my run). Tab, click-away and click-on-other-box paths do revert correctly, so only the Enter path is missed. Expected: the saved amount comes back and focus ends nowhere, or at least a message next to the box.
2. **After a refused Add by mouse click, Esc and click-outside no longer clear the line (minor, outside the checklist, user rule 2026-10-25 "Esc / click-outside clears the add line").** Repro: name "asd", amount empty, click Add with the mouse. Message shows, focus is parked (not in the line), "asd" stays. Esc does nothing; a click on blank area does nothing; the text and the message stay until the user clicks into the line and presses Esc. (After a refusal by Enter, the cursor stays in the line and Esc clears it correctly.)

## Security

Diff reviewed (MoneyBox.cs, SettingsPage.xaml/.cs): only UI input validation and focus handling; no SQL, shell, file-path, network, secret or dependency changes. Findings: none. Input limits: a 40-digit entry is cut to 9 digits (999 999 999) and saved without any message (observation, not a defect against the request).

## Untestable

None.

## Extra observations

- After a refusal by Enter the cursor stays in the add line (by design); mouse Add parks focus.
- Status message "Not saved: ..." sits at the very bottom of the window and duplicates the message under the box.
- B1c-style layout (message below the fold) is fine at 5 tags: the message was inside the window each time.

## Lessons

- The Enter path and the Tab/click path of the same box are separate code paths; a fix covering "leaving" must be re-tested with Enter, Tab, click-away and click-other-box (cause: revert lives in LostFocus, Enter keeps focus). Rule: test every way out of a box, not one.
- Leftover state from earlier script steps (text left in the add line after a refused mouse Add) polluted later checks; reset the add line before each focus matrix and rerun on a clean start before calling a failure.
- No qa-kit exists yet; each side re-writes UIA helpers. Rule: move `qa/b1d_lib.py` (foreground and inside-window guards, shot, focus listing) into a shared `qa-kit/`.
- Heredocs containing quotes can be rejected by the shell tool; write scripts with the file tool instead.

VERDICT: PASS WITH CAVEATS - workflow=regular feature=b1d-input-fixes defectsQa=0

# QA report: B1d input fixes (regular side), 2026-10-25

Build tested: commit b1c91fe in its own QA worktree (`.claude/worktrees/qa-b1d`, detached). Strict Debug build
(`-warnaserror --no-incremental`): 0 warnings, 0 errors. `dotnet test --no-build`: **229/229 passed** (regular).
The agentic side was not built or run here, because this QA run covers the regular side only.
The app ran against a scratch MENTOR_ROOT (`scratchpad\b1d\root`: fixture config with salary 2925 and Studioshoo
1600, empty `data/` holding only a `winui_state.json` that marks today's kickoff and review as done, no `progress.db` copied),
with `MENTOR_INSTANCE_SUFFIX=verify`. Each script started a new copy of the app and stopped only that copy. The live
Release instance (PID 9396) was not touched.
All UI actions used real mouse clicks (`click_input`/`mouse.click`) and real keystrokes (`send_keys`). For every
focus check, QA read the focused element and then typed "9" to confirm that no box and no part of config.json changed.

## Per-line results

| # | Result | Evidence |
|---|---|---|
| 1 | PASS | Name "asd" with an empty amount, refused four ways: mouse click on Add, Enter in the name box, Enter in the empty amount box, and amount of only spaces. Each time nothing was added and config had no "asd". The red line "Monthly net needs an amount above 0." showed under the line and was on screen (screenshot `qa/b1d_l1a.png`). An amount of "0" was refused the same way, by click and by Enter. |
| 2 | PASS | Typing "abc" leaves the box empty (letters are removed as you type). Add click and Enter were both refused with the same red message, and nothing was added. "12abc" leaves "12". |
| 3 | PASS | Name "zed", amount 500, Tab: both boxes kept "zed" and "500", and focus moved to the Add button. A mouse click on Add then added zed=500 to config. |
| 4 | PASS | Esc in the line cleared both boxes and added nothing. A click on blank space, and a click on the salary box, also cleared the line without adding. |
| 5 | PASS (caveat C1) | 112 click cases in `b1d_l5.out` plus 45 in `b1d_extra.out`. Starting points: nothing focused, salary box (unchanged or edited), tag box (unchanged or edited), add-line name, add-line amount, and after leaving by Enter, Tab, Shift+Tab, Esc or a refused Add. Clicked: blank space (3 spots), the Settings / Income / Paid work headings, the "Employment status" label, the description text, the hourly readouts, the bottom text, the status strip, the sidebar BALANCE label, the Employment toggle (clicked twice), the section list (General then Income), the sidebar Today then Settings, the Add button and Remove. **The salary box never got the cursor and "9" changed nothing**, with one exception: clicking the salary box's own caption "Previous salary — monthly net (€)" puts the cursor in the salary box (see C1). |
| 6 | PASS | "500 123" typed key by key, typed with a real Space and pasted, in the salary box, the Studioshoo box and the new-tag box. The readout read "= €2 976,92/h" (500123 ÷ 168, correct) at every step and never "= —/h" (`b1d_l6b.out`, `qa/b1d_l6.png`). Typing "-500 123" shows "—/h" with the minus message; deleting the minus shows "€2 976,92/h" right away. A tag added at "500 123" shows "€2 976,92/h". |
| 7 | PASS | First a salary save produced "Saved."; then name "asd" with an empty amount was refused. The status strip was then empty, and only the red refusal message showed. A duplicate name ("zed") and a minus amount also clear "Saved.". |
| 8 | PASS | After "zed"/500, a mouse click on Add added the tag. Focus then went to the invisible "nowhere" spot (`IncomeFocusPark`), not to any box and not to Add; "9" changed nothing. The next Tab carries on past the button. The same holds for the Enter add from the name box and from the amount box. |
| 9 | PASS | Salary and Studioshoo boxes, each with these inputs: empty, only spaces, "abc", and "0" (tag box only). Each was left by Tab, Enter, a click on another field, a click on blank space and a click on a heading. **Every time the box went back to the stored amount and config stayed unchanged**, with the status message "Previous salary needs a number — the saved amount was kept." or "\"Studioshoo\" needs an amount above 0 — the saved amount was kept." A minus stays as typed, with the red message, and config is unchanged (108 cases, `b1d_boxes.out`). Esc does not leave a money box (see O1). |
| 10 | PASS | Enter saves and moves the cursor to nowhere (both boxes). A minus is kept, and stays with its message after Tab or a blank click; digits typed after the minus keep the sign; deleting the minus hides the message (`b1d_reg.out`, screenshots `b1d_reg_minus_*.png`). Numbers show thin-space grouping ("2 925", "500 123"). No spinner arrows: each money box has only its caption text as a child. B1c line 5, Enter in the name box adds the tag, still holds (nmx=300). |
| 11 | PASS | Strict build clean; 229/229 tests. QA ran the §5 standing input-box checks: empty, spaces, text, "-", "-5", a very long value, valid values, and "0", each left by Tab, Enter, Esc, another field, blank space and a heading, on both stored boxes. The new-tag line was covered by lines 1-4 and 8. |

## Defects

None that fail an ACCEPTANCE line or plainly break REQUEST.md.

## Caveats (need a user decision, not counted as defects)

- **C1: clicking a box's own caption puts the cursor in that box.** Clicking the caption "Previous salary — monthly
  net (€)" puts the cursor in the salary box, and a following "9" changes the box text ("2 925" becomes "29 259"; nothing is saved
  until the box is left). The same happens with "New payable tag" and the other box captions. This is standard Windows
  behaviour: the caption belongs to the box. The code also counts it on purpose as "clicked on the box". It is
  ambiguous against ACCEPTANCE 5 ("click … a label: focus never lands in the salary box") and REQUEST 3 ("unless the user
  clicked … into it"). **Why it matters:** a user who clicks the caption to "click away", then types, changes the salary box. **Possible fix:**
  treat a press on a caption like a click on blank space, so the cursor goes nowhere. **Why that works:** the cursor would
  then enter a box only from a click inside its white field. The user decides whether the caption counts as part of the box.
  Repro: `qa/b1d_l5.py`, row "nothing -> label salary box header".

## Other observations (outside the checklist; not defects)

- O1: Esc in the salary or tag box does not leave the box (normal text-box behaviour; no user rule says otherwise). If
  the box was emptied first, it stays empty and shows "= —/h" until it is left; leaving it then restores the amount
  (line 9).
- O2: Salary "0" is accepted and saved. No rule forbids a 0 salary, but the request only says that tags may not be 0.
- O3: A very long entry ("9999…" with 23 digits) is cut to 9 digits ("999 999 999") without a message and saved. Nothing breaks,
  but the user is not told the number was cut.
- O4: Esc in the add line moves focus to the Settings section list (this dates from B1b and is not new). Typing "9" or "s" there changed
  nothing.
- O5: After a *refused* Add, the cursor goes to the box that needs fixing (builder's choice, recorded in DECISIONS.md).
- O6: The status-strip message for a restored amount sits at the bottom of the page, about 500 px below the box. It is visible but
  not next to the box.
- O7: In the scratch instance, the Settings list shows "AI: Key saved" and "TickTick: Connected". The app's
  credential store is not separated by MENTOR_ROOT, so a scratch QA instance can reach the user's real AI key and
  TickTick token. QA never triggered AI or TickTick. This existed before B1d, but it matters for every scratch-root QA run.
- O8: The first line-5 run was invalid. Timed prompts (kickoff, then evening review) opened during it, took the cursor
  away, and, in a way not reproduced, salary ended as 55. A focused retry (`b1d_dialog2.py`) showed that the dialogs block every click and
  key and change nothing. After the dialogs were closed with the mouse, focus went to the "Today" nav item, not the salary box
  (`b1d_dialog.out`). Every later run pre-marks today's prompts as done.

## Security (by hand against the /security-review categories; diff b1c91fe)

- Injection: none found (amounts are parsed as digits only; the tag name is slugged; no SQL, shell or path built from input).
- Authentication/authorisation, debug switches: none found (no new switch).
- Secrets and crypto: none in the diff. See O7 (pre-existing: the shared credential store reaches scratch instances).
- Unsafe deserialisation / code execution: none found.
- Data exposure: none found (nothing new is logged or exported).
- Input validation: empty, spaces, text, minus, 0 and long values were all exercised live. Invalid values are never saved and
  never replaced by 0. Long values are silently capped at 9 digits (O3).
- Business-logic abuse: none found. A tag at 0 cannot be saved by Add (click or Enter) or by editing an existing tag.
- Supply chain / config: no new dependencies.
- Resource abuse: none found (the new dispatcher work happens once per pointer event, with no loops).

## Untestable lines

None.

## Files

Scripts and outputs are in `D:\CLAUDE\Planillium\regular\qa\`: `b1d_h.py` (helpers and launcher),
`b1d_l5.py`/`.out`, `b1d_main.py`/`.out`, `b1d_boxes.py`/`.out`, `b1d_extra.py`/`.out`, `b1d_l6b.py`/`.out`,
`b1d_reg.py`/`.out`, `b1d_dialog*.py`/`.out`, `b1d_dump.py`, and screenshots `b1d_*.png` (taken with PrintWindow flag 2).

## Lessons

- Timed app prompts (kickoff, evening review) appear after work-start or end-of-day time on an empty scratch root and take the cursor
  in the middle of a run. This made the first line-5 run worthless. Rule: pre-seed `data/winui_state.json` with today's
  `last_kickoff`/`last_review` before launch (now done in `b1d_h.launch`), and test the dialog path separately.
- The app window shifts about 52 px on every launch. Fixed screen coordinates then click the wrong control (a click meant for the section list
  switched to another section). Rule: always click at window-relative coordinates read from the live window rectangle.
- A box's caption is part of the box. "Click a label" checks must click both free-standing text and box captions,
  because they behave differently (C1).
- The finder that collects "red" message texts by keywords also caught the status strip. Read messages by automation ID
  (`SaveStatus`) separately from the red line.
- There is no `qa-kit/` folder in the project yet (§6). The helpers live in `regular/qa/b1d_h.py` until one is set up.

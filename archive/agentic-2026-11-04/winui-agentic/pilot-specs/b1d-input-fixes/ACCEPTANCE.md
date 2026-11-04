# Acceptance -- B1d (scratch MENTOR_ROOT only; never real data)

Fixture: salary 2925, tag Studioshoo 1600/month. Use REAL mouse clicks and keystrokes, not only programmatic focus.

1. Add line: name "asd", amount empty, press Add button; then again with Enter in the name box: nothing added,
   message visible, config.json has no "asd".
2. Same with amount "abc": nothing added, message visible.
3. Add line: name "zed", amount 500, press Tab out of the amount box: text stays in both boxes; then Add works.
4. Esc / click outside the add line still clears it without adding.
5. Click blank space, a Settings heading, a button, a label: focus never lands in the salary box; typing "9"
   afterwards changes nothing. Repeat after leaving the salary box, a tag box and the add line.
6. Amount "500 123" in any money box: the readout is correct or hidden, never "= --/h".
7. Refused entry (line 1) after a successful save: "Saved." no longer shown.
8. Click the Add button with the mouse after a valid entry: tag added, no box and not the Add button holds focus.
9. Emptying a money box or typing "abc" then leaving: box and stored value agree, or the text stays with a visible
   message; no silent mismatch.
10. B1/B1b/B1c lines still hold: Enter, minus, thin spaces, no spinners.
11. Build clean (/warnaserror); QA runs the standing input-box checks (app-qa-checks §5) and covers every line above.

# Acceptance -- B1b money boxes (run only against a scratch MENTOR_ROOT, never real data)

Fixture: config with previous salary 2925, one payable tag "Studioshoo" 1600/month.

1. Type 3000 into the previous-salary box without leaving it: config.json still holds 2925.
2. Leave the box (Tab or click elsewhere): config.json holds 3000; the box shows "3 000".
3. Type 3100 and press Enter: config.json holds 3100.
4. Same three behaviours for a payable tag's monthly-net box.
5. Same for the new-tag amount box (value is used when the tag is added; no save before that).
6. Click the Income heading, then type "9": no money box changes; config.json unchanged.
7. Type "-" in a money box: a hint containing "can't be negative" (or equivalent wording) appears; the box does
   not keep the minus sign; the hint disappears on the next valid keystroke or when the box is left.
8. Leaving a box with an empty or invalid entry behaves as before B1b (no crash, previous value kept).
9. Thin-space formatting and no spinner arrows (B1 lines 6-8) still hold.
10. Build clean (0 warnings with /warnaserror); tests pass; QA's shared suite covers lines 1-4 and 7.

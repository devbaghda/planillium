# Acceptance -- B1f (scratch MENTOR_ROOT only; never real data)

Fixture: salary 2925, tag Studioshoo 1600/month. REAL mouse input (not programmatic focus), clicks about 1 s apart.

1. Click in the "New payable tag" box, then click empty pane space; repeat 20 times at varied points (left, middle,
   right, top, bottom of the pane): the salary box never holds the cursor.
2. Same, starting from the tag-amount box, the salary box's neighbours, and a Studioshoo amount box.
3. Same, but the click on empty space is the FIRST click after the window was inactive (switch to another window
   and back, then click blank).
4. Same, but with the mouse wheel scrolled once before the blank click.
5. After each case, typing "9" changes nothing in the salary box.
6. B1 to B1d lines still hold. Build clean (/warnaserror). QA runs the standing input-box checks
   (app-qa-checks section 5) and reports the root cause the coder found.

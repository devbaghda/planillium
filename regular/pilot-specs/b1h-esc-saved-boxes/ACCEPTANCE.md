# Acceptance -- B1h (scratch MENTOR_ROOT only; never real data)

Fixture: salary 2925, tag Studioshoo 1600/month. Real keyboard input. Check both the Previous-salary box and a tag
amount box for every line.

1. Type 2500, press Esc: the box shows the saved amount (2 925), the cursor is not in any money box, stored amount
   unchanged.
2. Empty the box, press Esc: same result as line 1.
3. Type letters (abc) or spaces, press Esc: same result as line 1.
4. Type -5, press Esc: same result as line 1 (message gone or harmless).
5. A valid new amount + Enter still saves and parks the cursor (B1d unchanged). Esc in the new-tag add line still
   clears the line and leaves it. Tab and click-elsewhere behave as before this task.
6. B1 to B1d lines still hold. Build clean (/warnaserror). Scope chosen from the diff.

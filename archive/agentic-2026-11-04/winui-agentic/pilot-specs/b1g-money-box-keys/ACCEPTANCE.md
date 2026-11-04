# Acceptance -- B1g (scratch MENTOR_ROOT only; never real data)

Fixture: salary 2925, tag Studioshoo 1600/month. Real keyboard input. Check both the Previous-salary box and a tag
amount box for every line.

1. Type 2500, press Esc: the box shows the saved amount (2 925), the cursor is not in any money box, stored amount
   unchanged.
2. Empty the box, press Esc: same result as line 1.
3. Type letters (abc) or spaces, press Esc: same result as line 1.
4. Type -5, press Enter: message "Not saved: Amounts can't be negative." (or its existing wording) is visible, the
   cursor is still in the box, stored amount unchanged.
5. Type -5, press Esc: same result as line 1 (box shows the saved amount, cursor leaves, message gone or harmless).
6. Type 12 digits (999999999999): a visible message says the amount is too large / refused; nothing saved; after
   leaving the box (Enter, Esc, Tab, click elsewhere) the box shows the saved amount, not 12 digits.
7. A valid new amount + Enter still saves and parks the cursor (B1d unchanged). Esc in the new-tag add line still
   clears the line and leaves it.
8. B1 to B1f lines still hold. Build clean (/warnaserror). QA runs the standing input-box checks
   (app-qa-checks section 5), scope chosen from the diff.

# Request -- B1g: keys in the saved money boxes (Settings -> Income), agentic app

Found by QA in B1f (Previous-salary box) and ruled by the user on 2026-10-27. Applies to the saved amount boxes:
Previous salary and every payable tag's amount box.

1. Esc in a saved money box discards what was typed, shows the saved amount again, and moves the cursor out of the
   box (nowhere / parked, as after Enter in B1d).
2. Esc in a box that was emptied shows the saved amount (not an empty box) and the cursor leaves the box.
3. Typing "-5" and pressing Enter refuses the value (message "Amounts can't be negative." stays visible), the cursor
   stays in the box, the saved amount is unchanged.
4. A 12-digit amount is refused with a visible message; nothing is saved; the box does not keep showing the 12
   digits after the user leaves it.
Unchanged: everything in B1 to B1f (the new-tag add line keeps its own Esc: clears the line and leaves it).

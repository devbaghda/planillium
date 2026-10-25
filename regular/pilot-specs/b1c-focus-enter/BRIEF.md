# Brief -- B1c (user decisions 2026-10-25, from the user's live check of the regular B1b app)

Rule kept (user): in the add-paid-job line, Esc or a click outside clears it without adding a tag (B1 rule stays).

New (user's words, paraphrased):
1. Pressing Enter in the add-paid-job line must work exactly like pressing the Add button; afterwards the cursor
   is nowhere -- it leaves the box.
2. Pressing Enter in any money box (salary, payable-tag monthly net) saves and the cursor leaves the box (nowhere).
3. Leaving a money box (click elsewhere or Tab) must not send the cursor to the salary box; it just leaves.
   Nothing has the cursor afterwards.

Context: follows B1b (regular 07d6ec4, agentic 7a5b58d). Agentic app not yet eyes-on checked by the user; any
findings there will be added as a dated addendum before B1c starts. Same constraints as B1b BRIEF.

## Addendum 2026-10-25 (user, after eyes-on check of both apps)
Agentic app showed the same behaviour as regular for items 1-3 above.
4. Minus sign (reverses B1b line 7): a typed minus is KEPT in the box together with the digits, as typed ("-500"
   stays "-500"). The message "Amounts can't be negative." stays visible for as long as the content is negative/
   invalid and disappears only when the value becomes valid. While invalid, nothing is saved (nor can the add line
   be added). Message visibility: the agentic version's is clearer; the regular one should be as noticeable.
   Open question for the user (not yet answered): what happens on leaving a box that still holds an invalid
   value -- assumed: previous saved value stays, box reverts to it, message clears.
   ANSWERED (user, 2026-10-25): leaving a box with a negative value behaves like leaving with a positive one --
   the text STAYS in the box (not reverted), the message stays visible, and the saved value is unchanged (nothing
   saved while invalid). Supersedes the "assumed" line above.

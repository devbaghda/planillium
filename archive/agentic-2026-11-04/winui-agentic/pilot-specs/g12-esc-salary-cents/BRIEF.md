# G12 brief
The user's words (2026-10-30): "esc and clicking in the areas does not move focus out of the box" (regular Settings AI cap); agentic: click-away works and saves, Esc does nothing. User ruling: Esc leaves the box and reverts to the saved value, in both apps (the Income boxes already do this, B1h).
Context: G8 (agentic) added FocusShield for other pages; regular has the B1f focus fix on Income only. Reuse what each side already has rather than a second mechanism.
Constraints: scope = REQUEST.md only; no refactor. Hours rounding stays. Real data is never touched; scratch runs only. Strict build and the existing suite must be green before hand-off (precheck).
Please also check the sibling cases: every Settings text/number box, not only the three named.

## Addendum 2026-10-30 (user rulings, after regular QA round 1)
- Salary ceiling: EUR 100 000 per month for salary, every payable-tag amount and a new payable tag; above it: "Amount is too large - at most EUR 100 000,00 a month. The saved amount was kept." (user confirmed).
- Settings -> AI Model box: saves when you leave the box (like the AI cap box: leave by click-away or Enter); the box never shows an unsaved value.
- A click on blank page space must leave NO text box holding the cursor (never hand focus to another input such as the API key box); typing afterwards reaches nothing. Shift+Tab after leaving a box moves to the control before it.

## Addendum 2 2026-10-30 (user ruling after regular round 2)
After a blank-area click leaves a box, Tab and Shift+Tab continue from the box that was left (same as after Esc), never from a hidden park control or the nav list. Esc in the "new payable tag" name/rate boxes empties the line and parks the cursor like every other box. A click on a NumberBox spin button must still change the value.

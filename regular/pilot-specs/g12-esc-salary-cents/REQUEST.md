# G12 - Settings box Esc rule, salary ceiling, Week/Month cent match (user, 2026-10-30)

Found by the user and the regular re-QA of 2026-10-30. Same text on both sides.

1. Settings text and number boxes: Esc releases the box and puts back the last SAVED value (the Income boxes already behave this way; make every other Settings box the same: AI cap, Model, API key, and any other text/number box on Settings). Clicking a blank area of the page (not only the sidebar) also releases the box; a click-away keeps saving the typed value as it does today. Any validation message belonging to the discarded text disappears with it.
2. Salary box: an absurd amount (for example 999999999999) is refused with "Amount is too large", nothing is saved, and the box shows the saved amount. Today regular saved 999 999 999 EUR/month silently. Choose a sensible ceiling and state it in the hand-off.
3. Reports: the paid-work figure of one week is the same to the cent in Week, Month and Year (regular showed 99,20 in the Week Total but 99,21 in the Month and Year rows). The totals-are-the-sum-of-the-shown-cents rule from G11 stays.
Out of scope: hours rounding in Reports (decided: leave as is).

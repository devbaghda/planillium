# Request -- B1f: cursor jumps to the salary box after a click on blank space (Settings -> Income)

Found by the user, 2026-10-26, in the agentic app after B1d PASS (line 3 of B1d was marked fixed).

1. After the cursor was in the "New payable tag" box, a click on empty space in the Settings pane (not on a
   label, heading, button or box) must not put the cursor in the salary box. It happened about 2 clicks out of 5,
   with the real mouse, clicks one second apart, never reproduced by scripted clicks.
2. Same for every other way of leaving a box (another box, the add line, a tag amount box): the cursor lands in
   the salary box only if the user clicked or tabbed into it.
Unchanged: everything in B1 to B1d.

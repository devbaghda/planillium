# Acceptance -- G6
1. Overdue banner showing -> Today dot showing (all pages, not only the Today page).
2. No overdue banner -> no dot, including a day with open tasks but nothing overdue, and an empty day.
3. Dealing with the last overdue task (reschedule, complete, day off) removes the dot without a restart; a new overdue
   task (for example at midnight rollover or a plan change) shows it without a restart.
4. Sibling check: list every other place that shows a dot/badge on the sidebar and state whether it is covered.
5. Build clean (/warnaserror); existing tests pass.

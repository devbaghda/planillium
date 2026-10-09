# Acceptance — replace remaining tasks (run only against a scratch MENTOR_ROOT, never real data)

Fixture: a plan with 10 tasks on days 1-10; tasks 1-4 ticked complete, 5-10 not; task 7 has a
reschedule override; pasted replacement JSON contains 5 tasks.

1. An active plan's page shows a "Replace remaining tasks…" button next to "+ Add task".
2. The dialog shows a copyable Claude prompt that lists the 4 done tasks.
3. Pasting invalid JSON shows an error in the dialog; nothing is changed; dialog stays open.
4. Pasting valid JSON shows a preview "6 unfinished tasks removed, 5 new tasks added" before applying.
5. Cancel at the preview leaves the plan file byte-identical.
6. Confirm: tasks 1-4 are still present, on the same days, still ticked complete.
7. Confirm: tasks 5-10 no longer exist in the plan file; the 5 new ones exist.
8. The 5 new tasks have days starting at (day of last done task) + 1, keeping the pasted relative spacing. (Fixture: last done is day 4, so start = 5.) This holds even for a plan that is far behind (59 days old: start is still 5; the new tasks then show as overdue — user decision 2026-10-09).
8a. After confirm, the plan's total length equals the last new task's day (shorter or longer than before); "Day X of Y" shows that Y; the sidebar "Finishes" date is the calendar date of that last day; the sidebar drift line reads on-track (0 d late), not the old lateness.
8b. The preview shows the new finish date and the old one.
9. Plan file fields the app doesn't model (briefing, extra phase fields) survive untouched.
10. Past score history (daily_score, completed-task rows for 1-4) is unchanged.
11. The removed tasks' reschedule overrides no longer affect anything (no phantom task appears).
12. A plan with zero done tasks: new tasks start at day 1.
13. A plan with all tasks done: action is refused with a clear message (nothing to replace).
14. Pasted JSON with a task title identical to a done task's title is rejected (would collide with its completion record).
15. Queued/archived plans do not offer the button.
16. Build clean (0 warnings with /warnaserror); existing tests still pass.

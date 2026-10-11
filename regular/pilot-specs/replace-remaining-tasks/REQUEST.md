# Feature request: replace the remaining part of a plan

User (2026-10-09): "I want to have the ability to change the remaining part of the plan. For example
the AI microsolutions plan has tasks for Reddit, but I understood I do not need Reddit for this. I
want to keep what is already done, but change the rest of the plan."

**Settled decisions (user, 2026-10-09):**
- Bulk replace, not per-task editing. One action on an active plan: "Replace remaining tasks…".
- "Done" = tasks ticked complete. They stay exactly as they are, in place. Every other task of that
  plan (overdue, today, future) is removed and replaced by the new list.
- New tasks come from pasted JSON (same format Claude produces for Add Plan). The dialog offers a
  copyable prompt for Claude that includes the plan's done tasks, so Claude rewrites only the rest.
- Before anything is changed the user sees a preview: how many unfinished tasks will be removed,
  how many new tasks will be added. Nothing changes until the user confirms.
- New tasks start on the first plan day after the last done task's day (day 1 if none is done), even if that is in the past — user decision 2026-10-09, reaffirmed after the objection that a far-behind plan then shows its whole new list as overdue. Done tasks never move.
- The new list may be shorter or longer than the old one. Everything derived from plan length follows: the plan's end date, the sidebar "Finishes dd.MM.yyyy" line, "Day X of Y", and the "N d late from plan" figure (which restarts from zero because the schedule was just redrawn). The preview states the new finish date vs the old.
- Because the whole remainder is replaced, there is no "close the gap" question.

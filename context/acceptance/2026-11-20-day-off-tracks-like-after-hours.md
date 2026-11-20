# Acceptance — manual day-off tracks like non-working hours (2026-11-20)

User's words: "let's change the tracking rule. On manual day-offs the program should behave the same
way it behaves on non working hours."
Replaces DECISIONS rule 10's last sentence ("tracking itself still runs normally on a manual day off").
Day-off = every active plan off that day (manual day-off or recurring exclusion mix; a pure recurring
rest day already tracks nothing and stays as is).

Non-working-hours behaviour (the model): only confident on-plan work, or neutral work carrying a pay tag,
is recorded, as it happens; everything else is not recorded; idle is never recorded; no off-plan alert;
no "where were you" question for stretches that fall outside the working window.

## Cases
- D1  Manual day-off, 10:00, window matches a confident on-plan rule -> a diary row is recorded.
- D2  Manual day-off, 10:00, window matches a neutral rule with a pay tag -> a row is recorded with the tag.
- D3  Manual day-off, 10:00, window matches nothing / off-plan / a suggestion-only rule -> no row.
- D4  Manual day-off, idle 15 min at 10:30 -> no idle row, no "where were you" question.
- D5  Manual day-off, work closes when the window changes or the rule stops matching (row ends at "now").
- D6  Manual day-off, no off-plan alert fires however long off-plan (unchanged).
- D7  Manual day-off, evening review: no pending-gap question for the day.
- D8  Same day, not off: unchanged (in hours records everything, idle rows and questions as before).
- D9  Day marked off at 14:00 after a normal session opened at 09:00: the 09:00-14:00 work is kept;
      from the next poll on, the off-day behaviour applies (takes effect within ~2 min, not next day).
- D10 Day un-marked off mid-day: normal tracking resumes within ~2 min.
- D11 Day-off scoring (no points, task-completion exception, Reports exclusion) unchanged.
- D12 After hours on a day off: same as before (after-hours rules).

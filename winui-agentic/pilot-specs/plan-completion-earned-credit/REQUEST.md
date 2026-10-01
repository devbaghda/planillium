# Feature request: plan progress counts as earned money

User request (2026-10-01): "On the days when tasks from the plan are fulfilled count as earned
money, because that way I am moving in the direction of work."

**Settled decision (user, 2026-10-01): proportional.** For a calendar day, credit back
`daily rate × (plan tasks completed that day ÷ plan tasks due that day)`, so finishing 3 of 4 tasks
credits 75% of that day's rate, finishing all credits 100% (that day's lost income nets to zero).
Plans with a task due that day: all active plans together. A day with no tasks due gets no credit.

Assumptions the Planner should state and apply (not re-ask): the credit only matters while the
employment toggle is OFF (unemployed; when employed the day already adds income); it applies to
every closed day since the counter's start date including history, derived from task data, and to
today as a live preview; it must show in the sidebar figure and in Reports day/week/month/year
figures, and the Reports card wording must stay sign-consistent and say what the credit is.
Keep the existing "closed days only in the ledger / live preview for today" rule. Decide and
document how a task completed late (on a later day) is attributed. Don't change the daily-rate
rule or the start date.

# Acceptance — Diary toolbar, Reports income columns, apps pie (2026-11-05)

User's words (2026-11-05): "The horizontal scroll [in the diary filter/mark rows] is excessive and not comfortable — create a better solution." "I do not have an option to mark all selected as paid." "Where did 'clear all filters' disappear?" "Rename [the two confusing columns]; I want to see hours of work for income; add unearned income, paid work and balance columns; do not add horizontal scrolling; add in all the time slices." "I made up my mind on including paid [in bulk marking]." "Pie: too much empty space, looks not nice, come up with another design."
Facts: category **Paid** = bought entertainment time (not income). Income = payable tags (Settings → Income).

**D. Diary rows (no sideways scrolling)**
- D1 At the default window (1180x780) and at 900 wide, the diary's filter row and bulk-action rows show every control with no horizontal scrollbar and nothing cut off.
- D2 "Clear filters" is always visible; clicking it clears category/app/page/tag/rule filters, All time and the search (as before).
- D3 One "Set category" menu offers On-plan, Off-plan, Neutral, Paid, Idle; one "Set tag" menu offers every tag (built-in and payable) and "Clear tag". "Add entry" stays.
- D4 Each action acts on the ticked rows, or on all matches when none are ticked, and the label shows the count once; same confirm/backup/Undo door as before. Choosing Paid sets category `paid` on exactly those rows.
- D5 No action is enabled when there are zero rows.

**R. Reports tables (Day, Week, Month, Year)**
- R1 Money columns, in order, after Score: Paid hours, Unearned income, Paid-work income, Balance. Old headers "Income lost/gained" and "Of which paid work" no longer appear.
- R2 Per row: Paid hours = minutes of payable-tagged diary rows ÷ 60; Paid-work income = those minutes × tag rate; Balance = what "Income lost/gained" showed; Unearned income = Balance − Paid-work income.
- R3 Total row sums every money column; Unearned total + Paid-work total = Balance total to the cent; a row's Month/Year figure equals the same days summed in the Week view.
- R4 No horizontal scrollbar on any of the four periods at the default window (1180x780).
- R5 With no payable-tagged time: Paid hours 0 h, Paid-work income €0,00, Unearned = Balance.

**P. Time-by-app pie**
- P1 The legend uses its width: each row has a share bar (length = share of the largest row) between the name and the hours; no large empty gap.
- P2 Pie shows the total hours in its centre; slices, click to drill down, Back link, tooltips unchanged.
- P3 Drill-down level works the same (pages inside an app, apps inside Other).

**G. Safety**
- G1 Scratch root only, synthetic data. G2 Strict build 0 warnings; suite green; new unit tests for R2/R3. G3 Sibling sweep: ReportExport (HTML/CSV) and any other reader of the old two money fields/headers.

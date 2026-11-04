# Acceptance -- B1 Reports and Settings (run only against a scratch MENTOR_ROOT, never real data)

Fixture: config with previous salary 2925, one payable tag "Studioshoo" 1600/month, user not employed; a
diary with off-plan time this month.

1. Reports, Today section: shows an unearned-income figure and a paid-work-income figure, both with EUR.
2. Same for This week, This month, This year (four sections, two figures each).
3. The paid-work figure for a period equals the payable tags' earnings for that period (Studioshoo only here);
   with no payable tag it shows 0 EUR (or an equivalent empty state), not an error.
4. The unearned-income figure equals what the existing sidebar/Reports earnings logic produced before this
   change for the same period (no calculation changed).
5. The off-plan nudge text contains "distracting apps and activities".
6. Typing 14000 into a money box displays "14 000" (thin or normal space) after typing; the saved value is
   the number 14000.
7. Salary 2925 displays "2 925"; payable tag 1600 displays "1 600"; the new-tag amount box formats the same.
8. None of those boxes shows up/down arrows (spin buttons).
9. Previous salary row: label "monthly net (EUR)", number box, and an "= EUR x,xx/h" readout, laid out like a tag row.
10. Esc in the New payable tag line leaves it without adding a tag; clicking outside does the same.
11. After 10, no tag was added and config.json is unchanged.
12. Entering a non-number, a negative number and an empty box in a money box behaves as it did before (no crash).
13. Build clean (0 warnings with /warnaserror); tests pass; QA's shared suite covers lines 3, 4, 6, 7.
